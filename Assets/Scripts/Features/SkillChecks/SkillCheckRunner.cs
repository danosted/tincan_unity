#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using VContainer.Unity;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// This peer, per frame: runs the check the server opened for this player. It opens the skill check input context
    /// (walking and looking go quiet), moves the needle on local time, grades the first Press against the band, or
    /// gives a Miss when time runs out, and sends the grade to the server. The grade then shows for a moment.
    /// </summary>
    public sealed class SkillCheckRunner : ITickable, ISkillCheckRunner
    {
        /// <summary>Seconds the grade shows after a check.</summary>
        public const float ShowSeconds = 1.2f;

        private readonly IActorRegistry _actors;
        private readonly IInputReader _input;
        private readonly IInputContextSwitch _contexts;
        private readonly SkillCheckInputContext _controls;
        private readonly ITimeService _time;
        private readonly PulsingGaugeProcessor _gauge;
        private ISkillCheckHost? _host;
        private int _checkId;
        private float _elapsed;
        private float _showLeft;

        public SkillCheckRunner(IActorRegistry actors, IInputReader input, IInputContextSwitch contexts, SkillCheckInputContext controls,
            ITimeService time, PulsingGaugeProcessor gauge)
        {
            _actors = actors;
            _input = input;
            _contexts = contexts;
            _controls = controls;
            _time = time;
            _gauge = gauge;
        }

        public bool IsRunning => _host != null;
        public SkillCheckDefinition? Definition { get; private set; }
        public float Needle { get; private set; }
        public PulsingGaugeBand Band { get; private set; }
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
            Needle = _gauge.Needle(_elapsed, definition.Period);
            if (_input.WasPressedThisFrame(_controls.Press)) Finish(_gauge.Grade(Needle, Band));
            else if (_elapsed >= definition.Seconds) Finish(SkillCheckGrade.Miss);
        }

        private void TryBegin()
        {
            foreach (var host in _actors.GetActors<ISkillCheckHost>())
            {
                if (!host.TryTakeOpened(out var opening) || host.Definition == null) continue;

                _host = host;
                _checkId = opening.CheckId;
                Definition = host.Definition;
                Band = new PulsingGaugeBand(opening.BandCenter, Definition.GoodWidth, Definition.PerfectWidth);
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
