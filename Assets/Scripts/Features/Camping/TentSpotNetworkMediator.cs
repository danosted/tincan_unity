#nullable enable
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Infrastructure Layer: a tent. A placed one (set down from a tent roll) spawns pitched and despawns once packed. It
    /// replicates whether the tent stands; its child
    /// "Tent" shows while one does and "TentMarker" (the pegs) while none does. Interacting routes to core's
    /// ItemInteractionHandler through IA_PitchTent (pitch with a held roll, pack with empty hands).
    /// </summary>
    public class TentSpotNetworkMediator : NetworkBehaviour, IInteractionTarget, ITentSpot
    {
        private const string TentVisual = "Tent";
        private const string MarkerVisual = "TentMarker";

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("The camping tunables: the tent roll item.")]
        [SerializeField] private CampingConfig? _config;
        [Tooltip("A tent a player set down: it spawns pitched and is taken away once packed.")]
        [SerializeField] private bool _placed;

        private readonly NetworkVariable<bool> _pitched = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private GameObject? _tent;
        private GameObject? _marker;

        public InteractionDefinition Definition => _interactionDefinition!;
        public CampingConfig? Config => _config;
        public bool IsPitched => _pitched.Value;
        public bool RemovesWhenPacked => _placed;

        private void Awake()
        {
            _tent = transform.Find(TentVisual)?.gameObject;
            _marker = transform.Find(MarkerVisual)?.gameObject;
            ShowTent(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && _placed) _pitched.Value = true;
            _pitched.OnValueChanged += HandlePitchedChanged;
            ShowTent(_pitched.Value);
        }

        public override void OnNetworkDespawn()
        {
            _pitched.OnValueChanged -= HandlePitchedChanged;
            base.OnNetworkDespawn();
        }

        // A held tent roll pitches an empty spot; empty hands pack a pitched tent back into a roll, and a placed tent is
        // then taken away (core's ItemInteractionHandler).
        public bool Accepts(ItemDefinition item) => !IsPitched && item == TentItem;

        public bool TryConsume(ItemDefinition item) => Accepts(item) && TryPitch();

        public ItemDefinition? YieldItem => IsPitched ? TentItem : null;

        public bool TryYield()
        {
            if (!TryPack()) return false;
            if (_placed) Remove();
            return true;
        }

        public void Unyield() => TryPitch();

        private ItemDefinition? TentItem => _config != null ? _config.TentItem : null;

        public bool TryPitch()
        {
            if (!IsServer || _pitched.Value) return false;
            _pitched.Value = true;
            return true;
        }

        public bool TryPack()
        {
            if (!IsServer || !_pitched.Value) return false;
            _pitched.Value = false;
            return true;
        }

        public void Remove()
        {
            if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) return;
            NetworkObject.Despawn(true);
        }

        private void HandlePitchedChanged(bool previous, bool current) => ShowTent(current);

        private void ShowTent(bool pitched)
        {
            if (_tent != null) _tent.SetActive(pitched);
            if (_marker != null) _marker.SetActive(!pitched);
        }
    }
}
