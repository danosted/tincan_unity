#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Airship stand-in backed by a real GameObject so child fixtures (fuel tank, gauge) can be found the way
    /// production code finds them. Call Destroy() in TearDown.
    /// </summary>
    public sealed class FakeAirshipView : IAirshipView, IShipState, IAirshipTuning
    {
        private readonly GameObject _gameObject;

        public FakeAirshipView(string name = "FakeAirship", IAbilityControllerBase? controller = null)
        {
            _gameObject = new GameObject(name);
            Controller = controller!;
        }

        public GameObject GameObject => _gameObject;
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public AirshipInputState InputState { get; set; }
        public IAbilityControllerBase Controller { get; set; }
        public Transform Transform => _gameObject.transform;
        public float MaxForwardSpeed => 10f;
        public float MaxBackwardSpeed => 5f;
        public float AccelerationRate => 5f;
        public float DecelerationRate => 5f;
        public float AngularAcceleration => 5f;
        public float AngularDeceleration => 5f;
        public float VelocityBlendRate => 1f;
        public float TurnSpeed => 30f;
        public float PitchSpeed => 20f;
        public float MaxBankAngle => 20f;
        public float BankSpeed => 1f;
        public Vector3 Velocity => Vector3.zero;
        public Vector3 PositionDelta => Vector3.zero;
        public Quaternion RotationDelta => Quaternion.identity;

        /// <summary>The last base stats a ship design set (IAirshipTuning), or null.</summary>
        public (float MaxForwardSpeed, float TurnSpeed, float MaxHealth)? BaseStats { get; private set; }

        public void ServerSetBaseStats(float maxForwardSpeed, float turnSpeed, float maxHealth) =>
            BaseStats = (maxForwardSpeed, turnSpeed, maxHealth);

        public void Destroy()
        {
            if (_gameObject != null) UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        /// <summary>The last velocity the movement use case applied.</summary>
        public Vector3 AppliedVelocity { get; private set; }

        public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity) => AppliedVelocity = velocity;
        public void Simulate(float deltaTime) { }
        public Vector3 GetPointVelocity(Vector3 worldPoint) => Vector3.zero;
    }
}
