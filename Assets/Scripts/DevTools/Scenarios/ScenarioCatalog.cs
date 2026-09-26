#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>A scenario plus the step libraries it needs, registered only when it is the one being run.</summary>
    public sealed class ScenarioEntry
    {
        public Scenario Scenario { get; }
        public Action<IContainerBuilder> RegisterLibraries { get; }

        public ScenarioEntry(Scenario scenario, Action<IContainerBuilder> registerLibraries)
        {
            Scenario = scenario;
            RegisterLibraries = registerLibraries;
        }
    }

    /// <summary>
    /// Every scenario the harness can run by name (<c>-scenario &lt;name&gt;</c>). Scenarios are code, versioned and
    /// reviewed with the feature they test. Add one per feature slice; see .docs/NETWORK_TEST_HARNESS.md.
    /// </summary>
    public static class ScenarioCatalog
    {
        /// <summary>
        /// Proof scenario for the harness itself, on a feature that already works: the server hands the subject a
        /// net and spawns a can in front of it; the subject swings with real input; both sides confirm the catch.
        /// </summary>
        public static readonly ScenarioEntry NetCatch = new(
            new Scenario.Builder("NetCatch")
                .Describe("Subject swings a net at a can spawned in reach; the can disappears and the jerry-can supply grows.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(2f, "settle after spawn")
                    .Do("RecordJerryCanSupply")
                    .Do("GiveSubjectNet")
                    .Wait(0.5f, "net replicates")
                    .Do("SpawnCanAtSubjectNet"))
                .Act(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Carrying.Net")
                    .WaitUntil("CanInNetReach", 10f)
                    .Checkpoint("can-in-reach")
                    .Hold(0.3f, ActionNames.AbilityPrimary)
                    .WaitUntil("NoCanInNetReach", 5f)
                    .Checkpoint("caught"))
                .Assert(s => s
                    .WaitUntil("JerryCanSupplyIncreased", 15f)
                    .Expect("JerryCanSupplyIncreased"))
                .Build(),
            builder => builder.Register<NetCatchScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Tag identity over the network: the subject toggles build mode, which on a client asks the server to add
        /// <c>State.Building</c> by name (AbilityNetworkMediator.RequestTagChangeServerRpc). The server must resolve
        /// the name through the tag registry and replicate the tag. Solo runs cover the local path only.
        /// </summary>
        public static readonly ScenarioEntry TagRequest = new(
            new Scenario.Builder("TagRequest")
                .Describe("Subject enters build mode; the server resolves State.Building by name through the tag registry.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("TagRegistryActive"))
                .Act(s => s
                    .WaitUntil("SubjectLacksTag", 5f, "State.Building")
                    .Tap(ActionNames.BuildMode, 0.3f)
                    .WaitUntil("SubjectHasTag", 3f, "State.Building")
                    .Checkpoint("build-mode-on"))
                .Assert(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Building")
                    .Expect("SubjectHasTag", "State.Building"))
                .Build(),
            _ => { });

        /// <summary>
        /// Items grant and revoke abilities on the right peers. The server hands the subject the net. The subject sees
        /// it replicate, gets GA_SwingNet granted locally (prediction) and swings. The server sees the swing, swaps the
        /// net for a jerry can, and checks that the swing ability and net tag are gone while starting abilities survive.
        /// The subject checks the same on its side, then the server empties the subject's hands.
        /// </summary>
        public static readonly ScenarioEntry EquipCycle = new(
            new Scenario.Builder("EquipCycle")
                .Describe("Equip net -> swing -> swap to jerry can -> unequip; grants and visuals follow on server and owner.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("SubjectHolds", "none")
                    .Do("EquipSubject", "ITEM_CatchingNet"))
                .Act(s => s
                    .WaitUntil("SubjectHolds", 5f, "ITEM_CatchingNet")
                    .WaitUntil("SubjectHasAbility", 3f, "GA_SwingNet")
                    .WaitUntil("SubjectHasTag", 3f, "State.Carrying.Net")
                    .Expect("SubjectVisualShown", "Carry_Net")
                    .Checkpoint("net-held")
                    .Hold(0.3f, ActionNames.AbilityPrimary)
                    .WaitUntil("SubjectHolds", 10f, "ITEM_JerryCan")
                    .WaitUntil("SubjectLacksAbility", 3f, "GA_SwingNet")
                    .WaitUntil("SubjectLacksTag", 3f, "State.Carrying.Net")
                    .WaitUntil("SubjectHasTag", 3f, "State.Carrying.JerryCan")
                    .Expect("SubjectHasAbility", "GA_Sprint")
                    .Expect("SubjectVisualShown", "Carry_JerryCan")
                    .Expect("SubjectVisualHidden", "Carry_Net")
                    .Checkpoint("can-held")
                    .WaitUntil("SubjectHolds", 10f, "none")
                    .WaitUntil("SubjectLacksTag", 3f, "State.Carrying.JerryCan")
                    .Expect("SubjectVisualHidden", "Carry_JerryCan"))
                .Assert(s => s
                    .WaitUntil("SubjectHasAbility", 5f, "GA_SwingNet")
                    .WaitUntil("SubjectHasTag", 10f, "State.Net.Swinging")
                    .Do("EquipSubject", "ITEM_JerryCan")
                    .Expect("SubjectLacksAbility", "GA_SwingNet")
                    .Expect("SubjectLacksTag", "State.Carrying.Net")
                    .Expect("SubjectHasTag", "State.Carrying.JerryCan")
                    .Expect("SubjectHasAbility", "GA_Sprint")
                    .Wait(3f, "subject observes the can")
                    .Do("UnequipSubject")
                    .Expect("SubjectHolds", "none")
                    .Expect("SubjectLacksTag", "State.Carrying.JerryCan"))
                .Build(),
            builder => builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Ship damage end to end, without the repair tool: the server breaks part 0 near the spawn. The subject sees the
        /// marker, the ship's damaged tag and the HUD line. The server sees the fuel leak (even though nobody drives),
        /// then restores the part, and both sides see the leak, tag, marker and HUD line go away.
        /// </summary>
        public static readonly ScenarioEntry ShipDamage = new(
            new Scenario.Builder("ShipDamage")
                .Describe("Break part 0 -> marker, damaged tag, HUD and fuel leak on both peers -> restore -> all clear.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("PointHealthy", "0")
                    .Expect("LeakRateAtMost", "0")
                    .Do("RecordFuel")
                    .Do("BreakPoint", "0"))
                .Act(s => s
                    .WaitUntil("PointBroken", 5f, "0")
                    .WaitUntil("MarkerShown", 3f, "0")
                    .WaitUntil("ShipHasTag", 3f, "State.Ship.Damaged")
                    .WaitUntil("PointHasTag", 3f, "0:State.Damaged")
                    .WaitUntil("HudShows", 3f, "Hull breaches")
                    .Checkpoint("part-broken")
                    .WaitUntil("PointHealthy", 30f, "0")
                    .WaitUntil("MarkerHidden", 3f, "0")
                    .WaitUntil("ShipLacksTag", 3f, "State.Ship.Damaged")
                    .WaitUntil("PointLacksTag", 3f, "0:State.Damaged")
                    .WaitUntil("HudHidden", 3f, "Hull breaches")
                    .Checkpoint("part-restored"))
                .Assert(s => s
                    .WaitUntil("LeakRateAbove", 3f, "0")
                    .WaitUntil("FuelDroppedBy", 10f, "0.5")
                    .Wait(3f, "subject observes the break")
                    .Do("RestorePoint", "0")
                    .WaitUntil("LeakRateAtMost", 3f, "0")
                    .Expect("ShipLacksTag", "State.Ship.Damaged")
                    .Expect("PointHealthy", "0"))
                .Build(),
            builder => builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        private static readonly ScenarioEntry[] All = { NetCatch, TagRequest, EquipCycle, ShipDamage };

        public static string Names => string.Join(", ", All.Select(entry => entry.Scenario.Name));

        public static bool TryGet(string? name, out ScenarioEntry entry)
        {
            entry = All.FirstOrDefault(candidate => string.Equals(candidate.Scenario.Name, name, StringComparison.OrdinalIgnoreCase))!;
            return entry != null;
        }
    }
}
