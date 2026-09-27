#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.Abilities;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Infrastructure Layer: one breakable spot inside the ShipDamageSockets fixture. It needs an AbilityNetworkMediator
    /// on the same GameObject (the point's own GAS actor; it lives in Assembly-CSharp, so it is found through
    /// <see cref="IAbilityControllerBase"/> rather than required by type). Health is a <see cref="HealthAttributeSet"/>
    /// on that controller, replicated with its attributes. Every peer shows the child named "Marker" while the point is
    /// broken. Its index is its order among the fixture's damage points.
    /// </summary>
    public class ShipDamagePointNetworkMediator : NetworkBehaviour, IShipDamagePoint, ITargetable
    {
        private const string MarkerName = "Marker";

        [SerializeField] private HealthAttribute? _healthAttribute;
        [SerializeField] private MaxHealthAttribute? _maxHealthAttribute;
        [Min(1f)] [SerializeField] private float _maxHealth = 100f;

        private GameObject? _marker;
        private IAbilityControllerBase? _controller;
        private HealthAttributeSet? _health;
        private bool _initialized;

        public int Index => transform.GetSiblingIndex();
        public Transform? Transform => this != null ? transform : null;
        public IAbilityControllerBase? Controller => _controller;

        // ITargetable: aim at the marker; the repair tool filters on State.Damaged, so a healthy part is never picked.
        public Guid TargetId { get; } = Guid.NewGuid();
        public Vector3 AimPoint => _marker != null ? _marker.transform.position : transform.position;
        public bool IsTargetable => IsSpawned;

        public float Health01 => _health == null || _health.MaxHealth <= 0f ? 1f : _health.HealthPercentage;
        public bool IsBroken => Health01 < 1f;

        private void Awake()
        {
            _marker = transform.FindDescendant(MarkerName)?.gameObject;
            _controller = GetComponent<IAbilityControllerBase>();
            if (_controller == null) Debug.LogError($"[{nameof(ShipDamagePointNetworkMediator)}] {name} needs an AbilityNetworkMediator on the same GameObject.", this);
            if (_controller != null && _healthAttribute != null && _maxHealthAttribute != null)
            {
                _health = new HealthAttributeSet(_controller, _healthAttribute, _maxHealthAttribute);
            }
        }

        // Attribute writes go into the ability mediator's NetworkList, so the server seeds health once both behaviours
        // are spawned rather than from OnNetworkSpawn, whose order between the two is not guaranteed. The marker follows
        // the replicated attribute each frame; five points make polling cheaper than wiring a change event.
        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer && !_initialized && _health != null && _controller is NetworkBehaviour { IsSpawned: true })
            {
                if (_health.MaxHealth <= 0f) _health.InitializeBaseValues(_maxHealth);
                _initialized = true;
            }

            if (_marker != null && _marker.activeSelf != IsBroken) _marker.SetActive(IsBroken);
        }
    }
}
