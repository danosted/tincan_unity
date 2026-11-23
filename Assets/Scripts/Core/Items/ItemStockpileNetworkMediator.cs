#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// A store aboard for one kind of item that counts what it holds (a crystal bin): put the item in, take one out.
    /// Unlike a rack it runs out. Others may spend from it on the server (the ship's shield) through
    /// <see cref="IItemYield.TryYield"/>.
    /// </summary>
    public interface IItemStockpile : IItemYield, IItemStore
    {
        int Count { get; }
        int Capacity { get; }
    }

    /// <summary>
    /// Infrastructure Layer: a stockpile fitting. Interacting routes to <see cref="ItemInteractionHandler"/> through
    /// its InteractionDefinition. The count is replicated, its children "Stock_00", "Stock_01", ... show one per item
    /// held (as many as there are), and its HUD prompt reads "{the interaction's prompt} {count}/{capacity}".
    /// </summary>
    public class ItemStockpileNetworkMediator : NetworkBehaviour, IInteractionTarget, IItemStockpile, IInteractionPrompt
    {
        private const string StockPrefix = "Stock_";

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [SerializeField] private ItemDefinition? _item;
        [SerializeField, Min(1)] private int _capacity = 12;

        private readonly NetworkVariable<int> _count = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private StockVisuals? _stock;

        public InteractionDefinition Definition => _interactionDefinition!;
        public ItemDefinition? YieldItem => _item;
        public ItemDefinition? StoreItem => _item;
        public int Count => _count.Value;
        public int Capacity => _capacity;

        public string Prompt => _interactionDefinition != null && _interactionDefinition.Prompt.Length > 0
            ? $"{_interactionDefinition.Prompt} {Count}/{Capacity}"
            : string.Empty;

        public bool TryStore()
        {
            if (!IsServer || _count.Value >= _capacity) return false;
            _count.Value++;
            return true;
        }

        public bool TryYield()
        {
            if (!IsServer || _count.Value <= 0) return false;
            _count.Value--;
            return true;
        }

        public void Unyield() => TryStore();

        public override void OnNetworkSpawn()
        {
            _count.OnValueChanged += OnCountChanged;
            ShowStock(_count.Value);
        }

        public override void OnNetworkDespawn() => _count.OnValueChanged -= OnCountChanged;

        private void OnCountChanged(int _, int count) => ShowStock(count);

        private void ShowStock(int count) => (_stock ??= new StockVisuals(transform, StockPrefix)).Show(count);
    }
}
