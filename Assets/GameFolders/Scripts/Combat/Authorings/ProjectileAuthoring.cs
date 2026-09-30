using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSSurvivor.Combat
{
    public class ProjectileAuthoring : MonoBehaviour
    {
        [Min(0.1f)]
        [SerializeField] private float speed = 18f;
        [Min(0.1f)]
        [SerializeField] private float damage = 2f;
        [Min(0.05f)]
        [SerializeField] private float hitRadius = 0.4f;
        [Min(0.1f)]
        [SerializeField] private float lifetime = 4f;

        private class Baker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                float lifetime = math.max(0.1f, authoring.lifetime);

                AddComponent(entity, new ProjectileConfig
                {
                    Speed = math.max(0.1f, authoring.speed),
                    Damage = math.max(0.1f, authoring.damage),
                    HitRadius = math.max(0.05f, authoring.hitRadius),
                    Lifetime = lifetime
                });

                AddComponent(entity, new ProjectileLifetime { Remaining = lifetime });
                AddComponent(entity, new ProjectileTarget { Target = Entity.Null });
            }
        }
    }
}
