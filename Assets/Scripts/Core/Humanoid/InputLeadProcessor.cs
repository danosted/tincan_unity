#nullable enable
using System;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Domain, pure: steers how far an owning client's clock runs ahead of the server so its inputs arrive just in time.
    /// NGO leads the client by half its RTT estimate plus <c>LocalBufferSec</c>; that estimate comes from sparse reliable
    /// acks and runs several ticks high, so inputs waited 3–4 ticks in the server's queue (measured 2026-10-03,
    /// .docs/plans/input-queue-lead.md). The server reports the queue depth it sees; this nudges <c>LocalBufferSec</c>
    /// so the depth settles around <see cref="TargetDepth"/>:
    /// <list type="bullet">
    /// <item>within <see cref="DeadBand"/> of the target: no change (no hunting);</item>
    /// <item>deeper: lower the lead slowly (<see cref="LowerRate"/>), so the change never outruns the ~1.5 s it takes to
    /// show in the reported depth;</item>
    /// <item>shallower (the server is close to starving, repeating inputs): raise it at NGO's own slew rate
    /// (<see cref="RaiseRate"/>).</item>
    /// </list>
    /// The result stays within [<see cref="MinBufferSec"/>, <see cref="MaxBufferSec"/>]; a negative buffer cancels part of
    /// an inflated RTT estimate.
    /// </summary>
    public sealed class InputLeadProcessor
    {
        public const float TargetDepth = 1.2f;
        public const float DeadBand = 0.3f;
        /// <summary>Seconds of lead removed per second while the queue is too deep.</summary>
        public const double LowerRate = 0.005;
        /// <summary>Seconds of lead added per second while the queue is too shallow (NGO slews its offset at 0.01 s/s).</summary>
        public const double RaiseRate = 0.01;
        public const double MinBufferSec = -0.5;
        public const double MaxBufferSec = 0.25;
        public const float MaxStepSec = 0.25f;

        /// <summary>The next <c>LocalBufferSec</c>, given the current one, the server's reported depth and the time since the last call.</summary>
        public double Next(double localBufferSec, float reportedDepth, float deltaSec)
        {
            if (deltaSec <= 0f) return localBufferSec;
            deltaSec = Math.Min(deltaSec, MaxStepSec); // after a gap in snapshots, no single large jump

            float error = reportedDepth - TargetDepth;
            if (Math.Abs(error) <= DeadBand) return Math.Clamp(localBufferSec, MinBufferSec, MaxBufferSec);

            double rate = error > 0f ? -LowerRate : RaiseRate;
            return Math.Clamp(localBufferSec + rate * deltaSec, MinBufferSec, MaxBufferSec);
        }
    }
}
