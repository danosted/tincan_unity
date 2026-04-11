using Unity.Netcode;
using UnityEngine;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Mediator that wraps a HumanoidControllerView to provide networking capabilities.
    /// This allows the HumanoidMovementUseCase to remain network-agnostic.
    /// </summary>
    public class HumanoidMovementNetworkMediator : NetworkMediator, IHumanoidMovementView
    {
        [SerializeField] private HumanoidControllerView _localView;

        // Sync position and rotation using NGO's NetworkVariable or NetworkTransform.
        // For this implementation, we will use a simple NetworkVariable for demonstration.
        private readonly NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);

        // Implementation of IHumanoidMovementView
        public Transform Transform => _localView.Transform;
        public bool IsActive => _localView.IsActive;
        public bool IsGrounded => _localView.IsGrounded;
        public float WalkSpeed => _localView.WalkSpeed;
        public float SprintMultiplier => _localView.SprintMultiplier;
        public float JumpForce => _localView.JumpForce;
        public float Gravity => _localView.Gravity;
        public Quaternion LookRotation => _localView.LookRotation;

        public void Move(Vector3 motion)
        {
            if (IsOwner)
            {
                // Local player: Apply movement and sync to network
                _localView.Move(motion);
                _netPosition.Value = _localView.Transform.position;
            }
            else
            {
                // Remote player: The UseCase shouldn't really be calling Move on us
                // if it's processing all views, but if it does, we ignore it
                // because we are driven by the network.
                UpdateFromNetwork();
            }
        }

        public void SetRotation(Quaternion rotation)
        {
            if (IsOwner)
            {
                _localView.SetRotation(rotation);
                _netRotation.Value = rotation;
            }
            else
            {
                UpdateFromNetwork();
            }
        }

        private void UpdateFromNetwork()
        {
            // Apply network state to local view
            _localView.Transform.position = _netPosition.Value;
            _localView.Transform.rotation = _netRotation.Value;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // Disable local logic/gravity on remote proxies if necessary
                // though the UseCase's processor might still run, the Mediator intercepts the results.
            }
        }
    }
}
