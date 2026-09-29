#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// What a player holds: one item at a time, server-authoritative. Holding an item grants what its
    /// <see cref="ItemDefinition"/> describes (see <see cref="EquipmentAbilityBinder"/>); visuals are the mediator's job.
    /// </summary>
    public interface IEquipment
    {
        ItemDefinition? Held { get; }
        bool IsEmptyHanded => Held == null;
        bool IsHolding(ItemDefinition? item) => item != null && Held == item;

        /// <summary>Server only. Fails when something is already held or the item is not in the catalog.</summary>
        bool TryEquip(ItemDefinition item);

        /// <summary>Server only. Fails when nothing is held.</summary>
        bool TryUnequip();
    }

    public static class EquipmentLocator
    {
        /// <summary>Resolves the equipment of an interaction requester (the player actor or a sibling component).</summary>
        public static IEquipment? Resolve(IActor? requester) => requester switch
        {
            IEquipment equipment => equipment,
            Component component when component != null => component.GetComponent<IEquipment>(),
            _ => null
        };
    }
}
