using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSSurvivor.Enemy
{
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private EnemySpawnData spawnData;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private uint randomSeed = 1234u;

        private class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                if (authoring.enemyPrefab == null)
                {
                    return;
                }

                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new EnemySpawner
                {
                    EnemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic)
                });

                // Seeded once at bake time and carried in the state from then on.
                // The old code rebuilt a Random from a stored seed every frame,
                // which made the first draw of each frame a function of the seed
                // alone - every spawn came out on almost the same bearing.
                AddComponent(entity, new EnemySpawnState
                {
                    Random = Unity.Mathematics.Random.CreateFromIndex(
                        math.max(1u, authoring.randomSeed))
                });

                DynamicBuffer<WaveElement> buffer = AddBuffer<WaveElement>(entity);
                if (authoring.spawnData == null || authoring.spawnData.Waves == null)
                {
                    return;
                }

                foreach (WaveConfig wave in authoring.spawnData.Waves)
                {
                    buffer.Add(new WaveElement
                    {
                        Count = math.max(1, wave.Count),
                        Interval = math.max(0f, wave.Interval)
                    });
                }
            }
        }
    }
}
