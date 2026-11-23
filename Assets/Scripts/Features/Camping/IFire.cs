#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Items;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// A fire that burns firewood: a campfire, or the ship's hearth. Server-owned; every peer reads it. A held log is fed
    /// to it by core's ItemInteractionHandler (<see cref="IItemConsumer"/>): it takes firewood while it has room.
    /// </summary>
    public interface IFire : IItemConsumer
    {
        CampingConfig? Config { get; }

        /// <summary>Seconds of burning left.</summary>
        float BurnSecondsLeft { get; }

        bool IsLit => BurnSecondsLeft > 0f;

        /// <summary>Server only. Adds that many seconds of burning; returns the seconds the fire took (0: refused).</summary>
        float Stoke(float seconds);
    }
}
