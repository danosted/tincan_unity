#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Look;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using VContainer;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>
    /// The first-person camera rig: the player looks through their own eyes. A profile loads one camera feature (this or
    /// ThirdPersonCamera); with several, the first registered wins and LookView warns. Contributes the field of view
    /// setting (<see cref="FirstPersonViewRig.FieldOfViewKey"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "FirstPersonCameraFeatureInstaller", menuName = "TinCan/Features/First Person Camera Feature Installer")]
    public class FirstPersonCameraFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<SettingDefinition>
    {
        [Tooltip("Assets/Settings/Camera/FirstPersonCameraConfig.")]
        [SerializeField] private FirstPersonCameraConfig? _config;
        [Tooltip("Assets/Settings/PlayerSettings/Setting_FieldOfView (view.fov).")]
        [SerializeField] private SettingDefinition? _fieldOfView;

        public IEnumerable<SettingDefinition> Contributions
        {
            get { if (_fieldOfView != null) yield return _fieldOfView; }
        }

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
