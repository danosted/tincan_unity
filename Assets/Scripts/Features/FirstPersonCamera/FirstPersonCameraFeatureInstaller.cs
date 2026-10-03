#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Look;
using UnityEngine;
using VContainer;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>
    /// The first-person camera rig: the player looks through their own eyes. A profile loads one camera feature (this or
    /// ThirdPersonCamera); with several, the first registered wins and LookView warns.
    /// </summary>
    [CreateAssetMenu(fileName = "FirstPersonCameraFeatureInstaller", menuName = "TinCan/Features/First Person Camera Feature Installer")]
    public class FirstPersonCameraFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Settings/Camera/FirstPersonCameraConfig.")]
        [SerializeField] private FirstPersonCameraConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No FirstPersonCameraConfig assigned; no first-person rig.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<FirstPersonViewRig>(Lifetime.Singleton).As<IViewRig>();
        }
    }
}
