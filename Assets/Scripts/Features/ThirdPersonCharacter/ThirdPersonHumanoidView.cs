using System;
using UnityEngine;
using TinCan.Features.Possession;
using TinCan.Core.Domain;
using TinCan.Features.HumanoidMovement;
using System.Collections.Generic;

namespace TinCan.Features.ThirdPersonCharacter
{
    /// <summary>
    /// View/Infrastructure Layer: The master component (Facade) for a third-person character.
    /// Enforces the required components and coordinates their interaction.
    /// Primary IActor and IPossessable entry point for UseCases.
    /// </summary>
    [RequireComponent(typeof(HumanoidControllerView))]
    [RequireComponent(typeof(ThirdPersonLookView))]
    public class ThirdPersonHumanoidView : MonoBehaviour, IHumanoidCharacterView
    {
        private HumanoidControllerView _movement;
        private ThirdPersonLookView _look;
        private IPossessionReceiver[] _receivers;

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;

        public ulong? OwnerId { get; private set; }
        public ulong? PossessorId { get; private set; }

        public IHumanoidMovementView Movement => _movement;
        public IHumanoidLookView Look => _look;

        public bool CanPossess(ulong playerId)
        {
            return !OwnerId.HasValue || OwnerId == playerId;
        }

        private void Awake()
        {
            _movement = GetComponent<HumanoidControllerView>();
            _look = GetComponent<ThirdPersonLookView>();
            _receivers = GetComponentsInChildren<IPossessionReceiver>(true);

            ValidateSetup();
        }

        public void OnPossessed(ulong playerId)
        {
            OwnerId = playerId;
            PossessorId = playerId;

            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionReceiver)this) continue;
                receiver.OnPossessed(playerId);
            }
        }

        public void OnUnpossessed()
        {
            PossessorId = null;

            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionReceiver)this) continue;
                receiver.OnUnpossessed();
            }
        }

        public void Dispose()
        {
            // Registration is managed by LifetimeScope BuildCallback
        }

        private void ValidateSetup()
        {
            if (_movement == null) Debug.LogError($"[ThirdPersonHumanoidView] Missing Movement View on {gameObject.name}");
            if (_look == null) Debug.LogError($"[ThirdPersonHumanoidView] Missing Look View on {gameObject.name}");
        }
    }
}
