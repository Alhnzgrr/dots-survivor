using Unity.Entities;
using Unity.Mathematics;

namespace ECSSurvivor.Player
{
    public struct PlayerTag : IComponentData { }

    public struct PlayerStats : IComponentData
    {
        public float MoveSpeed;
    }

    /// Raw stick/keyboard direction, already clamped to the unit disc. Kept apart
    /// from the movement step so input can be replaced (replay, AI, a test) without
    /// touching how movement integrates.
    public struct MoveInput : IComponentData
    {
        public float2 Value;
    }
}
