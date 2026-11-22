#nullable enable
using System;

namespace TinCan.Features.Camping
{
    public readonly struct FireStokedEvent
    {
        public readonly Guid RequesterId;
        public readonly float BurnSecondsLeft;

        public FireStokedEvent(Guid requesterId, float burnSecondsLeft)
        {
            RequesterId = requesterId;
            BurnSecondsLeft = burnSecondsLeft;
        }

        public override string ToString() => $"Actor {RequesterId} stoked a fire ({BurnSecondsLeft:0} s of burning)";
    }
}
