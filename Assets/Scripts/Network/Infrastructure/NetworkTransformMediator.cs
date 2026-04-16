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
                    _netRotation.Value = transform.rotation;
                    return;
                }
            }

            _netParentPlatform.Value = new NetworkObjectReference();
            _netPosition.Value = transform.position;
            _netRotation.Value = transform.rotation;
        }

        private void UpdateFromNetwork()
        {
            Vector3 targetWorldPos;
            if (_netParentPlatform.Value.TryGet(out NetworkObject netObj))
            {
                targetWorldPos = netObj.transform.TransformPoint(_netLocalPosition.Value);
            }
            else
            {
                targetWorldPos = _netPosition.Value;
            }

            float drift = Vector3.Distance(transform.position, targetWorldPos);
            if (drift > 0.1f)
            {
                transform.position = Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * 10f);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, _netRotation.Value, Time.deltaTime * 15f);
        }
    }
}
