using Unity.Netcode;
using TinCan.Features.Events;
using VContainer;
using UnityEngine;
using System.Collections.Generic;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Synchronizes active events and timers across the network.
    /// </summary>
    public class CoordinatedEventMediator : NetworkBehaviour
    {
        [SerializeField] private List<CoordinatedEventDefinition> _definitions;

        private IEventOrchestrator _orchestrator;

        private readonly NetworkVariable<int> _netEventIndex = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _netRemainingTime = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _netIsActive = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Inject]
        public void Construct(IEventOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _orchestrator.OnEventStarted += HandleServerEventStarted;
                _orchestrator.OnEventEnded += HandleServerEventEnded;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _orchestrator != null)
            {
                _orchestrator.OnEventStarted -= HandleServerEventStarted;
                _orchestrator.OnEventEnded -= HandleServerEventEnded;
            }
        }

        private void Update()
        {
            if (IsServer && _orchestrator.IsEventActive)
            {
                _netRemainingTime.Value = _orchestrator.RemainingTime;
            }
        }

        private void HandleServerEventStarted(CoordinatedEventDefinition definition)
        {
            int index = _definitions.IndexOf(definition);
            if (index == -1)
            {
                Debug.LogError($"[CoordinatedEventMediator] Event {definition.name} not found in mediator definitions list!");
                return;
            }
            _netEventIndex.Value = index;
            _netIsActive.Value = true;
            _netRemainingTime.Value = definition.Duration;
        }

        private void HandleServerEventEnded(CoordinatedEventDefinition definition, bool success)
        {
            _netIsActive.Value = false;
            _netEventIndex.Value = -1;
        }

        /// <summary>
        /// Debug RPC to trigger an event from any client (if permitted).
        /// </summary>
        [Rpc(SendTo.Server)]
        public void TriggerEventRpc(int index)
        {
            if (index >= 0 && index < _definitions.Count)
            {
                _orchestrator.TriggerEvent(_definitions[index]);
            }
        }
    }
}
