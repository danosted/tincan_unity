#nullable enable
using System;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Infrastructure Layer: a gas pocket spawned by a gas field (with an EntityNetworkMediator, which registers it as an
    /// actor, and its <see cref="GasPocketVolume"/>). The server's detonation is replicated: every peer, late joiners
    /// included, hides the cloud (<see cref="GasPocketVolume.VisualChildName"/>) and plays the "Burst" particle child if
    /// the prefab has one. A world object: never parented, no motion.
    /// </summary>
    [RequireComponent(typeof(GasPocketVolume))]
    public class GasPocketNetworkMediator : NetworkBehaviour, IGasPocket
    {
        private const string BurstChildName = "Burst";

        private readonly NetworkVariable<bool> _detonated = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private GasPocketVolume _volume = null!;
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;
        public Vector3 Center => _volume.Center;
        public float Radius => _volume.Radius;
        public bool Detonated => _detonated.Value;
        public GasPocketVolume Volume => _volume;

        private void Awake()
        {
            _volume = GetComponent<GasPocketVolume>();
            _volume.Detonated += OnVolumeDetonated;
        }

        public override void OnNetworkSpawn()
        {
            _detonated.OnValueChanged += OnDetonatedChanged;
            if (_detonated.Value) Show(detonated: true, burst: false);
        }

        public override void OnNetworkDespawn() => _detonated.OnValueChanged -= OnDetonatedChanged;

        public override void OnDestroy()
        {
            if (_volume != null) _volume.Detonated -= OnVolumeDetonated;
            base.OnDestroy();
        }

        private void OnVolumeDetonated(GasPocketVolume volume)
        {
            if (IsServer && IsSpawned) _detonated.Value = true;
        }

        private void OnDetonatedChanged(bool previous, bool current)
        {
            if (current && !previous) Show(detonated: true, burst: true);
        }

        private void Show(bool detonated, bool burst)
        {
            // Clients learn of it here, so their volume (and the chart) agrees with the server's.
            if (detonated) _volume.MarkDetonated();

            var visual = transform.Find(GasPocketVolume.VisualChildName);
            if (visual != null) visual.gameObject.SetActive(!detonated);
            if (!burst) return;

            var particles = transform.Find(BurstChildName)?.GetComponent<ParticleSystem>();
            if (particles != null) particles.Play(true);
        }
    }
}
