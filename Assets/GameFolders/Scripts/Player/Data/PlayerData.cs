using UnityEngine;

namespace ECSSurvivor.Player
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "ECS-Survivor/Player Data")]
    public class PlayerData : ScriptableObject
    {
        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 6f;
        [Min(1f)]
        [SerializeField] private float maxHealth = 100f;
        [Min(0f)]
        [SerializeField] private float hitInvulnerability = 0.6f;

        public float MoveSpeed => moveSpeed;
        public float MaxHealth => maxHealth;
        public float HitInvulnerability => hitInvulnerability;
    }
}
