#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The drawn cloud sea and its holes (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"): the server's
    /// hole list on the CloudHolesState ship fixture, the ambient holes, and the sea drawn around the local camera. The
    /// boundary (<see cref="CloudBoundaryFeatureInstaller"/>) reads the same holes, so a hole is open air for the game too.
    /// Built by TinCan > Dev > Cloud Sea > Build Assets.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudSeaFeatureInstaller", menuName = "TinCan/Features/Cloud Sea Feature Installer")]
    public class CloudSeaFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private CloudSeaConfig? _config;
        [Tooltip("The sea's material (TinCan/Environment/Cloud Sea Swell).")]
        [SerializeField] private Material? _seaMaterial;
        [Tooltip("Networked CloudHolesState prefab (NetworkObject, EntityNetworkMediator, CloudHolesNetworkMediator) on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No CloudSeaConfig assigned; there is no drawn sea.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<CloudHoleProcessor>(Lifetime.Singleton);
            builder.Register<CloudHolesUseCase>(Lifetime.Singleton).As<ICloudHoles>().As<ISimulationTickable>();
            builder.Register<AmbientCloudHolesUseCase>(Lifetime.Singleton).As<ISimulationTickable>();
            builder.Register<CloudSeaPresenter>(Lifetime.Singleton).WithParameter(typeof(Material), _seaMaterial).As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_stateFixture != null) yield return _stateFixture;
            }
        }
    }
}
