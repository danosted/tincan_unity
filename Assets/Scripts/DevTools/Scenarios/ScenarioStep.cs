#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.DevTools.Scenarios
{
    public enum ScenarioStepKind
    {
        /// <summary>Do nothing for <see cref="ScenarioStep.Duration"/> seconds.</summary>
        Wait,
        /// <summary>Hold the actions for <see cref="ScenarioStep.Duration"/> seconds, then release them.</summary>
        Hold,
        /// <summary>Tap the actions once, then settle for <see cref="ScenarioStep.Duration"/> seconds.</summary>
        Tap,
        /// <summary>Run the named <see cref="ScenarioCommand"/> once; failure aborts the scenario.</summary>
        Do,
        /// <summary>Poll the named <see cref="ScenarioProbe"/> until it passes; timing out aborts the scenario.</summary>
        WaitUntil,
        /// <summary>Evaluate the named probe once and record the verdict; failure fails the run but does not abort it.</summary>
        Expect,
        /// <summary>Capture a screenshot named after the step.</summary>
        Checkpoint
    }

    /// <summary>One instruction of a scenario. Build them through <see cref="ScenarioSteps"/>.</summary>
    public readonly struct ScenarioStep
    {
        public readonly ScenarioStepKind Kind;
        public readonly string Label;
        /// <summary>Wait/Hold length, Tap settle time, or WaitUntil timeout, in seconds.</summary>
        public readonly float Duration;
        public readonly string[] Actions;
        /// <summary>Command, probe or checkpoint name.</summary>
        public readonly string Name;
        public readonly string Argument;

        public ScenarioStep(ScenarioStepKind kind, string label, float duration = 0f, string[]? actions = null, string name = "", string argument = "")
        {
            Kind = kind;
            Label = label;
            Duration = Math.Max(0f, duration);
            Actions = actions ?? Array.Empty<string>();
            Name = name;
            Argument = argument;
        }
    }

    /// <summary>Result of a command or probe: whether it held, and a human-readable detail for the report.</summary>
    public readonly struct ScenarioCheck
    {
        public readonly bool Ok;
        public readonly string Detail;

        private ScenarioCheck(bool ok, string detail)
        {
            Ok = ok;
            Detail = detail;
        }

        public static ScenarioCheck Pass(string detail = "") => new(true, detail);
        public static ScenarioCheck Fail(string detail) => new(false, detail);
    }

    /// <summary>A named action a scenario can run, such as spawning something or equipping the subject.</summary>
    public sealed class ScenarioCommand
    {
        public string Name { get; }
        private readonly Func<string, ScenarioCheck> _execute;

        public ScenarioCommand(string name, Func<string, ScenarioCheck> execute)
        {
            Name = name;
            _execute = execute;
        }

        public ScenarioCheck Execute(string argument) => _execute(argument);
    }

    /// <summary>A named condition a scenario can wait for or assert, read from this peer's view of the game.</summary>
    public sealed class ScenarioProbe
    {
        public string Name { get; }
        private readonly Func<string, ScenarioCheck> _evaluate;

        public ScenarioProbe(string name, Func<string, ScenarioCheck> evaluate)
        {
            Name = name;
            _evaluate = evaluate;
        }

        public ScenarioCheck Evaluate(string argument) => _evaluate(argument);
    }

    /// <summary>
    /// The commands and probes one feature contributes to scenarios. Registered in the container by the scenario
    /// catalog entry that needs it, so a library's dependencies are only resolved when its feature is under test.
    /// </summary>
    public interface IScenarioLibrary
    {
        IEnumerable<ScenarioCommand> Commands { get; }
        IEnumerable<ScenarioProbe> Probes { get; }
    }
}
