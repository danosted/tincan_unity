using System;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.FreeCamera;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Fake look view; unused by tests that keep the character uncaptured (isCaptured == false).
    /// </summary>
    public class FakeOrbitalLookView : IOrbitalLookView
    {
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Sensitivity => 1f;
        public float MaxPitch => 90f;
        public Camera Camera => null;
        public void ApplyLook(float pitch, float yaw) { }
    }

    /// <summary>
    /// Real-Transform-backed movement view. RefreshSensing performs an actual Physics.Raycast
    /// (works in EditMode without Play Mode), avoiding any need to fabricate a RaycastHit.
    /// </summary>
    public class FakeHumanoidMovementView : IHumanoidMovementView
    {
        private readonly GameObject _gameObject;
        private GroundData _currentGround;
        private RaycastHit? _lastGroundHit;

        public float WalkSpeed { get; set; } = 5f;
        public float SprintMultiplier { get; set; } = 1.5f;
        public float JumpForce { get; set; } = 8f;
        public float Gravity { get; set; } = 0f;
        public Quaternion LookRotation { get; set; } = Quaternion.identity;
        public bool IsControlsEnabled { get; private set; } = true;
        public float GroundProbeDistance { get; set; } = 1f;

        public Transform Transform => _gameObject.transform;
        public GroundData CurrentGround => _currentGround;
        public RaycastHit? LastGroundHit => _lastGroundHit;

        public FakeHumanoidMovementView(string name)
        {
            _gameObject = new GameObject(name);
        }

        public void Destroy() => UnityEngine.Object.DestroyImmediate(_gameObject);

        public void RefreshSensing()
        {
            if (Physics.Raycast(Transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, GroundProbeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                _lastGroundHit = hit;
                _currentGround.IsGrounded = true;
            }
            else
            {
                _lastGroundHit = null;
                _currentGround.IsGrounded = false;
            }
        }

        public void Move(Vector3 motion) => Transform.position += motion;
        public void SetRotation(Quaternion rotation) => Transform.rotation = rotation;
        public void SetPose(Vector3 position, Quaternion rotation) => Transform.SetPositionAndRotation(position, rotation);
        public void UpdateGroundData(GroundData data) => _currentGround = data;

        public void EnableControls() => IsControlsEnabled = true;
        public void DisableControls() => IsControlsEnabled = false;
    }

    /// <summary>
    /// Minimal IHumanoidCharacterView fake: uncaptured (PossessorId null) so tests exercise the
    /// server/observer simulation path without needing InputService realism.
    /// </summary>
    public class FakeHumanoidCharacterView : IHumanoidCharacterView
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public HumanoidInputState InputState { get; set; }
        public ulong? PossessorId { get; private set; }
        public IHumanoidMovementView Movement { get; }
        public IOrbitalLookView Look { get; } = new FakeOrbitalLookView();
        public GameplayTagContainer ActiveTags => new GameplayTagContainer(null);

        public FakeHumanoidCharacterView(FakeHumanoidMovementView movement)
        {
            Movement = movement;
        }

        public void AuthoritativeSetPossessor(ulong? playerId) => PossessorId = playerId;
        public bool CanPossess(ulong playerId) => true;

        public bool HasTag(GameplayTag tag) => false;
        public void AddTag(GameplayTag tag) { }
        public void RemoveTag(GameplayTag tag) { }

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value)
        {
            value = default;
            return false;
        }

        public void SetAttribute(GameplayAttribute attribute, AttributeValue value) { }
        public void ResetAttributesToBase() { }

        public void GrantAbility(IAbilityDefinition definition) { }
        public void RemoveAbility(IAbilityDefinition definition) { }
        public bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase target = null) => false;
        public void HandleGameplayEvent(GameplayEventData eventData) { }

        public HumanoidAttributeSet GetAttributeSet() => null;
    }
}
