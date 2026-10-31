#nullable enable
using System.Collections.Generic;
using TinCan.Features.Airship.Damage;
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// The designed events the gas challenge contributes (through <see cref="GasChallengeFeatureInstaller"/>). Ids are
    /// stable and unique across every feature (ship damage owns 1); never reuse one. Plan: .docs/plans/gas-squall-event.md.
    /// </summary>
    public static class GasChallengeDesignedEvents
    {
        public const int GasSquallId = 2;

        /// <summary>
        /// A gas field ahead; whatever the crew fails to steer around goes off and breaks what is near it, fittings first;
        /// then the crew repairs every break. A crew that steered clean is done once the field is behind it. In the
        /// rotation it replaces <see cref="ShipDamageDesignedEvents.HullStress"/>, whose repair phase it ends with.
        /// </summary>
        public static readonly EventDefinition GasSquall =
            new EventDefinition.Builder(GasSquallId, "GasSquall")
                .Describe("A gas field ahead: steer through it; whatever it breaks, the crew repairs before the hull gives way.")
                .Supersedes(ShipDamageDesignedEvents.HullStress.Id)
                .Phase("Warn", p => p
                    .OnEnter(new Announce("Gas ahead: steer clear!"), new SpawnGasField(5, 200f, 150f))
                    .Duration(4f))
                .Phase("Field", p => p
                    .Until(new GasFieldCleared())
                    .Timeout(60f))
                .Phase("Repair", p => p
                    .OnEnter(new Announce("Clear of the gas: check the ship and repair the breaks!"))
                    .Until(new BrokenPartsAtMost(0))
                    .Timeout(90f))
                .OnSuccess(new Announce("Gas squall: the ship holds."), new ClearGasField())
                .OnFailure(new Announce("Gas squall: the ship gave way."), new ClearGasField())
                .Build();

        public static readonly IReadOnlyList<EventDefinition> All = new[] { GasSquall };
    }
}
