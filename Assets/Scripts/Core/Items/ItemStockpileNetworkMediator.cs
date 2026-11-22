#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// A store aboard for one kind of item that counts what it holds (a crystal bin): put the item in, take one out.
    /// Unlike a rack (<see cref="IItemSource"/>) it runs out. Others may spend from it on the server (the ship's shield).
    /// </summary>
    public interface IItemStockpile : IInteractable
    {
        ItemDefinition? Item { get; }
        int Count { get; }
        int Capacity { get; }

        /// <summary>Server only: adds one; false when full.</summary>
        bool ServerTryAdd();

        /// <summary>Server only: removes one; false when empty.</summary>
        bool ServerTryTake();
    }

    /// <summary>
    /// Infrastructure Layer: a stockpile fitting. Interacting routes to <see cref="StockItemInteractionHandler"/> through
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
        private Transform[]? _stock;

        public InteractionDefinition Definition => _interactionDefinition!;
        public ItemDefinition? Item => _item;
        public int Count => _count.Value;
        public int Capacity => _capacity;

        public string Prompt => _interactionDefinition != null && _interactionDefinition.Prompt.Length > 0
            ? $"{_interactionDefinition.Prompt} {Count}/{Capacity}"
            : string.Empty;

        public bool ServerTryAdd()
        {
            if (!IsServer || _count.Value >= _capacity) return false;
            _count.Value++;
            return true;
        }

        public bool ServerTryTake()
        {
            if (!IsServer || _count.Value <= 0) return false;
            _count.Value--;
            return true;
        }

        public override void OnNetworkSpawn()
        {
            _count.OnValueChanged += OnCountChanged;
            ShowStock(_count.Value);
        }

        public override void OnNetworkDespawn() => _count.OnValueChanged -= OnCountChanged;

        private void OnCountChanged(int _, int count) => ShowStock(count);

        private void ShowStock(int count)
        {
            _stock ??= FindStock();
            for (int i = 0; i < _stock.Length; i++) _stock[i].gameObject.SetActive(i < count);
        }

        private Transform[] FindStock()
        {
            var found = new System.Collections.Generic.List<Transform>();
            for (int i = 0; ; i++)
            {
                var child = transform.FindDescendant($"{StockPrefix}{i:00}");
                if (child == null) break;
                found.Add(child);
            }

            return found.ToArray();
        }
    }
}
