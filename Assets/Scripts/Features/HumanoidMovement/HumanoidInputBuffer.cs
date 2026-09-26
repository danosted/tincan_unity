#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Server-side queue of one remote player's inputs, keyed by <see cref="HumanoidInputState.Sequence"/>.
    /// The owner sends every tick's input (with redundant copies of the previous few), and the server consumes
    /// exactly one per simulation tick, so both sides step through the same input stream:
    /// <list type="bullet">
    /// <item>Duplicates and inputs at or before the last consumed sequence are ignored.</item>
    /// <item>A starved tick repeats the last input with its one-shot bits cleared (no double jump).</item>
    /// <item>Above <see cref="MaxQueued"/> the oldest inputs are skipped, but their one-shot bits (jump, ability
    /// presses) carry into the next consumed input, so a tap is never lost to a latency spike.</item>
    /// </list>
    /// </summary>
    public sealed class HumanoidInputBuffer
    {
        public const int DefaultMaxQueued = 4;

        private readonly SortedList<uint, HumanoidInputState> _queue = new();
        private HumanoidInputState _last;
        private bool _hasLast;
        private bool _carryJump;
        private ulong _carryMask;
        private HumanoidInputBufferStats _stats;

        public HumanoidInputBuffer(int maxQueued = DefaultMaxQueued) => MaxQueued = Math.Max(1, maxQueued);

        public int MaxQueued { get; }
        public int Depth => _queue.Count;
        public uint LastConsumedSequence { get; private set; }
        public HumanoidInputBufferStats Stats => _stats;

        public void Receive(IReadOnlyList<HumanoidInputState> inputs)
        {
            foreach (var input in inputs)
            {
                if (_hasLast && input.Sequence <= LastConsumedSequence) continue;
                if (_queue.ContainsKey(input.Sequence)) continue;

                _queue.Add(input.Sequence, input);
                _stats.Received++;
            }
        }

        /// <summary>The input to simulate this tick. Call exactly once per server simulation tick.</summary>
        public HumanoidInputState Consume()
        {
            _stats.Ticks++;
            _stats.DepthSum += _queue.Count;
            _stats.MaxDepth = Math.Max(_stats.MaxDepth, _queue.Count);

            if (_queue.Count == 0)
            {
                _stats.Starved++;
                var repeat = _last;
                repeat.IsJumping = false;
                return repeat;
            }

            while (_queue.Count > MaxQueued)
            {
                var skipped = _queue.Values[0];
                _queue.RemoveAt(0);
                _carryJump |= skipped.IsJumping;
                _carryMask |= skipped.ActiveInputMask;
                _stats.Skipped++;
            }

            var next = _queue.Values[0];
            _queue.RemoveAt(0);
            next.IsJumping |= _carryJump;
            next.ActiveInputMask |= _carryMask;
            _carryJump = false;
            _carryMask = 0;

            _last = next;
            _hasLast = true;
            LastConsumedSequence = next.Sequence;
            _stats.Consumed++;
            return next;
        }
    }

    [Serializable]
    public struct HumanoidInputBufferStats
    {
        public int Ticks;
        public int Received;
        public int Consumed;
        public int Starved;
        public int Skipped;
        public int MaxDepth;
        public long DepthSum;

        public float MeanDepth => Ticks > 0 ? (float)DepthSum / Ticks : 0f;
    }

    /// <summary>
    /// An actor whose server-side input is fed from a <see cref="HumanoidInputBuffer"/>. The movement use case calls
    /// <see cref="AdvanceInput"/> once per simulation tick before it reads <c>InputState</c>.
    /// </summary>
    public interface IBufferedInputSource
    {
        void AdvanceInput();
        HumanoidInputBufferStats InputBufferStats { get; }
    }
}
