#nullable enable
using System.Collections.Generic;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Infrastructure Layer: a pile of deadwood at a campsite. It replicates its stock and the server time it was counted
    /// at; the logs that have gathered since are worked out when read (<see cref="DeadwoodProcessor"/>), so it needs no
    /// tick. Child objects named "Log_*" show while their index is below what is there. Interacting routes to
    /// <see cref="GatherFirewoodInteractionHandler"/> through IA_GatherFirewood.
    /// </summary>
    public class DeadwoodNetworkMediator : NetworkBehaviour, IInteractionTarget, IDeadwood
    {
        private const string LogVisualPrefix = "Log_";

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("The camping tunables: how many logs a pile holds and how fast it gathers again.")]
        [SerializeField] private CampingConfig? _config;

        private readonly NetworkVariable<int> _stock = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> _stockAt = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<GameObject> _logVisuals = new();
        private int _shown = -1;

        public InteractionDefinition Definition => _interactionDefinition!;
        public CampingConfig? Config => _config;
        public int Available => Settled().Stock;

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(LogVisualPrefix)) _logVisuals.Add(child.gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && _config != null)
            {
                _stock.Value = _config.DeadwoodLogs;
                _stockAt.Value = Now;
            }
            ShowLogs(Available);
        }

        private void Update()
        {
            if (!IsSpawned) return;
            int available = Available;
            if (available != _shown) ShowLogs(available);
        }

        public bool TryTake()
        {
            if (!IsServer || _config == null) return false;

            var taken = DeadwoodProcessor.Take(_stock.Value, _stockAt.Value, Now, _config.DeadwoodLogs, _config.DeadwoodRegrowSeconds);
            if (taken is not { } stock) return false;
            Write(stock);
            return true;
        }

        public void Return()
        {
            if (!IsServer || _config == null) return;
            Write(DeadwoodProcessor.Return(_stock.Value, _stockAt.Value, Now, _config.DeadwoodLogs, _config.DeadwoodRegrowSeconds));
        }

        private (int Stock, double At) Settled() => _config == null
            ? (0, 0d)
            : DeadwoodProcessor.Settle(_stock.Value, _stockAt.Value, Now, _config.DeadwoodLogs, _config.DeadwoodRegrowSeconds);

        private void Write((int Stock, double At) stock)
        {
            _stock.Value = stock.Stock;
            _stockAt.Value = stock.At;
        }

        private void ShowLogs(int count)
        {
            _shown = count;
            for (int i = 0; i < _logVisuals.Count; i++) _logVisuals[i].SetActive(i < count);
        }

        private double Now => NetworkManager != null && IsSpawned ? NetworkManager.ServerTime.Time : 0d;
    }
}
