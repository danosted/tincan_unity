using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Utility component for the Unity Editor to automatically start networking.
    /// This component is purely for data; logic is handled by the ProjectLifetimeScope.
    /// </summary>
    public class NetworkDevBootstrapper : MonoBehaviour
    {
        [SerializeField] private bool _autoStartHost = true;

        public bool AutoStartHost => _autoStartHost;
    }
}
