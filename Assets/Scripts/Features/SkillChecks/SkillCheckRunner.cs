#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using VContainer.Unity;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// This peer, per frame: runs the check the server opened for this player. It opens the skill check input context
    /// (walking and looking go quiet) and lays the check out here (the gauge's band, or the combo's directions; co-op
    /// trusts the peer), harder by the level it was opened at (a challenge's escalation). The pulsing gauge grades the first Press against the band; the combo grades once its last
    /// direction is pressed. Time running out is a Miss. The grade goes to the server and shows for a moment.
    /// </summary>
    public sealed class SkillCheckRunner : ITickable, ISkillCheckRunner
    {
        /// <summary>Seconds the grade shows after a check.</summary>
        public const float ShowSeconds = 1.2f;

        private static readonly ComboDirection[] AllDirections =
            { ComboDirection.Left, ComboDirection.Right, ComboDirection.Up, ComboDirection.Down };

        private readonly SkillCheckHostRegistry _hosts;
        private readonly IInputReader _input;
        private readonly IInputContextSwitch _contexts;
        private readonly SkillCheckInputContext _controls;
        private readonly ITimeService _time;
        private readonly PulsingGaugeProcessor _gauge;
        private readonly ComboProcessor _combo;
        private readonly Random _random = new();
        private ISkillCheckHost? _host;
        private int _checkId;
        private float _elapsed;
        private float _period;
        private float _showLeft;
        private int _wrongPresses;

        public SkillCheckRunner(SkillCheckHostRegistry hosts, IInputReader input, IInputContextSwitch contexts, SkillCheckInputContext controls,
            ITimeService time, PulsingGaugeProcessor gauge, ComboProcessor combo)
        {
            _hosts = hosts;
            _input = input;
            _contexts = contexts;
            _controls = controls;
            _time = time;
            _gauge = gauge;
            _combo = combo;
        }

        public bool IsRunning => _host != null;
        public SkillCheckDefinition? Definition { get; private set; }
        public float Needle { get; private set; }
        public PulsingGaugeBand Band { get; private set; }
        public IReadOnlyList<ComboDirection> Combo { get; private set; } = Array.Empty<ComboDirection>();
        public int ComboDone { get; private set; }
        public float SecondsLeft => Definition != null && IsRunning ? Definition.Seconds - _elapsed : 0f;
        public SkillCheckGrade? Shown { get; private set; }

        public void Tick()
        {
            if (_host == null)
            {
                if (_showLeft > 0f && (_showLeft -= _time.DeltaTime) <= 0f) Shown = null;
                TryBegin();
                return;
            }

            var definition = Definition!;
            if (!_host.IsSimulating)
            {
                Finish(null);
                return;
            }

            _elapsed += _time.DeltaTime;
            if (definition.Kind == SkillCheckKind.Combo) TickCombo();
            else TickGauge();
            if (_host != null && _elapsed >= definition.Seconds) Finish(SkillCheckGrade.Miss);
        }

        private void TickGauge()
        {
            Needle = _gauge.Needle(_elapsed, _period);
            if (_input.WasPressedThisFrame(_controls.Press)) Finish(_gauge.Grade(Needle, Band));
        }

        private void TickCombo()
        {
            foreach (var direction in AllDirections)
            {
                if (!_input.WasPressedThisFrame(_controls.For(direction))) continue;

                ComboDone = _combo.Advance(Combo, ComboDone, direction, out bool wrong);
                if (wrong) _wrongPresses++;
                if (ComboDone >= Combo.Count) Finish(_combo.Grade(true, _wrongPresses));
                return; // one press a frame
            }
        }

        private void TryBegin()
        {
            foreach (var host in _hosts.All)
            {
                if (!host.TryTakeOpened(out int checkId, out int level) || host.Definition == null) continue;

                _host = host;
                _checkId = checkId;
                var definition = Definition = host.Definition;
                _period = SkillCheckDifficulty.Period(definition.Period, level);
                Band = _gauge.Band(_random, SkillCheckDifficulty.Width(definition.GoodWidth, level),
                    SkillCheckDifficulty.Width(definition.PerfectWidth, level));
                Combo = definition.Kind == SkillCheckKind.Combo
                    ? _combo.Sequence(_random, SkillCheckDifficulty.ComboLength(definition.ComboLength, level))
                    : Array.Empty<ComboDirection>();
                ComboDone = 0;
                _wrongPresses = 0;
                _elapsed = 0f;
                Needle = 0f;
                Shown = null;
                _contexts.SetOpen(_controls, true);
                return;
            }
        }

        private void Finish(SkillCheckGrade? grade)
        {
            if (grade is { } given) _host!.SendGrade(_checkId, given);
            _host = null;
            _contexts.SetOpen(_controls, false);
            Shown = grade;
            _showLeft = grade != null ? ShowSeconds : 0f;
        }
    }
}
