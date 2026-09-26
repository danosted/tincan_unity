#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.DevTools;
using TinCan.DevTools.Scenarios;

namespace TinCan.Tests.EditMode
{
    public class ScenarioRunnerTests
    {
        private sealed class FakeWorld : IScenarioWorld
        {
            public readonly List<string> Log = new();
            public readonly HashSet<string> Held = new();
            public readonly Dictionary<string, bool> Probes = new();
            public readonly Dictionary<string, bool> Commands = new();

            public void Press(string action)
            {
                Held.Add(action);
                Log.Add("press " + action);
            }

            public void Release(string action)
            {
                Held.Remove(action);
                Log.Add("release " + action);
            }

            public void Tap(string action) => Log.Add("tap " + action);

            public ScenarioCheck Execute(string command, string argument)
            {
                Log.Add($"do {command}({argument})");
                return Commands.TryGetValue(command, out var ok) && ok ? ScenarioCheck.Pass() : ScenarioCheck.Fail("refused");
            }

            public ScenarioCheck Evaluate(string probe, string argument) =>
                Probes.TryGetValue(probe, out var ok) && ok ? ScenarioCheck.Pass("yes") : ScenarioCheck.Fail("no");

            public string Capture(string checkpoint)
            {
                Log.Add("capture " + checkpoint);
                return checkpoint + ".png";
            }
        }

        private FakeWorld _world = null!;
        private ScenarioTimeline _timeline = null!;

        [SetUp]
        public void SetUp()
        {
            _world = new FakeWorld();
            _timeline = new ScenarioTimeline();
            _timeline.Start(0f);
        }

        private ScenarioRunner Runner(ScenarioSteps steps) => new(steps.ToArray(), _world, _timeline);

        [Test]
        public void Hold_PressesForItsDurationThenReleases()
        {
            var runner = Runner(new ScenarioSteps().Hold(1f, "Fire").Wait(1f));

            runner.Tick(0f);
            Assert.That(_world.Held, Does.Contain("Fire"));

            runner.Tick(1.1f);
            Assert.That(_world.Held, Is.Empty);
            Assert.That(runner.IsDone, Is.False);

            runner.Tick(2.2f);
            Assert.That(runner.Status, Is.EqualTo(ScenarioStatus.Passed));
        }

        [Test]
        public void InstantSteps_ChainWithinOneTick()
        {
            _world.Commands["Spawn"] = true;
            _world.Probes["Ready"] = true;
            var runner = Runner(new ScenarioSteps().Do("Spawn", "x").WaitUntil("Ready", 5f).Checkpoint("shot").Expect("Ready"));

            runner.Tick(0f);

            Assert.That(runner.Status, Is.EqualTo(ScenarioStatus.Passed));
            Assert.That(_world.Log, Is.EqualTo(new[] { "do Spawn(x)", "capture shot" }));
            Assert.That(runner.Captures, Is.EqualTo(new[] { "shot.png" }));
        }

        [Test]
        public void WaitUntil_BlocksUntilProbePasses()
        {
            var runner = Runner(new ScenarioSteps().WaitUntil("Ready", 5f).Tap("Jump"));

            runner.Tick(0f);
            runner.Tick(1f);
            Assert.That(_world.Log, Is.Empty);

            _world.Probes["Ready"] = true;
            runner.Tick(2f);
            Assert.That(_world.Log, Is.EqualTo(new[] { "tap Jump" }));
            Assert.That(_timeline.Entries.Any(entry => entry.kind == "reached"), Is.True);
        }

        [Test]
        public void WaitUntil_Timeout_FailsWithTheProbeDetail()
        {
            var runner = Runner(new ScenarioSteps().WaitUntil("Never", 1f).Tap("Jump"));

            runner.Tick(0f);
            runner.Tick(1.5f);

            Assert.That(runner.Status, Is.EqualTo(ScenarioStatus.Failed));
            Assert.That(runner.Failures.Single(), Does.Contain("timed out").And.Contain("no"));
            Assert.That(_world.Log, Does.Not.Contain("tap Jump"));
        }

        [Test]
        public void FailedCommand_AbortsTheRun()
        {
            var runner = Runner(new ScenarioSteps().Do("Missing").Tap("Jump"));

            runner.Tick(0f);

            Assert.That(runner.Status, Is.EqualTo(ScenarioStatus.Failed));
            Assert.That(_world.Log, Does.Not.Contain("tap Jump"));
        }

        [Test]
        public void FailedExpect_IsRecordedAndTheRunContinues()
        {
            var runner = Runner(new ScenarioSteps().Expect("Ready").Tap("Jump", 0f));

            runner.Tick(0f);

            Assert.That(runner.Status, Is.EqualTo(ScenarioStatus.Failed));
            Assert.That(_world.Log, Does.Contain("tap Jump"));
            Assert.That(runner.Expectations.Single().passed, Is.False);
        }

        [Test]
        public void Abort_WhileHolding_ReleasesInput()
        {
            var runner = Runner(new ScenarioSteps().Hold(10f, "Fire"));
            runner.Tick(0f);

            runner.Abort("timeout");

            Assert.That(runner.Status, Is.EqualTo(ScenarioStatus.Failed));
            Assert.That(_world.Held, Is.Empty);
            Assert.That(runner.Failures.Single(), Does.Contain("timeout"));
        }

        [Test]
        public void Timeline_RecordsStepsAndEnd()
        {
            var runner = Runner(new ScenarioSteps().Wait(0.5f, "settle"));

            runner.Tick(0f);
            _timeline.SetTime(1f);
            runner.Tick(1f);

            var labels = _timeline.Entries.Select(entry => entry.label).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "settle", "Passed" }));
            Assert.That(_timeline.Entries.Last().t, Is.EqualTo(1f));
        }
    }

    public class ScenarioDefinitionTests
    {
        private static readonly Scenario Sample = new Scenario.Builder("Sample")
            .Arrange(s => s.Do("A"))
            .Act(s => s.Do("B"))
            .Assert(s => s.Do("C"))
            .Build();

        [TestCase(ScenarioRole.Solo, "A,B,C")]
        [TestCase(ScenarioRole.Server, "A,C")]
        [TestCase(ScenarioRole.Subject, "B")]
        public void StepsFor_ComposesPhasesPerRole(ScenarioRole role, string expected)
        {
            var names = string.Join(",", Sample.StepsFor(role).Select(step => step.Name));

            Assert.That(names, Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_FindsScenariosCaseInsensitively()
        {
            Assert.That(ScenarioCatalog.TryGet("netcatch", out var entry), Is.True);
            Assert.That(entry.Scenario.Name, Is.EqualTo("NetCatch"));
            Assert.That(ScenarioCatalog.TryGet("nope", out _), Is.False);
        }

        [Test]
        public void HarnessOptions_ParsesScenarioAndSoloMode()
        {
            var options = HarnessOptions.Parse(new[] { "-scenario", "NetCatch", "-scenariomode", "solo" });

            Assert.That(options.Scenario, Is.EqualTo("NetCatch"));
            Assert.That(options.ScenarioSolo, Is.True);
            Assert.That(options.IsActive, Is.True);
            Assert.That(options.IsScripted, Is.True);
            Assert.That(options.TelemetryEnabled, Is.False);
        }

        [Test]
        public void EventRecorder_DescribesEventFields()
        {
            Assert.That(ScenarioEventRecorder.Describe(new SampleEvent(3, "x")), Is.EqualTo("Count=3, Name=x"));
        }

        private readonly struct SampleEvent
        {
            public readonly int Count;
            public readonly string Name;

            public SampleEvent(int count, string name)
            {
                Count = count;
                Name = name;
            }
        }
    }
}
