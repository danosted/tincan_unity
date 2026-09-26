#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Movement state expressed in a platform's local space (or world space when <see cref="Platform"/> is null).
    /// Used for both the server's authoritative state and the owner's recorded predictions, so they compare directly.
    /// </summary>
    public readonly struct HumanoidAuthoritativeState
    {
        public readonly uint Sequence;
        public readonly byte TeleportEpoch;
        public readonly Transform? Platform;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 LocalHorizontalVelocity;
        public readonly float VerticalVelocity;

        public HumanoidAuthoritativeState(uint sequence, byte teleportEpoch, Transform? platform, Vector3 localPosition, Vector3 localHorizontalVelocity, float verticalVelocity)
        {
            Sequence = sequence;
            TeleportEpoch = teleportEpoch;
            Platform = platform;
            LocalPosition = localPosition;
            LocalHorizontalVelocity = localHorizontalVelocity;
            VerticalVelocity = verticalVelocity;
        }

        /// <summary>Expresses a world pose and velocity relative to <paramref name="platform"/>.</summary>
        public static HumanoidAuthoritativeState FromWorld(uint sequence, byte teleportEpoch, Transform? platform, Vector3 worldPosition, Vector3 worldHorizontalVelocity, float verticalVelocity)
        {
            if (platform == null) return new(sequence, teleportEpoch, null, worldPosition, worldHorizontalVelocity, verticalVelocity);

            var inverse = Quaternion.Inverse(platform.rotation);
            return new(sequence, teleportEpoch, platform, inverse * (worldPosition - platform.position), inverse * worldHorizontalVelocity, verticalVelocity);
        }

        public Vector3 WorldPosition => Platform != null ? Platform.position + Platform.rotation * LocalPosition : LocalPosition;
        public Vector3 WorldHorizontalVelocity => Platform != null ? Platform.rotation * LocalHorizontalVelocity : LocalHorizontalVelocity;
    }

    /// <summary>
    /// The owner's unacknowledged inputs with what it predicted after each, oldest first. On a divergent
    /// acknowledgement the remaining inputs are replayed from the server's state and their predictions replaced.
    /// </summary>
    public sealed class HumanoidPredictionHistory
    {
        public const int DefaultCapacity = 128; // ~4 s at 30 Hz: far longer than any sane round trip

        public struct Entry
        {
            public HumanoidInputState Input;
            public HumanoidAuthoritativeState State;
        }

        private readonly List<Entry> _entries = new();
        private readonly int _capacity;

        public HumanoidPredictionHistory(int capacity = DefaultCapacity) => _capacity = Math.Max(1, capacity);

        public int Count => _entries.Count;

        /// <summary>Unacknowledged entries, oldest first. Replay writes corrected states back by index.</summary>
        public IReadOnlyList<Entry> Entries => _entries;

        public void Record(HumanoidInputState input, HumanoidAuthoritativeState state)
        {
            _entries.Add(new Entry { Input = input, State = state });
            if (_entries.Count > _capacity) _entries.RemoveAt(0);
        }

        public bool TryGet(uint sequence, out HumanoidAuthoritativeState state)
        {
            foreach (var entry in _entries)
            {
                if (entry.State.Sequence != sequence) continue;

                state = entry.State;
                return true;
            }

            state = default;
            return false;
        }

        public void Replace(int index, HumanoidAuthoritativeState state)
        {
            var entry = _entries[index];
            entry.State = state;
            _entries[index] = entry;
        }

        /// <summary>Forgets every prediction up to and including <paramref name="sequence"/> (acknowledged by the server).</summary>
        public void DropThrough(uint sequence)
        {
            int count = 0;
            while (count < _entries.Count && _entries[count].State.Sequence <= sequence) count++;
            _entries.RemoveRange(0, count);
        }

        public void Clear() => _entries.Clear();
    }

    public enum ReconciliationAction
    {
        /// <summary>No prediction to compare (already acknowledged, or older than the history).</summary>
        Ignore,
        /// <summary>Prediction matches within tolerance.</summary>
        Match,
        /// <summary>Divergence: rewind to the server state and replay the unacknowledged inputs.</summary>
        Correct,
        /// <summary>Teleport, frame change or large divergence: jump to the server state.</summary>
        Snap
    }

    public readonly struct ReconciliationDecision
    {
        public readonly ReconciliationAction Action;
        public readonly Vector3 PositionError;
        public readonly Vector3 HorizontalVelocityError;
        public readonly float VerticalVelocityError;

        public ReconciliationDecision(ReconciliationAction action, Vector3 positionError = default, Vector3 horizontalVelocityError = default, float verticalVelocityError = 0f)
        {
            Action = action;
            PositionError = positionError;
            HorizontalVelocityError = horizontalVelocityError;
            VerticalVelocityError = verticalVelocityError;
        }
    }

    /// <summary>
    /// Compares the server's state after input N with what the owner predicted after input N, in the same platform
    /// frame, and decides: accept, rewind to the server state and replay the newer inputs, or snap.
    /// </summary>
    public sealed class HumanoidReconciliationProcessor
    {
        public const float PositionTolerance = 0.02f;
        public const float VelocityTolerance = 0.1f;
        public const float SnapDistance = 3f;

        public ReconciliationDecision Evaluate(HumanoidAuthoritativeState server, bool hasPrediction, HumanoidAuthoritativeState predicted, byte knownTeleportEpoch)
        {
            if (server.TeleportEpoch != knownTeleportEpoch) return new(ReconciliationAction.Snap);
            if (!hasPrediction) return new(ReconciliationAction.Ignore);
            if (server.Platform != predicted.Platform) return new(ReconciliationAction.Snap);

            Vector3 positionError = server.LocalPosition - predicted.LocalPosition;
            if (positionError.magnitude > SnapDistance) return new(ReconciliationAction.Snap);

            Vector3 velocityError = server.LocalHorizontalVelocity - predicted.LocalHorizontalVelocity;
            float verticalError = server.VerticalVelocity - predicted.VerticalVelocity;

            bool matches = positionError.magnitude <= PositionTolerance &&
                           velocityError.magnitude <= VelocityTolerance &&
                           Mathf.Abs(verticalError) <= VelocityTolerance;

            return matches
                ? new(ReconciliationAction.Match)
                : new(ReconciliationAction.Correct, positionError, velocityError, verticalError);
        }
    }

    /// <summary>Owner-side prediction health, read by telemetry.</summary>
    public sealed class HumanoidPredictionStats
    {
        public int Acks;
        public int Matches;
        public int Corrections;
        public int Snaps;
        public float MaxCorrection;
        public float CorrectionSum;
        public int AckLatencySamples;
        public float AckLatencySum;
        public float AckLatencyMax;

        public float MeanCorrection => Corrections > 0 ? CorrectionSum / Corrections : 0f;
        public float MeanAckLatency => AckLatencySamples > 0 ? AckLatencySum / AckLatencySamples : 0f;

        public void AddAckLatency(float milliseconds)
        {
            AckLatencySamples++;
            AckLatencySum += milliseconds;
            AckLatencyMax = Mathf.Max(AckLatencyMax, milliseconds);
        }
    }

    /// <summary>
    /// A humanoid whose owner predicts locally and whose server publishes authoritative state back. Implemented by
    /// the network mediator; the movement use case drives it.
    /// </summary>
    public interface IPredictedHumanoid
    {
        /// <summary>True on the owning client that is not also the server.</summary>
        bool IsLocallyPredicted { get; }

        /// <summary>True on the server for a player owned by a remote client.</summary>
        bool PublishesAuthoritativeState { get; }

        HumanoidPredictionStats PredictionStats { get; }

        /// <summary>Owner: takes the newest unprocessed server state, if one arrived since the last call.</summary>
        bool TryTakeAuthoritativeState(out HumanoidAuthoritativeState state);

        /// <summary>Server: sends the state after simulating <see cref="HumanoidAuthoritativeState.Sequence"/> to the owner.</summary>
        void PublishAuthoritativeState(in HumanoidAuthoritativeState state);
    }
}
