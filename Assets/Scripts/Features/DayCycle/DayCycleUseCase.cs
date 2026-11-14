#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Atmosphere;
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// The day in play, on every peer: the config's driver and sun path (<see cref="DayClock"/>) read on the shared
    /// clock (<see cref="ITimeService.Time"/>, the server's time once connected), so every peer sees the same sun with
    /// nothing replicated. The Atmosphere feature reads it as the <see cref="ISunSource"/>. Plan: .docs/plans/day-cycle.md (D2).
    /// </summary>
    public sealed class DayCycleUseCase : ISunSource
    {
        private readonly DayClock _clock;
        private readonly ITimeService _time;

        public DayCycleUseCase(DayCycleConfig config, ITimeService time, SunPathProcessor sunPath)
        {
            _clock = new DayClock(config.ActiveDriver, config.Path, sunPath);
            _time = time;
        }

        /// <summary>The clock, to swap its driver while running.</summary>
        public DayClock Clock => _clock;

        public SunPosition Sun => _clock.SunAt(_time.Time);

        public float Hour => Sun.Hour;

        public float Elevation => Sun.Elevation;

        public Vector3 ToSun => Sun.ToSun;
    }
}
