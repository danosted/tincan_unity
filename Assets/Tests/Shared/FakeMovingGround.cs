using System;
using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Minimal moving-ground component with no Awake-time dependency, safe to use in EditMode tests.
    /// </summary>
    public class FakeMovingGround : MonoBehaviour, IPointVelocityMovingGround
    {
        public Vector3 Velocity { get; set; }
        public Vector3 PositionDelta { get; set; }
        public Quaternion RotationDelta { get; set; } = Quaternion.identity;

        public Vector3 GetPointVelocity(Vector3 worldPoint) => Velocity;
    }
}
