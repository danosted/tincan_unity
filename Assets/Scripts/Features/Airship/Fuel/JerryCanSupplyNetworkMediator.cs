#nullable enable
using System.Collections.Generic;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Infrastructure Layer: the crystal rack (jerry-can crate). Server-written count, replicated to everyone;
    /// child objects named "Can_*" are shown while their index is below the count. Interacting routes to
    /// TakeJerryCanInteractionHandler via IA_TakeJerryCan. Lives under the FuelSystem fixture or the fuel.crystal_rack
    /// fitting; it reads the initial supply and the can item from its own FuelConfig, because a fitting is spawned
    /// before it is parented to the ship.
    /// </summary>
    public class JerryCanSupplyNetworkMediator : NetworkBehaviour, IInteractionTarget, IJerryCanSupply
    {
        private const string CanVisualPrefix = "Can_";

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("The fuel tunables: the initial supply and the can item.")]
        [SerializeField] private FuelConfig? _config;

        private readonly NetworkVariable<int> _count = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<GameObject> _canVisuals = new();

        public InteractionDefinition Definition => _interactionDefinition!;
        public int Count => _count.Value;
        public TinCan.Core.Items.ItemDefinition? Item => _config != null ? _config.JerryCanItem : null;

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(CanVisualPrefix)) _canVisuals.Add(child.gameObject);
            }
        }

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

        private void ApplyVisuals(int count)
        {
            for (int i = 0; i < _canVisuals.Count; i++)
            {
                _canVisuals[i].SetActive(i < count);
            }
        }
    }
}
