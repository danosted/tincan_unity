#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Helm
{
    /// <summary>
    /// Steering the ship from its helm, in your own body: the helm fixture (a station, so the Stations installer must be
    /// in the profile too), the Helmsman input context, the helmsman's axes in their predicted input, and the helm as the
    /// ship's pilot on the server. Plan: .docs/plans/helm-station.md.
    /// </summary>
    [CreateAssetMenu(fileName = "HelmFeatureInstaller", menuName = "TinCan/Features/Helm Feature Installer")]
    public class HelmFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<InputContext>
    {
        [Tooltip("The helm station and where it sits on the ship.")]
        [SerializeField] private List<ShipFixtureDefinition> _helmFixtures = new();
        [Tooltip("Assets/Input/Contexts/Context_Helmsman: the helm's controls while steering.")]
        [SerializeField] private HelmsmanInputContext? _controls;

        public override void Install(IContainerBuilder builder)
        {
            if (_controls == null)
            {
                Debug.LogWarning($"[{name}] No HelmsmanInputContext assigned; the helm cannot be steered.", this);
                return;
            }

            builder.RegisterInstance(_controls);
            builder.Register<HelmInputUseCase>(Lifetime.Singleton).As<IHumanoidInputContributor>();
            builder.Register<HelmSteeringUseCase>(Lifetime.Singleton).As<IAirshipPilotInput>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                foreach (var fixture in _helmFixtures)
                {
                    if (fixture != null && fixture.Prefab != null) yield return fixture.Prefab;
                }
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions => _helmFixtures;

        IEnumerable<InputContext> FeatureInstaller.IExtension<InputContext>.Contributions
        {
            get { if (_controls != null) yield return _controls; }
        }
    }
}
