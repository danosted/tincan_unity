#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Reads presses of one input bit, once each, from inside the movement step (an <see cref="IHumanoidMotionContributor"/>):
    /// each input is read once and in order, so a replay of an input already read never presses again, and a release the
    /// host sees only in a replay still counts. One per reader (the crank, camp placement); it keeps per-player state.
    /// </summary>
    public sealed class InputPresses
    {
        private readonly Dictionary<Guid, uint> _lastSequence = new();
        private readonly HashSet<Guid> _held = new();

        /// <summary>Whether <paramref name="bit"/> went down in this input, for this player, read for the first time.</summary>
        public bool Pressed(Guid player, in HumanoidInputState input, int bit)
        {
            if (bit < 0) return false;
            if (_lastSequence.TryGetValue(player, out uint last) && input.Sequence <= last) return false;
            _lastSequence[player] = input.Sequence;

            bool down = (input.ActiveInputMask & (1UL << bit)) != 0;
            if (!down)
            {
                _held.Remove(player);
                return false;
            }

            return _held.Add(player);
        }
    }
}
