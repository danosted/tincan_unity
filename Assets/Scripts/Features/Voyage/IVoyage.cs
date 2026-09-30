#nullable enable
using UnityEngine;

namespace TinCan.Features.Voyage
{
    /// <summary>Server-side control over the voyage, for dev tooling (scenarios) and, later, designed events.</summary>
    public interface IVoyage
    {
        VoyagePhase Phase { get; }

        /// <summary>Start a new voyage now: reset the world and begin the briefing.</summary>
        bool Begin();

        /// <summary>End the briefing now and cast off.</summary>
        bool CastOff();

        /// <summary>Move this voyage's destination (scenarios put it on the ship to arrive).</summary>
        bool MoveDestination(Vector3 destination);
    }
}
