#nullable enable
namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Changes a humanoid's velocity for one simulated movement step, for a system that moves the body by more than its
    /// own legs (a grappling hook reeling it in). Called on the server and on the predicting owner, after the body's own
    /// momentum, jump and gravity and before the move, so the change is part of the predicted simulation. It also runs
    /// when the owner replays unacknowledged inputs after a correction (<see cref="HumanoidMotionStep.IsReplay"/>): a
    /// replay must reproduce the motion without changing the contributor's state. Register with
    /// <c>.As&lt;IHumanoidMotionContributor&gt;()</c>.
    /// </summary>
    public interface IHumanoidMotionContributor
    {
        void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step);
    }
}
