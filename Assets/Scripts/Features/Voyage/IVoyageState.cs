#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// The voyage as every peer sees it: replicated by the VoyageState fixture on the ship, so hosts, clients and late
    /// joiners agree. The server writes it (the Server* calls do nothing elsewhere); anyone may ask for a restart.
    /// </summary>
    public interface IVoyageState : IActor
    {
        VoyagePhase Phase { get; }

        /// <summary>Which voyage this is: 1 for the first, one more for every restart.</summary>
        int Voyage { get; }

        /// <summary>World position of this voyage's destination.</summary>
        Vector3 Destination { get; }

        /// <summary>Whole seconds of briefing left (0 once underway).</summary>
        int BriefingSecondsLeft { get; }

        /// <summary>Any peer: ask the server to start a new voyage.</summary>
        void RequestRestart();

        /// <summary>Server: true once per restart request since the last call.</summary>
        bool ConsumeRestartRequest();

        void ServerSetPhase(VoyagePhase phase);
        void ServerSetVoyage(int voyage);
        void ServerSetDestination(Vector3 destination);
        void ServerSetBriefingSecondsLeft(int seconds);
    }
}
