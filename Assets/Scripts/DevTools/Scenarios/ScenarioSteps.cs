#nullable enable
using System.Collections.Generic;

namespace TinCan.DevTools.Scenarios
{
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
