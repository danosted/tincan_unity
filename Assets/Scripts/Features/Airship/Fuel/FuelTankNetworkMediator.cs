#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Infrastructure Layer: the ship's fuel tank. Stores the level as the Attr_Fuel gameplay attribute on the
    /// airship's ability controller (replicated by AbilityNetworkMediator), so clients read it for free and only
    /// the server ever writes it. Lives on the FuelSystem child of the airship prefab.
    /// </summary>
    public class FuelTankNetworkMediator : NetworkBehaviour, IFuelTank
    {
        [SerializeField] private GameplayAttribute? _fuelAttribute;
        [SerializeField] private FuelConfig? _config;

        private FuelAttributeSet? _attributes;
        private readonly FuelConsumptionProcessor _processor = new();

        public FuelConfig? Config => _config;
        public float Capacity => _config != null ? _config.Capacity : 0f;
        public float Level => _attributes?.Level ?? 0f;
        public bool IsEmpty => Level <= 0f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            var controller = GetComponentInParent<IAbilityControllerBase>();
            if (controller == null || _fuelAttribute == null || _config == null)
            {
                Debug.LogWarning($"[{nameof(FuelTankNetworkMediator)}] Missing ability controller on a parent, fuel attribute or config; tank disabled.", this);
                return;
            }

            _attributes = new FuelAttributeSet(controller, _fuelAttribute);

            if (IsServer && !_attributes.HasValue)
            {
                _attributes.SetLevel(_processor.ClampLevel(_config.InitialLevel, _config.Capacity));
            }
        }

        public void Consume(float amount)
        {
            if (!IsServer || _attributes == null || amount <= 0f) return;
            _attributes.SetLevel(_processor.ClampLevel(Level - amount, Capacity));
        }

        public float Refill(float amount)
        {
            if (!IsServer || _attributes == null || amount <= 0f) return 0f;

            float before = Level;
            float after = _processor.ClampLevel(before + amount, Capacity);
            if (Mathf.Approximately(before, after)) return 0f;

            _attributes.SetLevel(after);
            return after - before;
        }
    }
}
