#nullable enable
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Infrastructure Layer: the crystal rack (jerry-can crate). Server-written count, replicated to everyone;
    /// child objects named "Can_*" are shown while their index is below the count. Interacting routes to
    /// core's ItemInteractionHandler via IA_TakeJerryCan (cans out and back in). Lives under the FuelSystem fixture or the fuel.crystal_rack
    /// fitting; it reads the initial supply and the can item from its own FuelConfig, because a fitting is spawned
    /// before it is parented to the ship.
    /// </summary>
    public class JerryCanSupplyNetworkMediator : NetworkBehaviour, IInteractionTarget, IJerryCanSupply, IItemYield, IItemStore
    {
        private const string CanVisualPrefix = "Can_";

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("The fuel tunables: the initial supply and the can item.")]
        [SerializeField] private FuelConfig? _config;

        private readonly NetworkVariable<int> _count = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private StockVisuals? _cans;

        public InteractionDefinition Definition => _interactionDefinition!;
        public int Count => _count.Value;
        public TinCan.Core.Items.ItemDefinition? Item => _config != null ? _config.JerryCanItem : null;

        private void Awake() => _cans = new StockVisuals(transform, CanVisualPrefix);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer && _config != null) _count.Value = Mathf.Max(0, _config.InitialSupply);

            _count.OnValueChanged += HandleCountChanged;
            ApplyVisuals(Count);
        }

        public override void OnNetworkDespawn()
        {
            _count.OnValueChanged -= HandleCountChanged;
            base.OnNetworkDespawn();
        }

        // Cans out into empty hands and back in, through core's ItemInteractionHandler.
        public ItemDefinition? YieldItem => Item;
        public ItemDefinition? StoreItem => Item;
        public bool TryYield() => TryTake();
        public void Unyield() => Add(1);

        public bool TryStore()
        {
            if (!IsServer) return false;
            Add(1);
            return true;
        }

        public bool TryTake()
        {
            if (!IsServer || _count.Value <= 0) return false;
            _count.Value -= 1;
            return true;
        }

        public void Add(int amount)
        {
            if (!IsServer || amount <= 0) return;
            _count.Value += amount;
        }

        private void HandleCountChanged(int previous, int current) => ApplyVisuals(current);

        private void ApplyVisuals(int count) => _cans?.Show(count);
    }
}
