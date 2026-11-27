#nullable enable
using UnityEngine;

namespace TinCan.DevTools
{
    /// <summary>
    /// Counts frames where something moved unevenly on screen: a frame whose speed is off the mean of the frames
    /// either side of it by more than half of that (or of the recent average speed, if higher): a frozen or doubled
    /// frame. Comparing with neighbours lets smooth speeding up and slowing down pass. Only counted while the recent
    /// average speed is over <see cref="MovingSpeed"/>. A body drawn at the 30 Hz tick on a 144 Hz screen scores dozens per second; one drawn
    /// smoothly scores about zero.
    /// </summary>
    public sealed class FramePacingMeter
    {
        public const float MovingSpeed = 2f;
        public const float JumpSpeed = 500f;
        public const float AverageTime = 0.25f;

        private float _averageSpeed;
        private Vector3 _last;
        private int _samples;
        private float _speedBefore;
        private float _speedMiddle;
        private float _middleDeltaTime;

        public int UnevenFrames { get; private set; }
        public float MovingSeconds { get; private set; }
        public float UnevenPerSecond => MovingSeconds > 0f ? UnevenFrames / MovingSeconds : 0f;

        public void Sample(Vector3 position, float deltaTime)
        {
            if (_samples == 0 || deltaTime <= 0f)
            {
                _last = position;
                _samples = 1;
                return;
            }

            float speed = Vector3.Distance(position, _last) / deltaTime;
            _last = position;

            // A jump (teleport, respawn) is not pacing: start over from here.
            if (speed > JumpSpeed)
            {
                _samples = 1;
                return;
            }

            // speeds: before, middle, this one. Judge the middle frame against its two neighbours, while the slow
            // average says it is moving at all (a ship stepping at 30 Hz has frozen frames whose neighbours are frozen too).
            if (_samples >= 3 && _averageSpeed > MovingSpeed)
            {
                float neighbours = 0.5f * (_speedBefore + speed);
                MovingSeconds += _middleDeltaTime;
                if (Mathf.Abs(_speedMiddle - neighbours) > 0.5f * Mathf.Max(neighbours, _averageSpeed)) UnevenFrames++;
            }

            _averageSpeed = _samples == 1 ? speed : Mathf.Lerp(_averageSpeed, speed, Mathf.Clamp01(deltaTime / AverageTime));
            _speedBefore = _speedMiddle;
            _speedMiddle = speed;
            _middleDeltaTime = deltaTime;
            _samples = Mathf.Min(_samples + 1, 3);
        }

        public string Describe() => MovingSeconds > 0f
            ? $"{UnevenPerSecond:F1} uneven/s over {MovingSeconds:F1} s moving"
            : "not moving";
    }
}
