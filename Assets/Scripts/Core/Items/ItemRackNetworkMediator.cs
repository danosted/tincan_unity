#nullable enable
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Infrastructure Layer: a rack that hands out the item named on it and takes it back, and never runs out (a tool
    /// rack, the grapple rack, the net rack). Interacting routes to <see cref="ItemInteractionHandler"/> through its
    /// InteractionDefinition (e.g. IA_TakeRepairTool). It holds no state: what a player holds is their equipment's state.
    /// New tools need only a rack prefab and an item asset.
    /// </summary>
    public class ItemRackNetworkMediator : NetworkBehaviour, IInteractionTarget, IItemYield, IItemStore
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [SerializeField] private ItemDefinition? _item;

        public InteractionDefinition Definition => _interactionDefinition!;
        public ItemDefinition? YieldItem => _item;
        public ItemDefinition? StoreItem => _item;

        public bool TryYield() => true;
        public void Unyield() { }
        public bool TryStore() => true;
    }
}
