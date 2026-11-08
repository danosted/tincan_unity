#nullable enable
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Domain, pure: the pulsing gauge. A needle sweeps from 0 to 1 and back at a steady speed, once every period; a
    /// press grades where it is against the band: inside the Perfect part, Perfect; inside the Good part, Good; else Miss.
    /// </summary>
    public class PulsingGaugeProcessor
    {
        /// <summary>The needle, 0 to 1, after this many seconds (it starts at 0 and turns at 1).</summary>
        public float Needle(float elapsed, float period)
        {
            if (period <= 0f) return 0f;
            float phase = Mathf.Repeat(elapsed / period, 1f);
            return phase < 0.5f ? phase * 2f : 2f - phase * 2f;
        }

        public SkillCheckGrade Grade(float needle, in PulsingGaugeBand band)
        {
            float off = Mathf.Abs(needle - band.Center);
            if (off <= band.PerfectWidth * 0.5f) return SkillCheckGrade.Perfect;
            if (off <= band.GoodWidth * 0.5f) return SkillCheckGrade.Good;
            return SkillCheckGrade.Miss;
        }
    }
}
