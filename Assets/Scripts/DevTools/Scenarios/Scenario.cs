#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Which part of a scenario this peer plays. <see cref="Solo"/> is a host playing alone against its own player;
    /// in a host + client run the host is <see cref="Server"/> and the client is <see cref="Subject"/>.
    /// </summary>
    public enum ScenarioRole
    {
        Solo,
        Server,
        Subject
    }

    /// <summary>
    /// A feature test run in the live game, in three phases. The server arranges the world around the subject player
    /// (spawn, equip, break), the subject acts through real input and checks what it sees, and the server asserts the
    /// authoritative outcome. Phases synchronise through replicated state (WaitUntil), never through timing guesses.
    /// </summary>
    public sealed class Scenario
    {
        public string Name { get; }
        public string Description { get; }
        public float TimeoutSeconds { get; }
        public IReadOnlyList<ScenarioStep> Arrange { get; }
        public IReadOnlyList<ScenarioStep> Act { get; }
        public IReadOnlyList<ScenarioStep> Assert { get; }

        private Scenario(string name, string description, float timeoutSeconds, IReadOnlyList<ScenarioStep> arrange, IReadOnlyList<ScenarioStep> act, IReadOnlyList<ScenarioStep> assert)
        {
            Name = name;
            Description = description;
            TimeoutSeconds = timeoutSeconds;
            Arrange = arrange;
            Act = act;
            Assert = assert;
        }

        public IReadOnlyList<ScenarioStep> StepsFor(ScenarioRole role) => role switch
        {
            ScenarioRole.Solo => Arrange.Concat(Act).Concat(Assert).ToArray(),
            ScenarioRole.Server => Arrange.Concat(Assert).ToArray(),
            ScenarioRole.Subject => Act,
            _ => Array.Empty<ScenarioStep>()
        };

        public sealed class Builder
        {
            private readonly string _name;
            private string _description = string.Empty;
            private float _timeout = 60f;
            private readonly ScenarioSteps _arrange = new();
            private readonly ScenarioSteps _act = new();
            private readonly ScenarioSteps _assert = new();

            public Builder(string name) => _name = name;

            public Builder Describe(string description)
            {
                _description = description;
                return this;
            }

            public Builder Timeout(float seconds)
            {
                _timeout = seconds;
                return this;
            }

            public Builder Arrange(Action<ScenarioSteps> steps)
            {
                steps(_arrange);
                return this;
            }

            public Builder Act(Action<ScenarioSteps> steps)
            {
                steps(_act);
                return this;
            }

            public Builder Assert(Action<ScenarioSteps> steps)
            {
                steps(_assert);
                return this;
            }

            public Scenario Build() => new(_name, _description, _timeout, _arrange.ToArray(), _act.ToArray(), _assert.ToArray());
        }
    }

    /// <summary>Fluent list of <see cref="ScenarioStep"/>s for one scenario phase.</summary>
    public sealed class ScenarioSteps
    {
        private readonly List<ScenarioStep> _steps = new();

        public ScenarioSteps Wait(float seconds, string label = "wait") =>
            Add(new ScenarioStep(ScenarioStepKind.Wait, label, seconds));

        public ScenarioSteps Hold(float seconds, params string[] actions) =>
            Add(new ScenarioStep(ScenarioStepKind.Hold, "hold " + string.Join("+", actions), seconds, actions));

        public ScenarioSteps Tap(string action, float settleSeconds = 0.2f) =>
            Add(new ScenarioStep(ScenarioStepKind.Tap, "tap " + action, settleSeconds, new[] { action }));

        public ScenarioSteps Do(string command, string argument = "") =>
            Add(new ScenarioStep(ScenarioStepKind.Do, Describe("do", command, argument), name: command, argument: argument));

        public ScenarioSteps WaitUntil(string probe, float timeoutSeconds, string argument = "") =>
            Add(new ScenarioStep(ScenarioStepKind.WaitUntil, Describe("until", probe, argument), timeoutSeconds, name: probe, argument: argument));

        public ScenarioSteps Expect(string probe, string argument = "") =>
            Add(new ScenarioStep(ScenarioStepKind.Expect, Describe("expect", probe, argument), name: probe, argument: argument));

        public ScenarioSteps Checkpoint(string name) =>
            Add(new ScenarioStep(ScenarioStepKind.Checkpoint, "checkpoint " + name, name: name));

        public ScenarioStep[] ToArray() => _steps.ToArray();

        private static string Describe(string verb, string name, string argument) =>
            string.IsNullOrEmpty(argument) ? $"{verb} {name}" : $"{verb} {name}({argument})";

        private ScenarioSteps Add(ScenarioStep step)
        {
            _steps.Add(step);
            return this;
        }
    }
}
