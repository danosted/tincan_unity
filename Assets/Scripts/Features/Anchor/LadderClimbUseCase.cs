#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// Inside the predicted movement step, on the server and the owner alike: a body on a dropped anchor's rope ladder
    /// climbs it (<see cref="LadderProcessor"/>). The ship holds still while its anchor is down, so every peer sees the
    /// ladder in the same place and the climb predicts like walking. Reaching the top is <see cref="LadderLandingUseCase"/>'s.
    /// </summary>
    public sealed class LadderClimbUseCase : IHumanoidMotionContributor
    {
        private readonly IActorRegistry _actors;

        public LadderClimbUseCase(IActorRegistry actors) => _actors = actors;

        public void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step)
        {
            var feet = character.Movement.Transform.position;
            foreach (var anchor in _actors.GetActors<IAnchor>())
            {
                if (!anchor.IsSimulating || !anchor.IsDown || anchor.Ladder is not { } ladder) continue;
                if (!LadderProcessor.TryClimb(feet, ladder, input.MovementDirection.z, input.IsJumping, step.IsGrounded, out var velocity)) continue;

                step.Velocity = velocity;
                return;
            }
        }
    }
}
