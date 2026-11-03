#nullable enable
using TinCan.Core.Domain.Atmosphere;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// The look's light and air in a scene: banded fog, ambient, the sun and the post effects, owned by one presenter
    /// (<see cref="AtmospherePresenter"/>, registered as <see cref="IAtmosphereOwner"/>, so CloudBoundary and the cloud
    /// tuning rig leave fog and sun to it). Presentation only, never replicated. Loaded by the play scenes' profiles;
    /// the test range stays plain. Built by TinCan > Dev > Look > Build Atmosphere. Plan: .docs/plans/look-slice.md (L2).
    /// </summary>
    [CreateAssetMenu(fileName = "AtmosphereFeatureInstaller", menuName = "TinCan/Features/Atmosphere Feature Installer")]
    public class AtmosphereFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Settings/Look/Look_GoldenAfternoon.")]
        [SerializeField] private LookProfile? _look;

        public override void Install(IContainerBuilder builder)
        {
            if (_look == null)
            {
                Debug.LogWarning($"[{name}] No LookProfile assigned; the scene keeps its own fog and light.", this);
                return;
            }

            builder.RegisterInstance(_look);
            builder.RegisterInstance<ISurfaceStyle>(_look.ActiveSurface);
            builder.Register<AtmosphereProcessor>(Lifetime.Singleton);
            builder.Register<AtmospherePresenter>(Lifetime.Singleton).AsSelf().As<IAtmosphereOwner>().As<IStartable>().As<ILateTickable>();
        }
    }
}
