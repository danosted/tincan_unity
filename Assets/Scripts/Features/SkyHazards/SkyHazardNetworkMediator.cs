#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Gas;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Something drifting in the sky that the crew shoots down. Its health is GAS state on its own
    /// <see cref="Controller"/>; it is shot down when that health is depleted (<see cref="HealthQueries.IsDepleted"/>).
    /// </summary>
    public interface ISkyHazard
    {
        Transform? Transform { get; }
        IAbilityControllerBase? Controller { get; }
    }

    /// <summary>
    /// Infrastructure Layer: one sky hazard. It needs an AbilityNetworkMediator on the same GameObject (its GAS actor,
    /// found through <see cref="IAbilityControllerBase"/>); health is a <see cref="HealthAttributeSet"/> registered on it,
    /// so any damage effect (a cannonball) hurts it and every peer sees the replicated health. It is a targetable, so the
    /// cannonball sweep finds it by its collider. The server moves it (SkyHazardUseCase); a NetworkTransformMediator on
    /// the prefab carries the motion to clients.
    /// </summary>
    public class SkyHazardNetworkMediator : NetworkBehaviour, ISkyHazard, ITargetable
    {
        [SerializeField] private HealthAttribute? _healthAttribute;
        [SerializeField] private MaxHealthAttribute? _maxHealthAttribute;
        [Min(1f)] [SerializeField] private float _maxHealth = 100f;

        private IAbilityControllerBase? _controller;
        private HealthAttributeSet? _health;
        private bool _initialized;

        public Transform? Transform => this != null ? transform : null;
        public Vector3 AimPoint => transform.position;
        public bool IsTargetable => IsSpawned && !_controller.IsDepleted();
        public IAbilityControllerBase? Controller => _controller;

        private void Awake()
        {
            _controller = GetComponent<IAbilityControllerBase>();
            if (_controller == null) Debug.LogError($"[{nameof(SkyHazardNetworkMediator)}] {name} needs an AbilityNetworkMediator on the same GameObject.", this);
            if (_controller != null && _healthAttribute != null && _maxHealthAttribute != null)
            {
                _health = new HealthAttributeSet(_controller, _healthAttribute, _maxHealthAttribute);
                _controller.RegisterAttributeSet(_health);
            }
        }

        // Same as the damage point: seed health once both behaviours are spawned (their OnNetworkSpawn order is not fixed).
        private void Update()
        {
            if (!IsSpawned || _initialized || _health == null || _controller is not NetworkBehaviour { IsSpawned: true }) return;

            if (IsServer && _health.MaxHealth <= 0f) _health.InitializeBaseValues(_maxHealth);
            _initialized = _health.MaxHealth > 0f;
        }
    }
}
