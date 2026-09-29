#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Infrastructure Layer: the player's held item, as an item id the server writes. On every change, every peer
    /// shows the held item's visual (the item's <see cref="ItemDefinition.HeldVisual"/> prefab, instantiated once under
    /// the player's <c>Visual</c> and then shown or hidden), and the server and the owning client apply its grants
    /// through <see cref="EquipmentAbilityBinder"/>. Proxies only draw.
    /// </summary>
    public class EquipmentNetworkMediator : NetworkBehaviour, IEquipment
    {
        private const string LogSource = "Items";
        private const string VisualRootName = "Visual";

        private readonly NetworkVariable<int> _heldId = new(
            ItemCatalog.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Dictionary<ItemDefinition, GameObject> _visuals = new();

        private ItemCatalog? _catalog;
        private EquipmentAbilityBinder? _binder;
        private IEventPublisher? _events;

        public ItemDefinition? Held => _catalog?.Find(_heldId.Value);

        // Core services: the Items installer always loads.
        [Inject]
        public void Construct(ItemCatalog catalog, EquipmentAbilityBinder binder, IEventPublisher events)
        {
            _catalog = catalog;
            _binder = binder;
            _events = events;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _heldId.OnValueChanged += HandleHeldChanged;
            Apply(ItemCatalog.None, _heldId.Value);
        }

        public override void OnNetworkDespawn()
        {
            _heldId.OnValueChanged -= HandleHeldChanged;
            base.OnNetworkDespawn();
        }

        public bool TryEquip(ItemDefinition item)
        {
            if (!IsServer || _catalog == null || _heldId.Value != ItemCatalog.None) return false;

            if (!_catalog.Contains(item))
            {
                _events?.LogWarning(LogSource, $"{item.name} is not in the item catalog; its feature's installer must contribute it and be in this scene's profile.");
                return false;
            }

            _heldId.Value = item.Id;
            return true;
        }

        public bool TryUnequip()
        {
            if (!IsServer || _heldId.Value == ItemCatalog.None) return false;
            _heldId.Value = ItemCatalog.None;
            return true;
        }

        private void HandleHeldChanged(int previous, int current) => Apply(previous, current);

        private void Apply(int previous, int current)
        {
            var item = _catalog?.Find(current);
            ApplyVisuals(item);

            if (!IsServer && !IsOwner) return;
            var controller = GetComponent<IAbilityControllerBase>();
            if (controller != null) _binder?.Bind(controller, item);

            if (IsServer && previous != current)
            {
                _events?.Publish(new HeldItemChangedEvent(controller?.Id ?? default, Describe(previous), item?.name ?? "none"));
            }
        }

        private void ApplyVisuals(ItemDefinition? held)
        {
            foreach (var pair in _visuals)
            {
                if (pair.Value != null) pair.Value.SetActive(pair.Key == held);
            }

            if (held == null || held.HeldVisual == null || _visuals.ContainsKey(held)) return;

            // First time this item is held here: build its visual. Named after the prefab so it can be found by name
            // (scenarios check "Carry_Net" and friends).
            var parent = transform.FindDescendant(VisualRootName) ?? transform;
            var visual = Instantiate(held.HeldVisual, parent, false);
            visual.name = held.HeldVisual.name;
            visual.SetActive(true);
            _visuals[held] = visual;
        }

        private string Describe(int id) => _catalog?.Find(id)?.name ?? (id == ItemCatalog.None ? "none" : $"#{id}");
    }
}
