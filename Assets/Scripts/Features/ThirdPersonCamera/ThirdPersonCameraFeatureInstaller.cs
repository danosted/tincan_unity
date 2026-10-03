#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Look;
using UnityEngine;
using VContainer;

namespace TinCan.Features.ThirdPersonCamera
{
    /// <summary>
    /// The third-person camera rig: the player's camera orbits behind them. A profile loads one camera feature (this or
    /// FirstPersonCamera); with several, the first registered wins and LookView warns.
    /// </summary>
    [CreateAssetMenu(fileName = "ThirdPersonCameraFeatureInstaller", menuName = "TinCan/Features/Third Person Camera Feature Installer")]
    public class ThirdPersonCameraFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Settings/Camera/ThirdPersonCameraConfig.")]
        [SerializeField] private ThirdPersonCameraConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No ThirdPersonCameraConfig assigned; no third-person rig.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<ThirdPersonViewRig>(Lifetime.Singleton).As<IViewRig>();
        }
    }
}
