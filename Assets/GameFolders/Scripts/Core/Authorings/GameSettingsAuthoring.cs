using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSSurvivor.Core
{
    /// Drop this on one object in the scene to tune the broadphase.
    ///
    /// Cell size is the only dial that matters and it is a genuine trade-off:
    /// too small and a query walks many nearly empty cells, too large and each
    /// cell holds enough enemies that the scan inside it costs what the grid was
    /// meant to save. A good starting point is a little above the widest query
    /// radius in the game - here, the weapon range divided by three or four.
    public class GameSettingsAuthoring : MonoBehaviour
    {
        [Min(SpatialHash.MinCellSize)]
        [SerializeField] private float spatialCellSize = 4f;

        private class Baker : Baker<GameSettingsAuthoring>
        {
            public override void Bake(GameSettingsAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpatialHashSettings
                {
                    CellSize = math.max(SpatialHash.MinCellSize, authoring.spatialCellSize)
                });
            }
        }
    }
}
