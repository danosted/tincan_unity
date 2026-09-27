#nullable enable
using System;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Features.Targeting
{
    /// <summary>
    /// A humanoid as a targeter. Everything comes from state owner and server share: the simulated body pose (it
    /// follows the replicated input's yaw) and the input's look pitch, which the server gets from the input stream.
    /// Eye height matches the interaction ray (InteractorControllerView); the orbit height is the look view's, so
    /// CameraAim rebuilds the same view centre the owner's camera shows.
    /// </summary>
    public readonly struct HumanoidTargeter : ITargeter
    {
        public const float EyeHeight = 1.5f;

        private readonly IHumanoidCharacterView _character;

        public HumanoidTargeter(IHumanoidCharacterView character) => _character = character;

        public Guid Id => _character.Id;

        public bool TryGetOrigin(out TargetingOrigin origin)
        {
            var body = _character.Movement?.Transform;
            if (body == null)
            {
                origin = default;
                return false;
            }

            float orbitHeight = _character.Look != null ? _character.Look.OrbitHeight : 0f;
            origin = new TargetingOrigin(body.position, body.rotation, EyeHeight, _character.InputState.LookPitch, orbitHeight);
            return true;
        }
    }
}
