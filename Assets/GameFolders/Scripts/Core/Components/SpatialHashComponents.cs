using Unity.Entities;
using Unity.Mathematics;

namespace ECSSurvivor.Core
{
    public struct SpatialHashSettings : IComponentData
    {
        public float CellSize;
    }

    public struct SpatialEntry
    {
        public Entity Entity;
        public float3 Position;
    }

    public static class SpatialHash
    {
        public const float MinCellSize = 0.5f;

        public static int3 ToCell(float3 position, float cellSize)
        {
            return (int3)math.floor(position / cellSize);
        }

        public static int HashCell(int3 cell)
        {
            return (int)math.hash(cell);
        }

        public static int Hash(float3 position, float cellSize)
        {
            return HashCell(ToCell(position, cellSize));
        }

        public static int CellRadius(float worldRadius, float cellSize)
        {
            return (int)math.ceil(worldRadius / math.max(MinCellSize, cellSize));
        }
    }
}
