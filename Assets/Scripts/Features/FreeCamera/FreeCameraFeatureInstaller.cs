#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// The spectator free camera: moves and turns the possessed free camera from local input, per frame. Contributes the
    /// Free Camera input context (Move; Cancel frees the cursor).
    /// </summary>
    [CreateAssetMenu(fileName = "FreeCameraFeatureInstaller", menuName = "TinCan/Features/Free Camera Feature Installer")]
    public class FreeCameraFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<InputContext>
    {
        [Tooltip("Assets/Input/Contexts/Context_FreeCamera.")]
        [SerializeField] private FreeCameraInputContext? _controls;

        public IEnumerable<InputContext> Contributions
        {
            get { if (_controls != null) yield return _controls; }
        }

        public override void Install(IContainerBuilder builder)
        {
            if (_controls == null)
            {
                Debug.LogWarning($"[{name}] No FreeCameraInputContext assigned; the free camera cannot be flown.", this);
                return;
            }

            builder.RegisterInstance(_controls);
            builder.Register<FreeCameraMovementProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraRotationProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraMovementUseCase>(Lifetime.Singleton).As<ITickable>();
            builder.Register<ToggleCursorInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();
        }
    }
}
