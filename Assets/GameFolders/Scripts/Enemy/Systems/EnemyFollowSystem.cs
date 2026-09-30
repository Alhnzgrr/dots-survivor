using ECSSurvivor.Core;
using ECSSurvivor.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSSurvivor.Enemy
{
    /// The one system that genuinely scales with enemy count, so it is the one
    /// that has to run wide: every enemy reads a single shared player position and
    /// writes only its own transform, which is the ideal shape for ScheduleParallel.
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [BurstCompile]
    public partial struct EnemyFollowSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<EnemyTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TryGetPlayerPosition(ref state, out float3 playerPosition))
            {
                return;
            }

            state.Dependency = new EnemyFollowJob
            {
                PlayerPosition = playerPosition,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }

        private bool TryGetPlayerPosition(ref SystemState state, out float3 position)
        {
            foreach (RefRO<LocalTransform> transform in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>().WithNone<Dead>())
            {
                position = transform.ValueRO.Position;
                return true;
            }

            position = default;
            return false;
        }
    }

    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    [WithNone(typeof(Dead))]
    public partial struct EnemyFollowJob : IJobEntity
    {
        public float3 PlayerPosition;
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, in EnemyMoveData moveData)
        {
            float3 toPlayer = PlayerPosition - transform.Position;
            toPlayer.y = 0f;

            float distanceSq = math.lengthsq(toPlayer);
            float stopDistance = math.max(0f, moveData.FollowStopDistance);
            float startDistance = math.max(stopDistance, moveData.FollowStartDistance);

            // Inside the stop ring it holds position, outside the start ring it has
            // not noticed the player yet. Both are squared comparisons so the job
            // never pays for a square root it does not need.
            if (distanceSq <= stopDistance * stopDistance ||
                distanceSq > startDistance * startDistance)
            {
                return;
            }

            float3 direction = toPlayer * math.rsqrt(math.max(distanceSq, math.EPSILON));
            transform.Position += direction * moveData.MoveSpeed * DeltaTime;
            transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
        }
    }
}
