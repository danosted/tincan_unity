#nullable enable
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>One gate in the sky. Its used state is replicated, so every peer (late joiners too) sees a used gate go dark.</summary>
    public interface IGate
    {
        Transform? Transform { get; }
        bool IsUsed { get; }

        /// <summary>Server: the ship passed through; the gate does nothing more.</summary>
        void ServerMarkUsed();
    }
}
