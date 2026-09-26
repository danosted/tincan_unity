#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.DevTools
{
    /// <summary>One frame of the locally controlled character, with its position in platform-local space.</summary>
    public readonly struct MovementSample
    {
        public readonly float Time;
        public readonly bool HasMoveInput;
        public readonly bool IsJumping;
        public readonly Vector3 LocalPosition;
        public readonly bool FrameChanged;
        public readonly bool PlatformMoving;

        public MovementSample(float time, bool hasMoveInput, bool isJumping, Vector3 localPosition, bool frameChanged, bool platformMoving)
        {
            Time = time;
            HasMoveInput = hasMoveInput;
            IsJumping = isJumping;
            LocalPosition = localPosition;
            FrameChanged = frameChanged;
            PlatformMoving = platformMoving;
        }
    }

    /// <summary>
    /// Measures how the locally controlled character responds to its own input, frame by frame:
    /// start latency (input on until it visibly moves), stop latency (input off until it visibly slows),
    /// jump latency (jump pressed until it visibly rises), snaps (jumps faster than any legal motion) and
    /// reversals (direction flips while input is steady, the signature of prediction fighting a correction).
    /// Everything is split by whether the platform underneath is moving.
    /// </summary>
    public sealed class MovementResponseTracker
    {
        public const float MoveThreshold = 0.02f;
        public const float RiseThreshold = 0.05f;
        public const float SpeedWindow = 0.1f;
        public const float StopSpeedRatio = 0.5f;
        public const float Timeout = 2f;
        public const float SteadyInputTime = 0.3f;
        public const float ReversalMinStep = 0.01f;
        public const float MinStopReferenceSpeed = 0.2f;
        public const float IdleSettleTime = 0.5f;

        private readonly float _maxLegalSpeed;
        private readonly Bucket _still = new();
        private readonly Bucket _moving = new();
        private readonly Queue<(float Time, Vector3 Position)> _window = new();

        private bool _hasPrevious;
        private MovementSample _previous;
        private Vector3 _previousStep;
        private float _lastInputChange;

        private Pending? _start;
        private Pending? _stop;
        private Pending? _jump;

        public MovementResponseTracker(float maxLegalSpeed) => _maxLegalSpeed = maxLegalSpeed;

        public int Frames { get; private set; }

        public void Add(MovementSample sample)
        {
            Frames++;

            if (!_hasPrevious || sample.FrameChanged)
            {
                Reset(sample);
                return;
            }

            var bucket = sample.PlatformMoving ? _moving : _still;
            float deltaTime = Math.Max(1e-4f, sample.Time - _previous.Time);
            Vector3 step = sample.LocalPosition - _previous.LocalPosition;

            TrackSnapsAndReversals(bucket, sample, step, deltaTime);
            TrackIdleDrift(bucket, sample, step, deltaTime);
            TrackWindow(sample);
            TrackInputEdges(sample);
            ResolvePending(sample);

            _previousStep = step;
            _previous = sample;
        }

        public ResponseSummary Summarise(bool platformMoving)
        {
            var bucket = platformMoving ? _moving : _still;
            return new ResponseSummary
            {
                start = bucket.Start.Summarise(),
                stop = bucket.Stop.Summarise(),
                jump = bucket.Jump.Summarise(),
                snaps = bucket.Snaps,
                maxSnapM = (float)Math.Round(bucket.MaxSnap, 3),
                reversals = bucket.Reversals,
                idleSeconds = (float)Math.Round(bucket.IdleSeconds, 1),
                idleDriftCmPerS = bucket.IdleSeconds > 0f ? (float)Math.Round(bucket.IdleDrift / bucket.IdleSeconds * 100f, 1) : 0f
            };
        }

        private void Reset(MovementSample sample)
        {
            _hasPrevious = true;
            _previous = sample;
            _previousStep = Vector3.zero;
            _lastInputChange = sample.Time;
            _window.Clear();
            _window.Enqueue((sample.Time, sample.LocalPosition));
            _start = _stop = _jump = null;
        }

        private void TrackSnapsAndReversals(Bucket bucket, MovementSample sample, Vector3 step, float deltaTime)
        {
            float allowed = _maxLegalSpeed * deltaTime + 0.05f;
            float distance = step.magnitude;
            if (distance > allowed)
            {
                bucket.Snaps++;
                bucket.MaxSnap = Math.Max(bucket.MaxSnap, distance - allowed);
            }

            Vector3 flat = Flat(step);
            Vector3 previousFlat = Flat(_previousStep);
            bool steadyInput = sample.Time - _lastInputChange > SteadyInputTime;
            if (steadyInput && flat.magnitude > ReversalMinStep && previousFlat.magnitude > ReversalMinStep &&
                Vector3.Dot(flat, previousFlat) < 0f)
            {
                bucket.Reversals++;
            }
        }

        /// <summary>Horizontal creep while standing still (no input for <see cref="IdleSettleTime"/>): deck sliding.</summary>
        private void TrackIdleDrift(Bucket bucket, MovementSample sample, Vector3 step, float deltaTime)
        {
            if (sample.HasMoveInput || sample.IsJumping || sample.Time - _lastInputChange < IdleSettleTime) return;
            if (_stop != null || _jump != null) return;

            bucket.IdleSeconds += deltaTime;
            bucket.IdleDrift += Flat(step).magnitude;
        }

        private void TrackWindow(MovementSample sample)
        {
            _window.Enqueue((sample.Time, sample.LocalPosition));
            while (_window.Count > 2 && sample.Time - _window.Peek().Time > SpeedWindow) _window.Dequeue();
        }

        private void TrackInputEdges(MovementSample sample)
        {
            var bucket = sample.PlatformMoving ? _moving : _still;

            if (sample.HasMoveInput != _previous.HasMoveInput)
            {
                _lastInputChange = sample.Time;
                if (sample.HasMoveInput)
                {
                    _start = new Pending(sample.Time, sample.LocalPosition, 0f, bucket.Start);
                    _stop = null;
                }
                else
                {
                    // Releasing while not yet visibly moving says nothing about stopping; the start stays pending.
                    float speed = WindowSpeedAt(sample);
                    if (speed >= MinStopReferenceSpeed)
                    {
                        _stop = new Pending(sample.Time, sample.LocalPosition, speed, bucket.Stop);
                        _start = null;
                    }
                }
            }

            if (sample.IsJumping && !_previous.IsJumping)
            {
                _jump = new Pending(sample.Time, sample.LocalPosition, 0f, bucket.Jump);
            }
        }

        private void ResolvePending(MovementSample sample)
        {
            _start = Resolve(_start, sample, p => Flat(sample.LocalPosition - p.Origin).magnitude > MoveThreshold);
            _stop = Resolve(_stop, sample, p => WindowSpeedAt(sample) < p.Reference * StopSpeedRatio);
            _jump = Resolve(_jump, sample, p => sample.LocalPosition.y - p.Origin.y > RiseThreshold);
        }

        private float WindowSpeedAt(MovementSample sample)
        {
            if (_window.Count < 2) return 0f;

            var oldest = _window.Peek();
            float span = Math.Max(1e-4f, sample.Time - oldest.Time);
            return Flat(sample.LocalPosition - oldest.Position).magnitude / span;
        }

        private static Pending? Resolve(Pending? pending, MovementSample sample, Func<Pending, bool> reached)
        {
            if (pending == null) return null;

            float elapsed = sample.Time - pending.Started;
            if (reached(pending))
            {
                pending.Stats.Add(elapsed * 1000f);
                return null;
            }

            if (elapsed <= Timeout) return pending;

            pending.Stats.AddTimeout();
            return null;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);

        private sealed class Pending
        {
            public readonly float Started;
            public readonly Vector3 Origin;
            public readonly float Reference;
            public readonly LatencyStats Stats;

            public Pending(float started, Vector3 origin, float reference, LatencyStats stats)
            {
                Started = started;
                Origin = origin;
                Reference = reference;
                Stats = stats;
            }
        }

        private sealed class Bucket
        {
            public readonly LatencyStats Start = new();
            public readonly LatencyStats Stop = new();
            public readonly LatencyStats Jump = new();
            public int Snaps;
            public float MaxSnap;
            public int Reversals;
            public float IdleSeconds;
            public float IdleDrift;
        }
    }

    [Serializable]
    public struct ResponseSummary
    {
        public LatencySummary start;
        public LatencySummary stop;
        public LatencySummary jump;
        public int snaps;
        public float maxSnapM;
        public int reversals;
        public float idleSeconds;
        public float idleDriftCmPerS;
    }
}
