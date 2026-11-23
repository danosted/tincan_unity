#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Core.Items
{
    // What a target can do with items, as small capabilities. A target implements the ones it has, and the one item
    // handler (ItemInteractionHandler) does the rest: features never write their own take, put-back or hand-over logic.
    // Plan and rule: .docs/CODE_STANDARDS.md, "Interactions: capabilities, not handlers".

    /// <summary>
    /// Hands out one item at a time into empty hands: a rack (never runs out), a vein or a woodpile (runs out, grows
    /// back), a bin (runs out). Server-side writes.
    /// </summary>
    public interface IItemYield : IInteractable
    {
        /// <summary>The item it hands out now; null when it has nothing to give (a tent not pitched).</summary>
        ItemDefinition? YieldItem { get; }

        /// <summary>Server: takes one out; false when none is left.</summary>
        bool TryYield();

        /// <summary>Server: puts back one taken out that could not be handed over.</summary>
        void Unyield();
    }

    /// <summary>Takes its own item back in: a rack, a woodpile, a bin. Server-side writes.</summary>
    public interface IItemStore : IInteractable
    {
        /// <summary>The item it takes in; null when it takes nothing now.</summary>
        ItemDefinition? StoreItem { get; }

        /// <summary>Server: takes one in; false when full.</summary>
        bool TryStore();
    }

    /// <summary>
    /// Uses a held item it accepts, which is then gone: the receiver burns fuel crystals and jerry cans, a fire burns
    /// firewood, a tent spot is pitched with a tent roll. Server-side writes.
    /// </summary>
    public interface IItemConsumer : IInteractable
    {
        /// <summary>Whether it takes this kind of item at all (right now).</summary>
        bool Accepts(ItemDefinition item);

        /// <summary>Server: uses one; false when it cannot now (a full tank, a full fire), and the item stays in hand.</summary>
        bool TryConsume(ItemDefinition item);
    }
}
