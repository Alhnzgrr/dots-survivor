using Unity.Entities;

namespace ECSSurvivor.Core
{
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;
    }

    public struct Dead : IComponentData, IEnableableComponent { }

    public struct Invulnerability : IComponentData
    {
        public float Remaining;
        public float Duration;
    }

    public struct DamageEvent
    {
        public Entity Target;
        public float Amount;
    }
}
