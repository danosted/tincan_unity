#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Items
{
    /// <summary>Something on the ship that hands out one kind of item (a tool rack). Unlimited: it never runs out.</summary>
    public interface IItemSource : IInteractable
    {
        ItemDefinition? Item { get; }
    }

    /// <summary>
    /// Infrastructure Layer: a rack that hands out the item named on it. Interacting routes to
    /// <see cref="TakeItemInteractionHandler"/> through its InteractionDefinition (e.g. IA_TakeRepairTool). It holds no
    /// state: what a player holds is their equipment's state. New tools need only a rack prefab and an item asset.
    /// </summary>
    public class ItemRackNetworkMediator : NetworkBehaviour, IInteractionTarget, IItemSource
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [SerializeField] private ItemDefinition? _item;

        public InteractionDefinition Definition => _interactionDefinition!;
        public ItemDefinition? Item => _item;
    }
}
