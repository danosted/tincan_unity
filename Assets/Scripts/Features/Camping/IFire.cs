#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.Camping
{
    /// <summary>A fire that burns firewood: a campfire, or the ship's hearth. Server-owned; every peer reads it.</summary>
    public interface IFire : IInteractable
    {
        CampingConfig? Config { get; }

        /// <summary>Seconds of burning left.</summary>
        float BurnSecondsLeft { get; }

        bool IsLit => BurnSecondsLeft > 0f;

        /// <summary>Server only. Adds that many seconds of burning; returns the seconds the fire took (0: refused).</summary>
        float Stoke(float seconds);
    }
}
