#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.Gathering
{
    /// <summary>A crystal vein in island rock: mined one crystal at a time, it grows them back over time. Server-owned.</summary>
    public interface IVein : IInteractable
    {
        GatheringConfig? Config { get; }

        /// <summary>Crystals there are to mine right now.</summary>
        int Available { get; }

        /// <summary>Server only. Takes one crystal; false when the vein is bare.</summary>
        bool TryTake();

        /// <summary>Server only. Puts one crystal back (a mined crystal that could not be handed over).</summary>
        void Return();
    }
}
