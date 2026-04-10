using System;
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// View/Infrastructure Layer: Unity-specific implementation using CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HumanoidControllerView : ControllableActorBase, IHumanoidMovementView
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 7f;
        [SerializeField] private float _sprintMultiplier = 1.8f;
        [SerializeField] private float _jumpForce = 8f;
        [SerializeField] private float _gravity = 20f;

        private CharacterController _controller;

        public Transform Transform => transform;
        public bool IsActive => IsControlsEnabled;
        public bool IsGrounded => _controller.isGrounded;
        public float WalkSpeed => _walkSpeed;
        public float SprintMultiplier => _sprintMultiplier;
        public float JumpForce => _jumpForce;
        public float Gravity => _gravity;

        public Quaternion LookRotation
        {
            get
            {
                // Return horizontal rotation based on current yaw from a look view if available
                var lookView = GetComponent<IHumanoidLookView>();
                return lookView != null ? Quaternion.Euler(0, lookView.Yaw, 0) : transform.rotation;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponent<CharacterController>();
        }

        public void Move(Vector3 motion)
        {
            _controller.Move(motion);
        }

        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
        }

    }
}
