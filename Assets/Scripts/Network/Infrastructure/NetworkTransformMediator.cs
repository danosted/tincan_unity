using Unity.Netcode;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Specialized NetworkTransform that supports platform-relative synchronization.
    /// Useful for characters moving on ships or moving platforms.
    /// </summary>
    public class NetworkTransformMediator : NetworkBehaviour
    {
        private readonly NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<NetworkObjectReference> _netParentPlatform = new NetworkVariable<NetworkObjectReference>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector3> _netLocalPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<Quaternion> _netLocalRotation = new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);

        private Transform _currentPlatform;

        public void SetPlatform(Transform platform)
        {
            _currentPlatform = platform;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner) UpdateToNetwork();
            else UpdateFromNetwork();
        }

        private void UpdateToNetwork()
        {
            if (_currentPlatform != null)
            {
                var netObj = _currentPlatform.GetComponentInParent<NetworkObject>();
                if (netObj != null)
                {
                    _netParentPlatform.Value = netObj;
                    _netLocalPosition.Value = netObj.transform.InverseTransformPoint(transform.position);
                    _netLocalRotation.Value = Quaternion.Inverse(netObj.transform.rotation) * transform.rotation;
                    return;
                }
            }

            _netParentPlatform.Value = new NetworkObjectReference();
            _netPosition.Value = transform.position;
            _netRotation.Value = transform.rotation;
        }

        private void UpdateFromNetwork()
        {
            if (_netParentPlatform.Value.TryGet(out NetworkObject netObj))
            {
                // Relative space: Glued tightly to the moving platform to prevent desync
                Vector3 targetWorldPos = netObj.transform.TransformPoint(_netLocalPosition.Value);
                Quaternion targetWorldRot = netObj.transform.rotation * _netLocalRotation.Value;

                transform.position = Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * 20f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRot, Time.deltaTime * 20f);
            }
            else
            {
                // World space
                float drift = Vector3.Distance(transform.position, _netPosition.Value);
                if (drift > 0.05f)
                {
                    transform.position = Vector3.Lerp(transform.position, _netPosition.Value, Time.deltaTime * 15f);
                }

                transform.rotation = Quaternion.Slerp(transform.rotation, _netRotation.Value, Time.deltaTime * 15f);
            }
        }
    }
}
