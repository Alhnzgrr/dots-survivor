using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSSurvivor.Combat
{
    public class PlayerWeaponAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [Min(0.01f)]
        [SerializeField] private float fireInterval = 0.35f;
        [Min(1f)]
        [SerializeField] private float range = 14f;

        private class Baker : Baker<PlayerWeaponAuthoring>
        {
            public override void Bake(PlayerWeaponAuthoring authoring)
            {
                if (authoring.projectilePrefab == null)
                {
                    return;
                }

                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlayerWeapon
                {
                    ProjectilePrefab = GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic),
                    FireInterval = math.max(0.01f, authoring.fireInterval),
                    Cooldown = 0f,
                    // A finite range is required, not optional: it is what bounds
                    // the spatial query to a handful of cells. An unlimited range
                    // would mean walking the whole grid to find one target.
                    Range = math.max(1f, authoring.range)
                });
            }
        }
    }
}
