#nullable enable
using System;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Features.Targeting
{
    /// <summary>
    /// A humanoid as a targeter: its simulated body pose, which follows the replicated input on owner and server
    /// alike. Eye height matches the interaction ray (InteractorControllerView). Pitch joins once it travels in the input.
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

            origin = new TargetingOrigin(body.position, body.rotation, EyeHeight);
            return true;
        }
    }
}
