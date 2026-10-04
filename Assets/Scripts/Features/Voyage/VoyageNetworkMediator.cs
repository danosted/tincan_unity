#nullable enable
using System;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Infrastructure Layer: the voyage's replicated state, on the VoyageState ship fixture (with an EntityNetworkMediator,
    /// which registers it as an actor). Server-written variables carry the phase, the voyage number, the destination, the
    /// briefing countdown and the session layout (seed, origin) to every peer, late joiners included; a server RPC takes
    /// Restart from any player.
    /// </summary>
    public class VoyageNetworkMediator : NetworkBehaviour, IVoyageState
    {
        private readonly NetworkVariable<byte> _phase = new(
            (byte)VoyagePhase.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _voyage = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _destination = new(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _briefingSecondsLeft = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _layoutSeed = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _origin = new(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private ActorIdentity? _identity;
        private bool _restartRequested;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public VoyagePhase Phase => (VoyagePhase)_phase.Value;
        public int Voyage => _voyage.Value;
        public Vector3 Destination => _destination.Value;
        public int BriefingSecondsLeft => _briefingSecondsLeft.Value;
        public int LayoutSeed => _layoutSeed.Value;
        public Vector3 Origin => _origin.Value;

        public void RequestRestart()
        {
            if (!IsSpawned) return;
            if (IsServer) _restartRequested = true;
            else RequestRestartRpc();
        }

        public bool ConsumeRestartRequest()
        {
            bool requested = _restartRequested;
            _restartRequested = false;
            return requested;
        }

        public void ServerSetPhase(VoyagePhase phase)
        {
            if (IsServer && _phase.Value != (byte)phase) _phase.Value = (byte)phase;
        }

        public void ServerSetVoyage(int voyage)
        {
            if (IsServer) _voyage.Value = voyage;
        }

        public void ServerSetDestination(Vector3 destination)
        {
            if (IsServer) _destination.Value = destination;
        }

        public void ServerSetLayout(int layoutSeed, Vector3 origin)
        {
            if (!IsServer) return;
            _origin.Value = origin;
            _layoutSeed.Value = layoutSeed;
        }

        public void ServerSetBriefingSecondsLeft(int seconds)
        {
            if (IsServer && _briefingSecondsLeft.Value != seconds) _briefingSecondsLeft.Value = seconds;
        }

        [Rpc(SendTo.Server)]
        private void RequestRestartRpc() => _restartRequested = true;
    }
}
