#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.DevTools
{
    public enum BotShipCommand
    {
        None,
        Take,
        Release
    }

    /// <summary>One segment of a route: which actions are held for how long, plus one-shot taps and ship commands on entry.</summary>
    public readonly struct BotStep
    {
        public readonly string Label;
        public readonly float Duration;
        public readonly string[] Held;
        public readonly string[] Taps;
        public readonly BotShipCommand Ship;

        public BotStep(string label, float duration, string[]? held = null, string[]? taps = null, BotShipCommand ship = BotShipCommand.None)
        {
            Label = label;
            Duration = Math.Max(0f, duration);
            Held = held ?? Array.Empty<string>();
            Taps = taps ?? Array.Empty<string>();
            Ship = ship;
        }
    }

    /// <summary>A fixed, repeatable sequence of <see cref="BotStep"/>s. Build one with <see cref="Builder"/>.</summary>
    public sealed class BotRoute
    {
        public string Name { get; }
        public IReadOnlyList<BotStep> Steps { get; }
        public float TotalDuration { get; }

        public BotRoute(string name, IReadOnlyList<BotStep> steps)
        {
            Name = name;
            Steps = steps;
            float total = 0f;
            foreach (var step in steps) total += step.Duration;
            TotalDuration = total;
        }

        public sealed class Builder
        {
            private readonly string _name;
            private readonly List<BotStep> _steps = new();

            public Builder(string name) => _name = name;

            public Builder Wait(float seconds, string label = "wait") => Add(new BotStep(label, seconds));
            public Builder Hold(float seconds, params string[] actions) => Add(new BotStep("hold " + string.Join("+", actions), seconds, actions));
            public Builder Tap(string action, float settleSeconds) => Add(new BotStep("tap " + action, settleSeconds, taps: new[] { action }));
            public Builder Ship(BotShipCommand command, float settleSeconds) => Add(new BotStep("ship " + command, settleSeconds, ship: command));

            public Builder Repeat(int times, Action<Builder> body)
            {
                for (int i = 0; i < times; i++) body(this);
                return this;
            }

            public BotRoute Build() => new(_name, _steps.ToArray());

            private Builder Add(BotStep step)
            {
                _steps.Add(step);
                return this;
            }
        }
    }

    /// <summary>Walks a route by elapsed time and reports each step once, on entry.</summary>
    public sealed class BotRouteCursor
    {
        private readonly BotRoute _route;
        private int _current = -1;

        public BotRouteCursor(BotRoute route) => _route = route;

        public int CurrentIndex => _current;
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Moves to the step active at <paramref name="elapsed"/> seconds. Returns the steps entered since the last call,
        /// in order, so a long frame never skips a tap or ship command.
        /// </summary>
        public IReadOnlyList<BotStep> Advance(float elapsed, List<BotStep> entered)
        {
            entered.Clear();
            if (IsComplete) return entered;

            float stepStart = 0f;
            int target = -1;
            for (int i = 0; i < _route.Steps.Count; i++)
            {
                float stepEnd = stepStart + _route.Steps[i].Duration;
                if (elapsed < stepEnd)
                {
                    target = i;
                    break;
                }
                stepStart = stepEnd;
            }

            int last = target < 0 ? _route.Steps.Count - 1 : target;
            for (int i = _current + 1; i <= last; i++) entered.Add(_route.Steps[i]);
            _current = last;
            IsComplete = target < 0;
            return entered;
        }
    }
}
