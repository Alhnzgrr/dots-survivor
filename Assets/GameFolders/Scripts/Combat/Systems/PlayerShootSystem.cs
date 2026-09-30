using ECSSurvivor.Core;
using ECSSurvivor.Player;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSSurvivor.Combat
{
    /// Auto-fire at the closest enemy in range.
    ///
    /// The previous version walked every enemy in the world once per shot to find
    /// the nearest one, so the cost of aiming grew with the size of the wave - the
    /// exact thing an ECS project exists to avoid. It now asks the spatial grid,
    /// which only visits the cells the weapon range actually covers.
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct PlayerShootSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<SpatialHashMap>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            SpatialHashMap hash = SystemAPI.GetSingleton<SpatialHashMap>();

            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            ComponentLookup<ProjectileConfig> configLookup =
                SystemAPI.GetComponentLookup<ProjectileConfig>(true);
            ComponentLookup<LocalTransform> transformLookup =
                SystemAPI.GetComponentLookup<LocalTransform>(true);

            foreach ((RefRO<LocalTransform> playerTransform, RefRW<PlayerWeapon> weapon)
                     in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PlayerWeapon>>()
                         .WithAll<PlayerTag>()
                         .WithNone<Dead>())
            {
                weapon.ValueRW.Cooldown = math.max(0f, weapon.ValueRO.Cooldown - deltaTime);
                if (weapon.ValueRO.Cooldown > 0f)
                {
                    continue;
                }

                Entity prefab = weapon.ValueRO.ProjectilePrefab;
                if (!configLookup.HasComponent(prefab))
                {
                    continue;
                }

                float3 origin = playerTransform.ValueRO.Position;

                if (!SpatialHashQuery.TryFindNearest(
                        hash.Map,
                        hash.CellSize,
                        origin,
                        weapon.ValueRO.Range,
                        out Entity target,
                        out float3 targetPosition))
                {
                    // Nothing in range: hold the shot rather than resetting the
                    // cooldown, so the first enemy to walk in is hit immediately.
                    continue;
                }

                ProjectileConfig config = configLookup[prefab];
                quaternion rotation = quaternion.LookRotationSafe(
                    math.normalizesafe(targetPosition - origin),
                    math.up());

                float scale = transformLookup.HasComponent(prefab)
                    ? transformLookup[prefab].Scale
                    : 1f;

                Entity projectile = ecb.Instantiate(prefab);
                ecb.SetComponent(projectile, LocalTransform.FromPositionRotationScale(origin, rotation, scale));
                ecb.SetComponent(projectile, new ProjectileTarget { Target = target });
                ecb.SetComponent(projectile, new ProjectileLifetime { Remaining = config.Lifetime });

                weapon.ValueRW.Cooldown = weapon.ValueRO.FireInterval;
            }
        }
    }
}
