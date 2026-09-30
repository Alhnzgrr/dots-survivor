using ECSSurvivor.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace ECSSurvivor.Player
{
    /// Reads devices directly rather than through a generated actions class, so the
    /// project has no .inputactions asset to keep in sync. SystemBase, not ISystem:
    /// the Input System is managed code and cannot be Burst compiled.
    [UpdateInGroup(typeof(InputSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            float2 direction = ReadDirection();

            // Normalising only past unit length keeps analog sticks analog while
            // stopping a diagonal on the keyboard from being 1.41x faster.
            float lengthSq = math.lengthsq(direction);
            if (lengthSq > 1f)
            {
                direction *= math.rsqrt(lengthSq);
            }

            foreach (RefRW<MoveInput> input in SystemAPI.Query<RefRW<MoveInput>>().WithAll<PlayerTag>())
            {
                input.ValueRW.Value = direction;
            }
        }

        private static float2 ReadDirection()
        {
            float2 direction = float2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    direction.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    direction.x += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    direction.y -= 1f;
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    direction.y += 1f;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                UnityEngine.Vector2 stick = gamepad.leftStick.ReadValue();
                direction += new float2(stick.x, stick.y);
            }

            return direction;
        }
    }
}
