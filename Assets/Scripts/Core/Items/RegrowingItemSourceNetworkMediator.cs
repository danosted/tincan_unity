#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>A source that holds a few of an item and grows them back in time; read on every peer.</summary>
    public interface IRegrowingItemSource : IItemYield
    {
        /// <summary>How many there are to take right now.</summary>
        int Available { get; }

        int Capacity { get; }
    }

    /// <summary>
    /// Infrastructure Layer: anything in the world that gives an item and grows it back: a crystal vein (Gathering), a
    /// pile of deadwood under a tree (Camping). It replicates its stock and the server time it was counted at; what has
    /// grown since is worked out when read (<see cref="RegrowingStock"/>), so it needs no tick. Children whose names start
    /// with <see cref="_visualPrefix"/> show while their index is below what is there. Its interaction (with an
    /// <see cref="ItemInteractionHandler"/>; a hold time makes it work, like mining) does the taking; with
    /// <see cref="_takesBack"/> the item can be put back. The item and the numbers are set on the prefab by its builder.
    /// </summary>
    public class RegrowingItemSourceNetworkMediator : NetworkBehaviour, IInteractionTarget, IRegrowingItemSource, IItemStore
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [SerializeField] private ItemDefinition? _item;
        [Tooltip("How many it holds when full (and when it spawns).")]
        [SerializeField, Min(1)] private int _capacity = 3;
        [Tooltip("Seconds to grow one back.")]
        [SerializeField, Min(1f)] private float _regrowSeconds = 120f;
        [Tooltip("Whether a held item of its kind can be put back (a woodpile yes, a vein no).")]
        [SerializeField] private bool _takesBack;
        [Tooltip("Children named with this prefix (Crystal_0, Log_1, ...) show one per item there.")]
        [SerializeField] private string _visualPrefix = "Stock_";

        private readonly NetworkVariable<int> _stock = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _stockAt = new(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private StockVisuals? _visuals;

        public InteractionDefinition Definition => _interactionDefinition!;
        public ItemDefinition? YieldItem => _item;
        public ItemDefinition? StoreItem => _takesBack ? _item : null;
        public int Available => Settled().Stock;
        public int Capacity => _capacity;

        private void Awake() => _visuals = new StockVisuals(transform, _visualPrefix);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                _stock.Value = _capacity;
                _stockAt.Value = Now;
            }
            _visuals?.Show(Available);
        }

        private void Update()
        {
            if (IsSpawned) _visuals?.Show(Available);
        }

        public bool TryYield()
        {
            if (!IsServer) return false;
            var taken = RegrowingStock.Take(_stock.Value, _stockAt.Value, Now, _capacity, _regrowSeconds);
            if (taken is not { } stock) return false;
            Write(stock);
            return true;
        }

        public void Unyield() => Return();

        public bool TryStore()
        {
            if (!IsServer || !_takesBack || Available >= _capacity) return false;
            Return();
            return true;
        }

        private void Return()
        {
            if (IsServer) Write(RegrowingStock.Return(_stock.Value, _stockAt.Value, Now, _capacity, _regrowSeconds));
        }

        private (int Stock, double At) Settled() => RegrowingStock.Settle(_stock.Value, _stockAt.Value, Now, _capacity, _regrowSeconds);

        private void Write((int Stock, double At) stock)
        {
            _stock.Value = stock.Stock;
            _stockAt.Value = stock.At;
        }

        private double Now => NetworkManager != null && IsSpawned ? NetworkManager.ServerTime.Time : 0d;
    }
}
