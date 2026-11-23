#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>An encounter zone in the world, as every peer sees it (replicated by its mediator).</summary>
    public interface IEncounterZone : IActor
    {
        string DisplayName { get; }

        /// <summary>Its centre on the map (x, z); a zone is a disc at every height.</summary>
        Vector2 Centre { get; }

        float Radius { get; }

        EncounterZoneState State { get; }

        /// <summary>Server time the warning or the rest ends (meaningless in the other states).</summary>
        double StateEndsAt { get; }

        /// <summary>Server: moves it (the scenario stands a move in for the ship sailing in or out).</summary>
        void ServerSetCentre(Vector2 centre);

        void ServerSetState(EncounterZoneState state, double endsAt);
    }
}
