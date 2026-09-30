using ECSSurvivor.Core;
using ECSSurvivor.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSSurvivor.Enemy
{
    /// Enemies hurt the player by standing on them. Rather than every enemy testing
    /// its own distance to the player, the player asks the grid which enemies are
    /// near it - one query per frame instead of one per enemy.
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct EnemyContactDamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<SpatialHashMap>();
            state.RequireForUpdate<DamageQueue>();
        }

        public void OnUpdate(ref SystemState state)
        {
            SpatialHashMap hash = SystemAPI.GetSingleton<SpatialHashMap>();
            if (!hash.Map.IsCreated || hash.Map.IsEmpty)
            {
                return;
            }

            NativeQueue<DamageEvent> damage = SystemAPI.GetSingleton<DamageQueue>().Value;
            ComponentLookup<EnemyContactDamage> contactLookup =
                SystemAPI.GetComponentLookup<EnemyContactDamage>(true);

            foreach ((RefRO<LocalTransform> transform, RefRW<Invulnerability> invulnerability, Entity player)
                     in SystemAPI.Query<RefRO<LocalTransform>, RefRW<Invulnerability>>()
                         .WithAll<PlayerTag>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                if (invulnerability.ValueRO.Remaining > 0f)
                {
                    continue;
                }

                if (!TryFindToucher(
                        in hash,
                        in contactLookup,
                        transform.ValueRO.Position,
                        out float contactDamage))
                {
                    continue;
                }

                damage.Enqueue(new DamageEvent
                {
                    Target = player,
                    Amount = contactDamage
                });

                // One hit opens the grace window, so a crowd of twenty deals one
                // hit, not twenty. Without this the player dies the instant a wave
                // closes in, and the number on the enemy asset becomes meaningless.
                invulnerability.ValueRW.Remaining = invulnerability.ValueRO.Duration;
            }
        }

        private static bool TryFindToucher(
            in SpatialHashMap hash,
            in ComponentLookup<EnemyContactDamage> contactLookup,
            float3 playerPosition,
            out float damage)
        {
            damage = 0f;

            // The widest contact radius any enemy could have bounds the search.
            const float maxContactRadius = 4f;

            if (!SpatialHashQuery.TryFindNearest(
                    hash.Map,
                    hash.CellSize,
                    playerPosition,
                    maxContactRadius,
                    out Entity nearest,
                    out float3 nearestPosition))
            {
                return false;
            }

            if (!contactLookup.HasComponent(nearest))
            {
                return false;
            }

            EnemyContactDamage contact = contactLookup[nearest];
            float radius = math.max(0.05f, contact.Radius);

            if (math.lengthsq(nearestPosition - playerPosition) > radius * radius)
            {
                return false;
            }

            damage = contact.Amount;
            return damage > 0f;
        }
    }
}
