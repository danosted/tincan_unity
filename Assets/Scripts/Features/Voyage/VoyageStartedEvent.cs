#nullable enable
using UnityEngine;

namespace TinCan.Features.Voyage
{
    /// <summary>Server: a voyage began its briefing; the world has been reset.</summary>
    public readonly struct VoyageStartedEvent
    {
        public readonly int Voyage;
        public readonly Vector3 Destination;

        public VoyageStartedEvent(int voyage, Vector3 destination)
        {
            Voyage = voyage;
            Destination = destination;
        }
    }
}
