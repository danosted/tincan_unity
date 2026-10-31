#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Server: one gas field at a time, laid ahead of the ship on demand (designed events, scenarios).</summary>
    public interface IGasField
    {
        /// <summary>The pockets of the current field (detonated ones included), until it is cleared away.</summary>
        IReadOnlyList<IGasPocket> Pockets { get; }

        /// <summary>
        /// Replaces any current field with <paramref name="count"/> pockets about <paramref name="ahead"/> metres ahead of the
        /// ship, across <paramref name="spread"/> metres either side. Returns how many were placed (rock can drop some).
        /// </summary>
        int Spawn(int count, float ahead, float spread);

        /// <summary>Every pocket of the field has gone off, or the ship has left it behind (true with no field).</summary>
        bool IsCleared { get; }

        /// <summary>Despawns the field's pockets.</summary>
        void Clear();
    }
}
