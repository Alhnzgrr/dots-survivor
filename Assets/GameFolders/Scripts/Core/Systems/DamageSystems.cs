using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSSurvivor.Core
{
    /// Every damage producer writes here instead of touching Health directly.
    /// Two projectiles landing on one enemy in the same frame is the normal case,
    /// and a parallel job cannot read-modify-write a shared component without
    /// losing one of them, so the writes are queued and applied on one thread.
    public struct DamageQueue : IComponentData
    {
        public NativeQueue<DamageEvent> Value;
    }

    [UpdateInGroup(typeof(ResolutionSystemGroup))]
    [UpdateBefore(typeof(DeathSystem))]
    public partial struct ApplyDamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            Entity singleton = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(singleton, "DamageQueue");
            state.EntityManager.AddComponentData(singleton, new DamageQueue
            {
                Value = new NativeQueue<DamageEvent>(Allocator.Persistent)
            });
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out DamageQueue queue))
            {
                return;
            }

            if (queue.Value.IsCreated)
            {
                queue.Value.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            DamageQueue queue = SystemAPI.GetSingleton<DamageQueue>();
            if (!queue.Value.IsCreated)
            {
                return;
            }

            // Every producer this frame ran as a job; drain only once they are done.
            state.Dependency.Complete();

            if (queue.Value.Count == 0)
            {
                return;
            }

            ComponentLookup<Health> healthLookup = SystemAPI.GetComponentLookup<Health>();
            ComponentLookup<Dead> deadLookup = SystemAPI.GetComponentLookup<Dead>();

            while (queue.Value.TryDequeue(out DamageEvent damage))
            {
                if (!healthLookup.HasComponent(damage.Target))
                {
                    continue;
                }

                Health health = healthLookup[damage.Target];
                if (health.Current <= 0f)
                {
                    continue;
                }

                health.Current = math.max(0f, health.Current - damage.Amount);
                healthLookup[damage.Target] = health;

                if (health.Current <= 0f && deadLookup.HasComponent(damage.Target))
                {
                    deadLookup.SetComponentEnabled(damage.Target, true);
                }
            }
        }
    }

    /// Death is a separate pass so that anything wanting to react to it - a score
    /// tick, a pickup drop, a sound - has one place to hook in, and so an entity
    /// is never destroyed halfway through the frame that damaged it.
    [UpdateInGroup(typeof(ResolutionSystemGroup))]
    public partial struct DeathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach ((RefRO<Health> health, Entity entity)
                     in SystemAPI.Query<RefRO<Health>>().WithAll<Dead>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
        }
    }

    /// Ticks down the grace window a damaged entity gets, so contact damage does
    /// not drain a health bar at the simulation frame rate.
    [UpdateInGroup(typeof(ResolutionSystemGroup))]
    [BurstCompile]
    public partial struct InvulnerabilitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (RefRW<Invulnerability> invulnerability in SystemAPI.Query<RefRW<Invulnerability>>())
            {
                if (invulnerability.ValueRO.Remaining <= 0f)
                {
                    continue;
                }

                invulnerability.ValueRW.Remaining = math.max(0f, invulnerability.ValueRO.Remaining - deltaTime);
            }
        }
    }
}
