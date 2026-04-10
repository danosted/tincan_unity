using System;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Pure C# domain logic for airship physics calculations.
    /// This class has no dependency on Unity (UnityEngine).
    /// </summary>
    public class AirshipEngine
    {
        public float CurrentThrottle { get; private set; }
        public float CurrentPitch { get; private set; }
        
        private readonly float _maxThrottle;
        private readonly float _throttleAcceleration;

        public AirshipEngine(float maxThrottle = 100f, float acceleration = 10f)
        {
            _maxThrottle = maxThrottle;
            _throttleAcceleration = acceleration;
        }

        public void ApplyThrottle(float input, float deltaTime)
        {
            float target = input * _maxThrottle;
            CurrentThrottle = Math.Clamp(
                CurrentThrottle + (target - CurrentThrottle) * _throttleAcceleration * deltaTime,
                0, _maxThrottle
            );
        }

        public void SetPitch(float pitch)
        {
            CurrentPitch = pitch;
        }
    }
}
