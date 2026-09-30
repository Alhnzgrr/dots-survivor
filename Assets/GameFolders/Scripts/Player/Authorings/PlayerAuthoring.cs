using ECSSurvivor.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSSurvivor.Player
{
    public class PlayerAuthoring : MonoBehaviour
    {
        [SerializeField] private PlayerData playerData;

        private class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                if (authoring.playerData == null)
                {
                    return;
                }

                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                float maxHealth = math.max(1f, authoring.playerData.MaxHealth);

                AddComponent<PlayerTag>(entity);
                AddComponent<MoveInput>(entity);
                AddComponent(entity, new PlayerStats
                {
                    MoveSpeed = math.max(0.1f, authoring.playerData.MoveSpeed)
                });
                AddComponent(entity, new Health
                {
                    Current = maxHealth,
                    Max = maxHealth
                });
                AddComponent(entity, new Invulnerability
                {
                    Remaining = 0f,
                    Duration = math.max(0f, authoring.playerData.HitInvulnerability)
                });
                AddComponent<Dead>(entity);
                SetComponentEnabled<Dead>(entity, false);
            }
        }
    }
}
