#nullable enable
using System.Collections.Generic;
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// The designed events ship damage contributes (through <see cref="ShipDamageFeatureInstaller"/>), as code:
    /// versioned and reviewed with the feature they coordinate. Ids are stable (they will be replicated) and unique
    /// across every feature; never reuse one. See .docs/plans/designed-events.md.
    /// </summary>
    public static class ShipDamageDesignedEvents
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
