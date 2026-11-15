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
    /// With a <see cref="CloudWispsConfig"/>, also the wisps past the camera (<see cref="CloudWispsPresenter"/>).
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
        [Tooltip("The wisps streaming past the camera with speed and depth (plan step 3). None: no wisps.")]
        [SerializeField] private CloudWispsConfig? _wispsConfig;
        [Tooltip("The wisps' material (TinCan/Environment/Cloud Wisp).")]
        [SerializeField] private Material? _wispMaterial;

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

            if (_wispsConfig == null) return;
            builder.RegisterInstance(_wispsConfig);
            builder.Register<CloudWispsProcessor>(Lifetime.Singleton);
            builder.Register<CloudWispsPresenter>(Lifetime.Singleton).WithParameter(typeof(Material), _wispMaterial).As<ILateTickable>();
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
