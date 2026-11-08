#nullable enable
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>
    /// Infrastructure Layer: one gate, whatever it does. The server spawns it (<see cref="GateUseCase"/>) and marks it
    /// used; the child named <c>Glow</c> shows while it is unused, on every peer. It is never moved, so it needs no
    /// transform sync.
    /// </summary>
    public class GateNetworkMediator : NetworkBehaviour, IGate
    {
        public const string GlowName = "Glow";

        private readonly NetworkVariable<bool> _used = new();
        private GameObject? _glow;

        public Transform? Transform => this != null ? transform : null;
        public bool IsUsed => _used.Value;

        public void ServerMarkUsed()
        {
            if (IsServer) _used.Value = true;
        }

        private void Awake() => _glow = transform.Find(GlowName)?.gameObject;

        public override void OnNetworkSpawn()
        {
            _used.OnValueChanged += OnUsedChanged;
            ShowGlow(!_used.Value);
        }

        public override void OnNetworkDespawn() => _used.OnValueChanged -= OnUsedChanged;

        private void OnUsedChanged(bool previous, bool used) => ShowGlow(!used);

        private void ShowGlow(bool shown)
        {
            if (_glow != null) _glow.SetActive(shown);
        }
    }
}
