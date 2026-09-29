#nullable enable
using System;
using TinCan.Core.Domain.Targeting;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// A humanoid as a targeter. Everything comes from state owner and server share: the simulated body pose (it
    /// follows the replicated input's yaw) and the input's look pitch, which the server gets from the input stream.
    /// The eye height is the character's own (<see cref="IHumanoidMovementView.EyeHeight"/>, tuned on the player prefab,
    /// measured from the root, which is the capsule centre); the orbit height is the look view's, so CameraAim rebuilds
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

            float orbitHeight = _character.Look != null ? _character.Look.OrbitHeight : 0f;
            origin = new TargetingOrigin(body.position, body.rotation, movement.EyeHeight, _character.InputState.LookPitch, orbitHeight);
            return true;
        }
    }
}
