#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.DevTools.Scenarios
{
    public enum ScenarioStatus
    {
        Running,
        Passed,
        Failed
    }

    /// <summary>What a <see cref="ScenarioRunner"/> needs from the running game. The use case implements it; tests fake it.</summary>
    public interface IScenarioWorld
    {
        void Press(ScriptedAction action);
        void Release(ScriptedAction action);
        void Tap(ScriptedAction action);
        ScenarioCheck Execute(string command, string argument);
        ScenarioCheck Evaluate(string probe, string argument);
        /// <summary>Captures a screenshot and returns where it will be written.</summary>
        string Capture(string checkpoint);
    }

    [Serializable]
    public struct ScenarioExpectation
    {
        public string name;
        public bool passed;
        public string detail;
    }

    /// <summary>
    /// Walks one role's steps against an <see cref="IScenarioWorld"/>. Instant steps (Do, Expect, Checkpoint, a passing
    /// WaitUntil) chain within one tick. A failed Do or a timed-out WaitUntil aborts the run; a failed Expect is
    /// recorded and the run continues, so one report lists every broken expectation.
    /// </summary>
    public sealed class ScenarioRunner
    {
        private readonly IReadOnlyList<ScenarioStep> _steps;
        private readonly IScenarioWorld _world;
        private readonly ScenarioTimeline _timeline;
        private readonly HashSet<ScriptedAction> _held = new();
        private readonly List<string> _failures = new();
        private readonly List<ScenarioExpectation> _expectations = new();
        private readonly List<string> _captures = new();
        private readonly string? _lane;

        private int _index;
        private bool _entered;
        private float _stepStart;
        private float _now;

        /// <param name="lane">Optional name ("server", "subject") prefixed to this runner's report lines, for a peer running several lanes.</param>
        public ScenarioRunner(IReadOnlyList<ScenarioStep> steps, IScenarioWorld world, ScenarioTimeline timeline, string? lane = null)
        {
            _lane = lane;
            _steps = steps;
            _world = world;
            _timeline = timeline;
        }

        public ScenarioStatus Status { get; private set; } = ScenarioStatus.Running;
        public bool IsDone => Status != ScenarioStatus.Running;
        public string CurrentStep => _index < _steps.Count ? _steps[_index].Label : "done";
        public IReadOnlyList<string> Failures => _failures;
        public IReadOnlyList<ScenarioExpectation> Expectations => _expectations;
        public IReadOnlyList<string> Captures => _captures;

        /// <summary>Advances to <paramref name="elapsed"/> seconds since the scenario started.</summary>
        public void Tick(float elapsed)
        {
            if (IsDone) return;
            _now = elapsed;

            while (_index < _steps.Count)
            {
                var step = _steps[_index];
                if (!_entered) Enter(step);
                if (IsDone) return;
                if (!IsComplete(step)) return;

                Exit(step);
                if (IsDone) return;
                _index++;
                _entered = false;
            }

            Status = _failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
            _timeline.Add("end", L(Status.ToString()), Status == ScenarioStatus.Passed, string.Join("; ", _failures));
        }

        /// <summary>Stops the run from outside (overall timeout, shutdown) and releases held input.</summary>
        public void Abort(string reason)
        {
            if (IsDone) return;
            Fail($"{L("aborted")} at '{CurrentStep}': {reason}");
        }

        private void Enter(ScenarioStep step)
        {
            _entered = true;
            _stepStart = _now;
            _timeline.Add("step", L(step.Label));

            switch (step.Kind)
            {
                case ScenarioStepKind.Hold:
                    foreach (var action in step.Actions)
                    {
                        if (_held.Add(action)) _world.Press(action);
                    }
                    break;
                case ScenarioStepKind.Tap:
                    foreach (var action in step.Actions) _world.Tap(action);
                    break;
                case ScenarioStepKind.Do:
                    var result = _world.Execute(step.Name, step.Argument);
                    _timeline.Add("do", L(step.Label), result.Ok, result.Detail);
                    if (!result.Ok) Fail($"'{L(step.Label)}' failed: {result.Detail}");
                    break;
                case ScenarioStepKind.Expect:
                    var verdict = _world.Evaluate(step.Name, step.Argument);
                    _expectations.Add(new ScenarioExpectation { name = L(step.Label), passed = verdict.Ok, detail = verdict.Detail });
                    _timeline.Add("expect", L(step.Label), verdict.Ok, verdict.Detail);
                    if (!verdict.Ok) _failures.Add($"'{L(step.Label)}' not met: {verdict.Detail}");
                    break;
                case ScenarioStepKind.Checkpoint:
                    string path = _world.Capture(step.Name);
                    _captures.Add(path);
                    _timeline.Add("checkpoint", L(step.Name), true, path);
                    break;
            }
        }

        private bool IsComplete(ScenarioStep step)
        {
            float inStep = _now - _stepStart;

            switch (step.Kind)
            {
                case ScenarioStepKind.Wait:
                case ScenarioStepKind.Hold:
                case ScenarioStepKind.Tap:
                    return inStep >= step.Duration;
                case ScenarioStepKind.WaitUntil:
                    var check = _world.Evaluate(step.Name, step.Argument);
                    if (check.Ok)
                    {
                        _timeline.Add("reached", L(step.Label), true, $"after {inStep:0.00} s. {check.Detail}".Trim());
                        return true;
                    }
                    if (inStep >= step.Duration) Fail($"'{L(step.Label)}' timed out after {step.Duration:0.#} s: {check.Detail}");
                    return false;
                default:
                    return true;
            }
        }

        private void Exit(ScenarioStep step)
        {
            if (step.Kind != ScenarioStepKind.Hold) return;

            foreach (var action in step.Actions)
            {
                if (_held.Remove(action)) _world.Release(action);
            }
        }

        private string L(string label) => _lane == null ? label : $"{_lane}: {label}";

        private void Fail(string failure)
        {
            _failures.Add(failure);
            foreach (var action in _held) _world.Release(action);
            _held.Clear();
            Status = ScenarioStatus.Failed;
            _timeline.Add("end", L("Failed"), false, failure);
        }
    }
}
