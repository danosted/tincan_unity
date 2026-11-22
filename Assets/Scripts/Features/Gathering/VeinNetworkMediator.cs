#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Gathering
{
    /// <summary>
    /// Infrastructure Layer: a crystal vein, placed on an island by Island Details. It replicates its stock and the server
    /// time it was counted at; the crystals grown since are worked out when read (<see cref="RegrowingStock"/>), so it needs
    /// no tick. Child objects named "Crystal_*" show while their index is below what is there. Interacting routes to
    /// <see cref="MineCrystalInteractionHandler"/> through IA_MineCrystal, whose hold time is the mining.
    /// </summary>
    public class VeinNetworkMediator : NetworkBehaviour, IInteractionTarget, IVein
    {
        private const string CrystalVisualPrefix = "Crystal_";

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("The gathering tunables: how many crystals a vein holds and how fast it grows them back.")]
        [SerializeField] private GatheringConfig? _config;

        private readonly NetworkVariable<int> _stock = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _stockAt = new(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<GameObject> _crystalVisuals = new();
        private int _shown = -1;

        public InteractionDefinition Definition => _interactionDefinition!;
        public GatheringConfig? Config => _config;
        public int Available => Settled().Stock;

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(CrystalVisualPrefix)) _crystalVisuals.Add(child.gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && _config != null)
            {
                _stock.Value = _config.VeinCrystals;
                _stockAt.Value = Now;
            }
            ShowCrystals(Available);
        }

        private void Update()
        {
            if (!IsSpawned) return;
            int available = Available;
            if (available != _shown) ShowCrystals(available);
        }

        public bool TryTake()
        {
            if (!IsServer || _config == null) return false;

            var taken = RegrowingStock.Take(_stock.Value, _stockAt.Value, Now, _config.VeinCrystals, _config.VeinRegrowSeconds);
            if (taken is not { } stock) return false;
            Write(stock);
            return true;
        }

        public void Return()
        {
            if (!IsServer || _config == null) return;
            Write(RegrowingStock.Return(_stock.Value, _stockAt.Value, Now, _config.VeinCrystals, _config.VeinRegrowSeconds));
        }

        private (int Stock, double At) Settled() => _config == null
            ? (0, 0d)
            : RegrowingStock.Settle(_stock.Value, _stockAt.Value, Now, _config.VeinCrystals, _config.VeinRegrowSeconds);

        private void Write((int Stock, double At) stock)
        {
            _stock.Value = stock.Stock;
            _stockAt.Value = stock.At;
        }

        private void ShowCrystals(int count)
        {
            _shown = count;
            for (int i = 0; i < _crystalVisuals.Count; i++) _crystalVisuals[i].SetActive(i < count);
        }

        private double Now => NetworkManager != null && IsSpawned ? NetworkManager.ServerTime.Time : 0d;
    }
}
