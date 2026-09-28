#nullable enable
using System.Collections.Generic;
using TinCan.Features.Airship.Damage;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Every designed event, as code: versioned and reviewed with the features it coordinates. Ids are stable (they
    /// will be replicated); never reuse one. The director only starts an event whose steps all have handlers in the
    /// loaded profile. See .docs/plans/designed-events.md.
    /// </summary>
    public static class EventCatalog
    {
        /// <summary>Two parts break; the crew repairs both before the timeout, or the hull gives way.</summary>
        public static readonly EventDefinition HullStress =
            new EventDefinition.Builder(1, "HullStress")
                .Describe("Two parts break; the crew must repair them before the hull gives way.")
                .Phase("Groan", p => p
                    .OnEnter(new Announce("Hull stress: the frame is groaning."))
                    .Duration(3f))
                .Phase("Break", p => p
                    .OnEnter(new BreakShipPart(0), new BreakShipPart(2), new Announce("Hull stress: repair the breaks!"))
                    .Until(new BrokenPartsAtMost(0))
                    .Timeout(90f))
                .OnSuccess(new Announce("Hull stress: the ship holds."))
                .OnFailure(new Announce("Hull stress: the hull gave way."))
                .Build();

        public static readonly IReadOnlyList<EventDefinition> All = new[] { HullStress };
    }
}
