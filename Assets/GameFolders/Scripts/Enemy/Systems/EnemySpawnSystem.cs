using ECSSurvivor.Core;
using ECSSurvivor.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSSurvivor.Enemy
{
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct EnemySpawnSystem : ISystem
    {
        private EntityQuery _aliveQuery;

        public void OnCreate(ref SystemState state)
        {
            _aliveQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, WaveOwner>()
                .Build();

            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<SpatialHashMap>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TryGetPlayerPosition(ref state, out float3 playerPosition))
            {
                return;
            }

            SpatialHashMap hash = SystemAPI.GetSingleton<SpatialHashMap>();
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            NativeParallelHashMap<Entity, int> aliveBySpawner = CountAliveBySpawner(ref state);

            // Enemies queued through the command buffer this frame are not in the
            // spatial hash yet, so a second wave entry could land on top of the
            // first. They are tracked here for the separation test and thrown away
            // at the end of the frame.
            NativeList<float3> spawnedThisFrame = new NativeList<float3>(16, Allocator.Temp);

            ComponentLookup<EnemySpawnSettings> settingsLookup =
                SystemAPI.GetComponentLookup<EnemySpawnSettings>(true);

            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach ((RefRO<EnemySpawner> spawner,
                      DynamicBuffer<WaveElement> waves,
                      RefRW<EnemySpawnState> spawnState,
                      Entity spawnerEntity)
                     in SystemAPI.Query<RefRO<EnemySpawner>, DynamicBuffer<WaveElement>, RefRW<EnemySpawnState>>()
                         .WithEntityAccess())
            {
                Entity prefab = spawner.ValueRO.EnemyPrefab;
                if (waves.Length == 0 || !settingsLookup.HasComponent(prefab))
                {
                    continue;
                }

                int waveIndex = spawnState.ValueRO.WaveIndex;
                if (waveIndex >= waves.Length)
                {
                    continue;
                }

                WaveElement wave = waves[waveIndex];

                if (spawnState.ValueRO.SpawnedInWave >= wave.Count)
                {
                    aliveBySpawner.TryGetValue(spawnerEntity, out int alive);
                    if (alive == 0)
                    {
                        spawnState.ValueRW.WaveIndex = waveIndex + 1;
                        spawnState.ValueRW.SpawnedInWave = 0;
                        spawnState.ValueRW.Cooldown = 0f;
                    }

                    continue;
                }

                spawnState.ValueRW.Cooldown -= deltaTime;
                if (spawnState.ValueRO.Cooldown > 0f)
                {
                    continue;
                }

                EnemySpawnSettings settings = settingsLookup[prefab];
                Unity.Mathematics.Random random = spawnState.ValueRO.Random;

                float3 spawnPosition = FindSpawnPosition(
                    ref random,
                    in hash,
                    in spawnedThisFrame,
                    playerPosition,
                    settings);

                Entity enemy = ecb.Instantiate(prefab);
                ecb.SetComponent(enemy, LocalTransform.FromPosition(spawnPosition));
                ecb.AddComponent(enemy, new WaveOwner { Spawner = spawnerEntity });

                spawnedThisFrame.Add(spawnPosition);
                spawnState.ValueRW.SpawnedInWave += 1;
                spawnState.ValueRW.Cooldown = wave.Interval;
                spawnState.ValueRW.Random = random;
            }

            spawnedThisFrame.Dispose();
            aliveBySpawner.Dispose();
        }

        private static float3 FindSpawnPosition(
            ref Unity.Mathematics.Random random,
            in SpatialHashMap hash,
            in NativeList<float3> spawnedThisFrame,
            float3 playerPosition,
            EnemySpawnSettings settings)
        {
            float minDistance = math.max(0f, settings.SpawnDistanceMin);
            float maxDistance = math.max(minDistance, settings.SpawnDistanceMax);
            float separation = math.max(0f, settings.SpawnMinSeparation);

            float3 candidate = default;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float2 bearing = random.NextFloat2Direction();
                float distance = maxDistance > minDistance
                    ? random.NextFloat(minDistance, maxDistance)
                    : minDistance;

                candidate = playerPosition + new float3(bearing.x, 0f, bearing.y) * distance;
                candidate.y = playerPosition.y;

                if (separation <= 0f)
                {
                    return candidate;
                }

                if (SpatialHashQuery.IsOccupied(hash.Map, hash.CellSize, candidate, separation))
                {
                    continue;
                }

                if (!OverlapsPending(in spawnedThisFrame, candidate, separation))
                {
                    return candidate;
                }
            }

            // Eight rejected candidates means the ring is genuinely crowded.
            // Spawning on the last one is better than skipping the tick and
            // letting the wave stall behind a wall of its own enemies.
            return candidate;
        }

        private static bool OverlapsPending(in NativeList<float3> pending, float3 candidate, float separation)
        {
            float separationSq = separation * separation;

            for (int i = 0; i < pending.Length; i++)
            {
                if (math.lengthsq(pending[i] - candidate) < separationSq)
                {
                    return true;
                }
            }

            return false;
        }

        /// One pass over live enemies per frame instead of one pass per spawner
        /// per frame. With a single spawner the difference is nothing; the point is
        /// that adding a second spawner no longer doubles the cost.
        private NativeParallelHashMap<Entity, int> CountAliveBySpawner(ref SystemState state)
        {
            int capacity = math.max(1, _aliveQuery.CalculateEntityCount());
            NativeParallelHashMap<Entity, int> counts =
                new NativeParallelHashMap<Entity, int>(capacity, Allocator.Temp);

            foreach (RefRO<WaveOwner> owner in
                     SystemAPI.Query<RefRO<WaveOwner>>().WithAll<EnemyTag>().WithNone<Dead>())
            {
                Entity spawner = owner.ValueRO.Spawner;
                counts.TryGetValue(spawner, out int current);
                counts[spawner] = current + 1;
            }

            return counts;
        }

        private bool TryGetPlayerPosition(ref SystemState state, out float3 position)
        {
            foreach (RefRO<LocalTransform> transform in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>().WithNone<Dead>())
            {
                position = transform.ValueRO.Position;
                return true;
            }

            position = default;
            return false;
        }
    }
}
