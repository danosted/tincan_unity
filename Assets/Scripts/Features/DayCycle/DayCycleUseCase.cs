#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Atmosphere;
using UnityEngine;
using VContainer;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// The day in play, on every peer: the config's driver and sun path (<see cref="DayClock"/>) read on the shared
    /// clock (<see cref="ITimeService.Time"/>, the server's time once connected) and, when the scene runs sessions, the
    /// session's progress (<see cref="ISessionProgress"/>, from Voyage, for the voyage driver), so every peer sees the
    /// same sun with nothing replicated. The Atmosphere feature reads it as the <see cref="ISunSource"/>.
    /// Plan: .docs/plans/day-cycle.md (D2, D3).
    /// </summary>
    public sealed class DayCycleUseCase : ISunSource
    {
        private readonly DayClock _clock;
        private readonly ITimeService _time;
        private readonly IObjectResolver? _resolver;
        private ISessionProgress? _session;
        private bool _resolved;

        [Inject]
        public DayCycleUseCase(DayCycleConfig config, ITimeService time, SunPathProcessor sunPath, IObjectResolver resolver)
            : this(config, time, sunPath, session: null)
        {
            _resolver = resolver;
            _resolved = false;
        }

        public DayCycleUseCase(DayCycleConfig config, ITimeService time, SunPathProcessor sunPath, ISessionProgress? session)
        {
            _clock = new DayClock(config.ActiveDriver, config.Path, sunPath);
            _time = time;
            _session = session;
            _resolved = true;
        }

        /// <summary>The clock, to swap its driver while running.</summary>
        public DayClock Clock => _clock;

        public SunPosition Sun => _clock.SunAt(Input());

        public float Hour => Sun.Hour;

        public float Elevation => Sun.Elevation;

        public Vector3 ToSun => Sun.ToSun;

        private DayClockInput Input()
        {
            // Resolved on first use, not in the constructor: the session's feature may be built after this one.
            if (!_resolved)
            {
                _resolver?.TryResolve(out _session);
                _resolved = true;
            }

            float? progress = _session != null && _session.TryGetProgress(out float value) ? value : null;
            return new DayClockInput(_time.Time, progress);
        }
    }
}
