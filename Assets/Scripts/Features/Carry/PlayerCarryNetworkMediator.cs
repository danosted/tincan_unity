#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Carry
{
    /// <summary>
    /// Infrastructure Layer: virtual carry state on the player prefab. The server writes the carried item; every peer
    /// toggles the matching child visual (Carry_JerryCan / Carry_Net). While a net is carried the server also keeps
    /// the carrying-net gameplay tag on the player so abilities can require it.
    /// </summary>
    public class PlayerCarryNetworkMediator : NetworkBehaviour, ICarrier
    {
        private const string JerryCanVisualName = "Carry_JerryCan";
        private const string NetVisualName = "Carry_Net";

        [SerializeField] private GameplayTag? _carryingNetTag;

        private readonly NetworkVariable<byte> _carried = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private GameObject? _jerryCanVisual;
        private GameObject? _netVisual;

        public CarriedItem Carried => (CarriedItem)_carried.Value;

        private void Awake()
        {
            _jerryCanVisual = transform.Find(JerryCanVisualName)?.gameObject;
            _netVisual = transform.Find(NetVisualName)?.gameObject;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _carried.OnValueChanged += HandleCarriedChanged;
            ApplyVisuals(Carried);
        }

        public override void OnNetworkDespawn()
        {
            _carried.OnValueChanged -= HandleCarriedChanged;
            base.OnNetworkDespawn();
        }

        public bool TryPickUp(CarriedItem item)
        {
            if (!IsServer || item == CarriedItem.None || Carried != CarriedItem.None) return false;
            _carried.Value = (byte)item;
            return true;
        }

        public bool TryDrop()
        {
            if (!IsServer || Carried == CarriedItem.None) return false;
            _carried.Value = (byte)CarriedItem.None;
            return true;
        }

        private void HandleCarriedChanged(byte previous, byte current)
        {
            ApplyVisuals((CarriedItem)current);
            UpdateNetTag((CarriedItem)previous, (CarriedItem)current);
        }

        private void ApplyVisuals(CarriedItem item)
        {
            if (_jerryCanVisual != null) _jerryCanVisual.SetActive(item == CarriedItem.JerryCan);
            if (_netVisual != null) _netVisual.SetActive(item == CarriedItem.Net);
        }

        private void UpdateNetTag(CarriedItem previous, CarriedItem current)
        {
            if (!IsServer || _carryingNetTag == null || previous == current) return;

            var controller = GetComponent<IAbilityControllerBase>();
            if (controller == null) return;

            switch (wasNet: previous == CarriedItem.Net, isNet: current == CarriedItem.Net)
            {
                case (wasNet: false, isNet: true):
                    controller.AddTag(_carryingNetTag);
                    break;
                case (wasNet: true, isNet: false):
                    controller.RemoveTag(_carryingNetTag);
                    break;
            }
        }
    }
}
