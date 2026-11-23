#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Infrastructure Layer: the hand crank fitting (fuel.crank). An Interact target so it shows in reach with its prompt;
    /// the turning itself is a Primary press read by <see cref="CrankTurnUseCase"/>, not Interact. The turn count is
    /// replicated, and its child "Handle" turns a quarter round its local Z for each turn, on every peer.
    /// </summary>
    public class FuelCrankNetworkMediator : NetworkBehaviour, IFuelCrank
    {
        private const string HandleName = "Handle";
        private const float DegreesPerTurn = 90f;

        [SerializeField] private InteractionDefinition? _interactionDefinition;

        private readonly NetworkVariable<int> _turns = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private Transform? _handle;
        private Quaternion _handleRest;

        public InteractionDefinition Definition => _interactionDefinition!;
        public IFuelTank? Tank => FuelTankLocator.FindFor(this);
        public int Turns => _turns.Value;

        public void ServerTurned()
        {
            if (IsServer) _turns.Value++;
        }

        public override void OnNetworkSpawn()
        {
            _handle = transform.FindDescendant(HandleName);
            if (_handle != null) _handleRest = _handle.localRotation;
            _turns.OnValueChanged += OnTurned;
            ShowHandle(_turns.Value);
        }

        public override void OnNetworkDespawn() => _turns.OnValueChanged -= OnTurned;

        private void OnTurned(int _, int turns) => ShowHandle(turns);

        private void ShowHandle(int turns)
        {
            if (_handle != null) _handle.localRotation = _handleRest * Quaternion.Euler(0f, 0f, turns % 4 * DegreesPerTurn);
        }
    }
}
