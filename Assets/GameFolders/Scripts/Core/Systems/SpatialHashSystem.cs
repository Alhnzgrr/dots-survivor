using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ECSSurvivor.Enemy;

namespace ECSSurvivor.Core
{
    /// Holds the enemy acceleration structure for the current frame.
    /// The map is rebuilt from scratch every frame rather than incrementally
    /// maintained: enemies move every frame anyway, so a rebuild is cheaper than
    /// tracking cell transitions, and it cannot drift out of sync with the world.
    public struct SpatialHashMap : IComponentData
    {
        public NativeParallelMultiHashMap<int, SpatialEntry> Map;

        /// Entity to position for the same set, filled by the same pass.
        /// A homing projectile needs its target's position while writing its own
        /// transform; going through a ComponentLookup&lt;LocalTransform&gt; would
        /// alias the transform its own query already holds for writing, and the
        /// job safety system rejects that. Reading a position the grid pass
        /// already gathered sidesteps the conflict instead of suppressing it,
        /// and a missing key doubles as "the target is gone".
        public NativeParallelHashMap<Entity, float3> Positions;

        public float CellSize;
    }

    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct SpatialHashSystem : ISystem
    {
        private EntityQuery _enemyQuery;

        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform>()
                .Build();

            Entity singleton = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(singleton, "SpatialHashMap");
            state.EntityManager.AddComponentData(singleton, new SpatialHashMap
            {
                Map = new NativeParallelMultiHashMap<int, SpatialEntry>(1024, Allocator.Persistent),
                Positions = new NativeParallelHashMap<Entity, float3>(1024, Allocator.Persistent),
                CellSize = 4f
            });
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out SpatialHashMap hash))
            {
                return;
            }

            if (hash.Map.IsCreated)
            {
                hash.Map.Dispose();
            }

            if (hash.Positions.IsCreated)
            {
                hash.Positions.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            SpatialHashMap hash = SystemAPI.GetSingleton<SpatialHashMap>();

            float cellSize = math.max(SpatialHash.MinCellSize, hash.CellSize);
            if (SystemAPI.TryGetSingleton(out SpatialHashSettings settings))
            {
                cellSize = math.max(SpatialHash.MinCellSize, settings.CellSize);
            }

            int enemyCount = _enemyQuery.CalculateEntityCount();

            hash.Map.Clear();
            hash.Positions.Clear();

            if (hash.Map.Capacity < enemyCount)
            {
                // Grow with headroom so a steadily rising wave count does not
                // reallocate on every single frame.
                hash.Map.Capacity = math.max(1024, enemyCount * 2);
            }

            if (hash.Positions.Capacity < enemyCount)
            {
                hash.Positions.Capacity = math.max(1024, enemyCount * 2);
            }

            hash.CellSize = cellSize;
            SystemAPI.SetSingleton(hash);

            if (enemyCount == 0)
            {
                return;
            }

            state.Dependency = new BuildSpatialHashJob
            {
                CellSize = cellSize,
                Writer = hash.Map.AsParallelWriter(),
                Positions = hash.Positions.AsParallelWriter()
            }.ScheduleParallel(_enemyQuery, state.Dependency);

            // Consumers read the map from the main thread only, so the build is
            // completed here. That keeps the fill parallel while making a read
            // race structurally impossible - the alternative, handing every
            // consumer a JobHandle to combine, buys little for a map this size
            // and is far easier to get wrong.
            state.Dependency.Complete();
        }
    }

    [BurstCompile]
    public partial struct BuildSpatialHashJob : IJobEntity
    {
        public float CellSize;
        public NativeParallelMultiHashMap<int, SpatialEntry>.ParallelWriter Writer;
        public NativeParallelHashMap<Entity, float3>.ParallelWriter Positions;

        private void Execute(Entity entity, in LocalTransform transform)
        {
            Writer.Add(SpatialHash.Hash(transform.Position, CellSize), new SpatialEntry
            {
                Entity = entity,
                Position = transform.Position
            });

            Positions.TryAdd(entity, transform.Position);
        }
    }

    public static class SpatialHashQuery
    {
        /// Returns the nearest entry to <paramref name="origin"/> within
        /// <paramref name="radius"/>, or false when the neighbourhood is empty.
        /// Only the cells the radius actually touches are visited, so cost scales
        /// with local density instead of total enemy count.
        public static bool TryFindNearest(
            in NativeParallelMultiHashMap<int, SpatialEntry> map,
            float cellSize,
            float3 origin,
            float radius,
            out Entity nearest,
            out float3 nearestPosition)
        {
            nearest = Entity.Null;
            nearestPosition = default;

            if (!map.IsCreated || map.IsEmpty)
            {
                return false;
            }

            float radiusSq = radius * radius;
            float bestDistSq = float.MaxValue;

            int3 centre = SpatialHash.ToCell(origin, cellSize);
            int span = SpatialHash.CellRadius(radius, cellSize);

            for (int x = -span; x <= span; x++)
            {
                for (int z = -span; z <= span; z++)
                {
                    int key = SpatialHash.HashCell(centre + new int3(x, 0, z));

                    if (!map.TryGetFirstValue(key, out SpatialEntry entry, out NativeParallelMultiHashMapIterator<int> it))
                    {
                        continue;
                    }

                    do
                    {
                        float distSq = math.lengthsq(entry.Position - origin);
                        if (distSq <= radiusSq && distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            nearest = entry.Entity;
                            nearestPosition = entry.Position;
                        }
                    }
                    while (map.TryGetNextValue(out entry, ref it));
                }
            }

            return nearest != Entity.Null;
        }

        /// True when any entry sits closer than <paramref name="separation"/> to
        /// <paramref name="position"/>. Used by the spawner to avoid stacking
        /// enemies on top of each other without scanning the whole world.
        public static bool IsOccupied(
            in NativeParallelMultiHashMap<int, SpatialEntry> map,
            float cellSize,
            float3 position,
            float separation)
        {
            if (!map.IsCreated || map.IsEmpty || separation <= 0f)
            {
                return false;
            }

            float separationSq = separation * separation;
            int3 centre = SpatialHash.ToCell(position, cellSize);
            int span = SpatialHash.CellRadius(separation, cellSize);

            for (int x = -span; x <= span; x++)
            {
                for (int z = -span; z <= span; z++)
                {
                    int key = SpatialHash.HashCell(centre + new int3(x, 0, z));

                    if (!map.TryGetFirstValue(key, out SpatialEntry entry, out NativeParallelMultiHashMapIterator<int> it))
                    {
                        continue;
                    }

                    do
                    {
                        if (math.lengthsq(entry.Position - position) < separationSq)
                        {
                            return true;
                        }
                    }
                    while (map.TryGetNextValue(out entry, ref it));
                }
            }

            return false;
        }
    }
}
