using ECSSurvivor.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSSurvivor.Enemy
{
    public class EnemyAuthoring : MonoBehaviour
    {
        [SerializeField] private EnemyData enemyData;

        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                if (authoring.enemyData == null)
                {
                    return;
                }

                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                EnemyData data = authoring.enemyData;
                float maxHealth = math.max(1f, data.MaxHealth);

                // Baked, not added at spawn time: a prefab entity carries the
                // Prefab tag and is excluded from queries anyway, and baking keeps
                // the archetype identical for every enemy so no structural change
                // is needed on the frame one spawns.
                AddComponent<EnemyTag>(entity);

                AddComponent(entity, new EnemySpawnSettings
                {
                    SpawnDistanceMin = math.max(0f, data.SpawnDistanceMin),
                    SpawnDistanceMax = math.max(data.SpawnDistanceMin, data.SpawnDistanceMax),
                    SpawnMinSeparation = math.max(0f, data.SpawnMinSeparation)
                });

                AddComponent(entity, new EnemyMoveData
                {
                    FollowStopDistance = math.max(0f, data.FollowStopDistance),
                    FollowStartDistance = math.max(data.FollowStopDistance, data.FollowStartDistance),
                    MoveSpeed = math.max(0f, data.MoveSpeed)
                });

                AddComponent(entity, new EnemyContactDamage
                {
                    Amount = math.max(0f, data.ContactDamage),
                    Radius = math.max(0.05f, data.ContactRadius)
                });

                AddComponent(entity, new Health
                {
                    Current = maxHealth,
                    Max = maxHealth
                });

                AddComponent<Dead>(entity);
                SetComponentEnabled<Dead>(entity, false);
            }
        }
    }
}
