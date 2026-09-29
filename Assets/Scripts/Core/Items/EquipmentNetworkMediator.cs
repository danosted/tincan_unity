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
    /// shows the held item's visual (a child named by <see cref="ItemDefinition.VisualName"/>), and the server and the
    /// owning client apply its grants through <see cref="EquipmentAbilityBinder"/>. Proxies only draw.
    /// </summary>
    public class EquipmentNetworkMediator : NetworkBehaviour, IEquipment
    {
        private const string LogSource = "Items";

        private readonly NetworkVariable<int> _heldId = new(
            ItemCatalog.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Dictionary<string, GameObject?> _visuals = new();

        private ItemCatalog? _catalog;
        private EquipmentAbilityBinder? _binder;
        private IEventPublisher? _events;

        public ItemDefinition? Held => _catalog?.Find(_heldId.Value);

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            // Registered by ItemsFeatureInstaller. Without it the player can hold nothing; say so instead of failing
            // the whole player's injection.
            resolver.TryResolve(out _catalog);
            resolver.TryResolve(out _binder);
            resolver.TryResolve(out _events);
            if (_catalog == null) Debug.LogError("[Items] No ItemCatalog registered; is ItemsFeatureInstaller in the scene's feature profile?", this);
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
                _events?.LogWarning(LogSource, $"{item.name} is not in the item catalog; add it to ItemsFeatureInstaller.");
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
            if (_catalog == null) return;

            foreach (var item in _catalog.All)
            {
                if (string.IsNullOrEmpty(item.VisualName)) continue;
                var visual = FindVisual(item.VisualName);
                if (visual != null) visual.SetActive(item == held);
            }
        }

        private GameObject? FindVisual(string visualName)
        {
            if (_visuals.TryGetValue(visualName, out var cached)) return cached;
            var found = transform.FindDescendant(visualName)?.gameObject;
            _visuals[visualName] = found;
            return found;
        }

        private string Describe(int id) => _catalog?.Find(id)?.name ?? (id == ItemCatalog.None ? "none" : $"#{id}");
    }
}
