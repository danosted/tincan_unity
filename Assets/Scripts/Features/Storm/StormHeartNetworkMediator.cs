#nullable enable
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Infrastructure Layer: the storm's heart on an encounter zone's object (added to the zone prefab by TinCan > Dev >
    /// Storm > Build Heart). Replicates the storm's strength, the heart's height and the last target slot hit; where the
    /// heart and its targets are comes from <see cref="StormHeartProcessor"/> on every peer.
    /// </summary>
    public sealed class StormHeartNetworkMediator : NetworkBehaviour, IStormHeart
    {
        private readonly NetworkVariable<float> _strength = new(1f);
        private readonly NetworkVariable<float> _altitude = new(0f);
        private readonly NetworkVariable<long> _hitSlot = new(-1);

        public float Strength => _strength.Value;
        public float Altitude => _altitude.Value;
        public long HitSlot => _hitSlot.Value;

        public void ServerReset(float altitude)
        {
            if (!IsServer) return;
            _strength.Value = 1f;
            _hitSlot.Value = -1;
            _altitude.Value = altitude;
        }

        public void ServerHit(long slot, float strength)
        {
            if (!IsServer) return;
            _hitSlot.Value = slot;
            _strength.Value = Mathf.Clamp01(strength);
        }
    }
}
