using Unity.Entities;

namespace ECSSurvivor.Combat
{
    public struct PlayerWeapon : IComponentData
    {
        public Entity ProjectilePrefab;
        public float FireInterval;
        public float Cooldown;
        public float Range;
    }

    public struct ProjectileConfig : IComponentData
    {
        public float Speed;
        public float Damage;
        public float HitRadius;
        public float Lifetime;
    }

    /// Projectiles home on the entity they were fired at. Storing the target rather
    /// than a direction means a shot cannot be dodged by an enemy that keeps
    /// walking, which is the behaviour a survivor-like wants from auto-fire.
    public struct ProjectileTarget : IComponentData
    {
        public Entity Target;
    }

    public struct ProjectileLifetime : IComponentData
    {
        public float Remaining;
    }
}
