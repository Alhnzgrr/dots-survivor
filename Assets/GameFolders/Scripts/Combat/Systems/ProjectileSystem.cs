using ECSSurvivor.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSSurvivor.Combat
{
    /// Projectiles are the other population that scales with the wave, so this runs
    /// wide as well. Each one touches only its own transform and lifetime; the two
    /// things it shares with other threads - the damage queue and the command
    /// buffer - are both written through parallel writers rather than locked.
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [BurstCompile]
    public partial struct ProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialHashMap>();
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SpatialHashMap hash = SystemAPI.GetSingleton<SpatialHashMap>();

            EntityCommandBuffer.ParallelWriter ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            state.Dependency = new ProjectileJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                TargetPositions = hash.Positions,
                Damage = SystemAPI.GetSingleton<DamageQueue>().Value.AsParallelWriter(),
                Ecb = ecb
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ProjectileJob : IJobEntity
    {
        public float DeltaTime;

        [ReadOnly] public NativeParallelHashMap<Entity, float3> TargetPositions;
        public NativeQueue<DamageEvent>.ParallelWriter Damage;
        public EntityCommandBuffer.ParallelWriter Ecb;

        private void Execute(
            [ChunkIndexInQuery] int chunkIndex,
            Entity entity,
            ref LocalTransform transform,
            ref ProjectileLifetime lifetime,
            in ProjectileConfig config,
            in ProjectileTarget target)
        {
            lifetime.Remaining -= DeltaTime;
            if (lifetime.Remaining <= 0f)
            {
                Ecb.DestroyEntity(chunkIndex, entity);
                return;
            }

            // A target missing from this frame's position map is a target that no
            // longer exists. Retiring the shot beats letting it fly to where the
            // enemy used to be.
            if (!TargetPositions.TryGetValue(target.Target, out float3 targetPosition))
            {
                Ecb.DestroyEntity(chunkIndex, entity);
                return;
            }

            float3 toTarget = targetPosition - transform.Position;
            float distanceSq = math.lengthsq(toTarget);
            float step = config.Speed * DeltaTime;
            float reach = math.max(0.01f, config.HitRadius) + step;

            // Compared against the distance it can cover this frame, so a fast
            // projectile cannot tunnel past a small enemy between two frames.
            if (distanceSq <= reach * reach)
            {
                Damage.Enqueue(new DamageEvent
                {
                    Target = target.Target,
                    Amount = config.Damage
                });

                Ecb.DestroyEntity(chunkIndex, entity);
                return;
            }

            float3 direction = toTarget * math.rsqrt(math.max(distanceSq, math.EPSILON));
            transform.Position += direction * step;
            transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
        }
    }
}
