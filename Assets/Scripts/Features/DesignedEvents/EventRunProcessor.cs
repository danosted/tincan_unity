#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Pure phase machine: given the current phase's state, decide whether the event stays, moves on, or ends.
    /// A condition phase ends when the condition holds and fails on timeout; a timed phase ends when its time is up.
    /// The condition wins over the timeout on the same tick.
    /// </summary>
    public sealed class EventRunProcessor
    {
        public EventStep Advance(bool hasCondition, bool conditionMet, int ticksInPhase, int phaseTicks, bool isLastPhase)
        {
            bool timeUp = ticksInPhase >= phaseTicks;
            return (hasCondition, conditionMet, timeUp) switch
            {
                (true, true, _) => isLastPhase ? EventStep.Succeed : EventStep.NextPhase,
                (true, false, true) => EventStep.Fail,
                (false, _, true) => isLastPhase ? EventStep.Succeed : EventStep.NextPhase,
                _ => EventStep.Stay
            };
        }
    }
}
