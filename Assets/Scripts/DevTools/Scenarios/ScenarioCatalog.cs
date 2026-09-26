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

        private static readonly ScenarioEntry[] All = { NetCatch, TagRequest };

        public static string Names => string.Join(", ", All.Select(entry => entry.Scenario.Name));

        public static bool TryGet(string? name, out ScenarioEntry entry)
        {
            entry = All.FirstOrDefault(candidate => string.Equals(candidate.Scenario.Name, name, StringComparison.OrdinalIgnoreCase))!;
            return entry != null;
        }
    }
}
