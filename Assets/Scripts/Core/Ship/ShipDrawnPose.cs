#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Where to draw a ship simulated on this peer (the host) between ticks. Simulation moves it at the tick rate
    /// (30 Hz); drawn there it steps on a high-refresh display, unevenly with VSync. This blends from the pose before the
    /// last tick to the simulated pose by the tick clock, so the drawn ship trails the simulation by up to one tick, as
    /// an interpolated ship does on a client. Times are on the network tick clock (<c>NetworkManager.LocalTime</c>).
    /// </summary>
    public sealed class ShipDrawnPose
    {
        public const float StaleAfter = 0.25f;

        private Vector3 _fromPosition;
        private Quaternion _fromRotation = Quaternion.identity;
        private float _tickTime = float.NegativeInfinity;

        /// <summary>A tick is about to move the ship from this pose; <paramref name="tickTime"/> is that tick's time.</summary>
        public void BeginTick(Vector3 position, Quaternion rotation, float tickTime)
        {
            _fromPosition = position;
            _fromRotation = rotation;
            _tickTime = tickTime;
        }

        /// <summary>The ship jumped (a teleport): draw it there at once, no blend from where it was.</summary>
        public void Snap(Vector3 position, Quaternion rotation)
        {
            _fromPosition = position;
            _fromRotation = rotation;
        }

        /// <summary>False when no tick moved the ship lately (not simulated here): draw it where it is.</summary>
        public bool IsActive(float now) => now - _tickTime <= StaleAfter;

        /// <summary>The pose to draw at <paramref name="now"/>, toward the simulated pose (<paramref name="position"/>, <paramref name="rotation"/>).</summary>
        public void Evaluate(Vector3 position, Quaternion rotation, float now, float tickInterval, out Vector3 drawnPosition, out Quaternion drawnRotation)
        {
            float t = tickInterval > 0f ? Mathf.Clamp01((now - _tickTime) / tickInterval) : 1f;
            drawnPosition = Vector3.Lerp(_fromPosition, position, t);
            drawnRotation = Quaternion.Slerp(_fromRotation, rotation, t);
        }
    }
}
