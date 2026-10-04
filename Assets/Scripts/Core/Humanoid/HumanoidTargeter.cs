#nullable enable
using System;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// A humanoid as a targeter. Everything comes from state owner and server share: the body's position and the input's
    /// look (its yaw, relative to the platform underfoot, and its pitch), which the server gets from the input stream.
    /// It faces where the player looks, not where the body has turned to yet: the body eases toward the look over a few
    /// frames, and a query that followed it would point elsewhere after a quick turn.
    /// The eye height is the character's own (<see cref="IHumanoidMovementView.EyeHeight"/>, tuned on the player prefab,
    /// measured from the root, which is the capsule centre); the aim height is the camera rig's, so CameraAim rebuilds
    /// the same view centre the owner's camera shows.
    /// </summary>
    public readonly struct HumanoidTargeter : ITargeter
    {
        private readonly IHumanoidCharacterView _character;

        public HumanoidTargeter(IHumanoidCharacterView character) => _character = character;

        public Guid Id => _character.Id;

        public bool TryGetOrigin(out TargetingOrigin origin)
        {
            var movement = _character.Movement;
            var body = movement?.Transform;
            if (movement == null || body == null)
            {
                origin = default;
                return false;
            }

            var input = _character.InputState;
            float aimHeight = _character.Look != null ? _character.Look.AimHeight : 0f;
            origin = new TargetingOrigin(body.position, Facing(input.LookRotation, movement.CurrentGround.MovingGroundTransform, body.rotation),
                movement.EyeHeight, input.LookPitch, aimHeight);
            return true;
        }

        /// <summary>
        /// The look's world yaw: the input's yaw is relative to the yaw of the platform underfoot (world yaw when there is
        /// none), as the movement simulation reads it. Before the first input the body's own facing stands in.
        /// </summary>
        public static Quaternion Facing(Quaternion inputLook, Transform? platform, Quaternion bodyRotation)
        {
            bool hasLook = inputLook.x != 0f || inputLook.y != 0f || inputLook.z != 0f || inputLook.w != 0f;
            if (!hasLook) return bodyRotation;

            float platformYaw = platform != null ? platform.eulerAngles.y : 0f;
            return Quaternion.Euler(0f, platformYaw + inputLook.eulerAngles.y, 0f);
        }
    }
}
