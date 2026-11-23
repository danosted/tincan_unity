#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Items;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// A tent: pitched from a tent roll, packed up again. A tent a player set down (<see cref="RemovesWhenPacked"/>) stands
    /// pitched and is gone once packed. Server-owned. Core's ItemInteractionHandler does both: a held tent roll pitches an
    /// empty spot (<see cref="IItemConsumer"/>), and empty hands pack a pitched tent into a roll (<see cref="IItemYield"/>).
    /// </summary>
    public interface ITentSpot : IItemConsumer, IItemYield
    {
        CampingConfig? Config { get; }

        bool IsPitched { get; }

        /// <summary>Is this tent taken away when it is packed (a placed tent), rather than leaving a free spot?</summary>
        bool RemovesWhenPacked { get; }

        /// <summary>Server only. False when a tent stands here already.</summary>
        bool TryPitch();

        /// <summary>Server only. False when there is no tent to pack.</summary>
        bool TryPack();

        /// <summary>Server only. Takes the tent out of the world.</summary>
        void Remove();
    }
}
