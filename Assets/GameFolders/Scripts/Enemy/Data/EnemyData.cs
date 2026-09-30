using UnityEngine;

namespace ECSSurvivor.Enemy
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "ECS-Survivor/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Spawn")]
        [Min(0f)]
        [SerializeField] private float spawnDistanceMin = 12f;
        [Min(0f)]
        [SerializeField] private float spawnDistanceMax = 18f;
        [Min(0f)]
        [SerializeField] private float spawnMinSeparation = 1.2f;

        [Header("Follow")]
        [Min(0f)]
        [SerializeField] private float followStopDistance = 0.9f;
        [Min(0f)]
        [SerializeField] private float followStartDistance = 40f;
        [Min(0f)]
        [SerializeField] private float moveSpeed = 3f;

        [Header("Combat")]
        [Min(1f)]
        [SerializeField] private float maxHealth = 5f;
        [Min(0f)]
        [SerializeField] private float contactDamage = 8f;
        [Min(0.05f)]
        [SerializeField] private float contactRadius = 1.1f;

        public float SpawnDistanceMin => spawnDistanceMin;
        public float SpawnDistanceMax => spawnDistanceMax;
        public float SpawnMinSeparation => spawnMinSeparation;
        public float FollowStopDistance => followStopDistance;
        public float FollowStartDistance => followStartDistance;
        public float MoveSpeed => moveSpeed;
        public float MaxHealth => maxHealth;
        public float ContactDamage => contactDamage;
        public float ContactRadius => contactRadius;
    }
}
