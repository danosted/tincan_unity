#nullable enable
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Inside the predicted movement step: holds a player marked overboard (the rules' overboard tag) still under the
    /// clouds, on the server and on their predicting peer alike. Separate from <see cref="OverboardUseCase"/>, which
    /// needs the respawn service (the movement use case itself), so the movement use case can take this one as a motion
    /// contributor without a cycle.
    /// </summary>
    public sealed class OverboardHoldUseCase : IHumanoidMotionContributor
    {
        private readonly RespawnRules _rules;

        public OverboardHoldUseCase(RespawnRules rules) => _rules = rules;

        public void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step)
        {
            if (_rules.OverboardTag != null && character.HasTag(_rules.OverboardTag)) step.Velocity = Vector3.zero;
        }
    }
}
