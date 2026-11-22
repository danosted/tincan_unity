#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.Camping
{
    /// <summary>A pile of deadwood that gives firewood and gathers it again over time. Server-owned.</summary>
    public interface IDeadwood : IInteractable
    {
        CampingConfig? Config { get; }

        /// <summary>Logs there are to take right now.</summary>
        int Available { get; }

        /// <summary>Server only. Takes one log; false when the pile is bare.</summary>
        bool TryTake();

        /// <summary>Server only. Puts one log back.</summary>
        void Return();
    }
}
