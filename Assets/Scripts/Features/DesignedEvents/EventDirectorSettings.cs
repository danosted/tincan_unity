#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>When the director starts events by itself. Lives on the events installer (per profile).</summary>
    [Serializable]
    public sealed class EventDirectorSettings
    {
        [Tooltip("Start catalog events in rotation without anyone asking. Scripted harness runs switch this off.")]
        public bool AutoStart = true;

        [Tooltip("Seconds after the first server tick before the first event.")]
        [Min(0f)] public float FirstEventDelay = 30f;

        [Tooltip("Seconds of quiet between the end of one event and the start of the next.")]
        [Min(0f)] public float QuietGap = 60f;
    }
}
