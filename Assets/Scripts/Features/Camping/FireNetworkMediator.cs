#nullable enable
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Infrastructure Layer: a fire (a campfire at a campsite, or the hearth fitting). It replicates one value, the server
    /// time it goes out, so it burns down on every peer with no tick and a late joiner sees it as it is. Interacting
    /// routes to <see cref="StokeFireInteractionHandler"/> through IA_StokeFire; <see cref="FireView"/> draws it.
    /// </summary>
    public class FireNetworkMediator : NetworkBehaviour, IInteractionTarget, IFire
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("The camping tunables: how long a log burns and how much the fire holds.")]
        [SerializeField] private CampingConfig? _config;

        private readonly NetworkVariable<double> _outAt = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public InteractionDefinition Definition => _interactionDefinition!;
        public CampingConfig? Config => _config;
        public float BurnSecondsLeft => FireProcessor.Remaining(Now, _outAt.Value);
        public bool IsLit => BurnSecondsLeft > 0f;

        /// <summary>How full the fire is, 0 to 1, for the view.</summary>
        public float Fullness => FireProcessor.Fullness(BurnSecondsLeft, _config != null ? _config.MaxBurnSeconds : 0f);

        public float Stoke(float seconds)
        {
            if (!IsServer || _config == null) return 0f;

            var (outAt, accepted) = FireProcessor.Stoke(Now, _outAt.Value, seconds, _config.MaxBurnSeconds);
            if (accepted > 0f) _outAt.Value = outAt;
            return accepted;
        }

        private double Now => NetworkManager != null && IsSpawned ? NetworkManager.ServerTime.Time : 0d;
    }
}
