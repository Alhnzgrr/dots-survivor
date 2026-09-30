using ECSSurvivor.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSSurvivor.Player
{
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [BurstCompile]
    public partial struct PlayerMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new PlayerMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(PlayerTag))]
    public partial struct PlayerMoveJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, in MoveInput input, in PlayerStats stats)
        {
            float2 direction = input.Value;
            if (math.lengthsq(direction) <= math.EPSILON)
            {
                return;
            }

            float3 delta = new float3(direction.x, 0f, direction.y) * stats.MoveSpeed * DeltaTime;
            transform.Position += delta;

            // Face the direction of travel; the enemies come from every side, so
            // without this the player model reads as sliding rather than moving.
            transform.Rotation = quaternion.LookRotationSafe(math.normalizesafe(delta), math.up());
        }
    }
}
