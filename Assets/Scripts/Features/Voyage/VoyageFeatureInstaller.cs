#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Voyage: a play session from here to a destination ahead, won on arrival (an end screen with Restart) and lost
    /// with the ship. It builds on Ship Loss: the loss holds the ship behind its Lost screen (Respawn), and the voyage adds
    /// a Restart Voyage row there (<see cref="VoyageLostChoices"/>). It resets and paces the other features through
    /// <see cref="ISessionParticipant"/> and references none of them. The VoyageState fixture on the ship carries the
    /// replicated state. Plans: .docs/plans/voyage-session.md, .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageFeatureInstaller", menuName = "TinCan/Features/Voyage Feature Installer")]
    public class VoyageFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private VoyageConfig? _config;
        [Tooltip("Networked VoyageState prefab (NetworkObject, EntityNetworkMediator, VoyageNetworkMediator) on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No VoyageConfig assigned; there are no voyages.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<VoyageRouteProcessor>(Lifetime.Transient);
            builder.Register<VoyageUseCase>(Lifetime.Singleton).As<IVoyage>().As<ISimulationTickable>();
            builder.Register<VoyageHudPresenter>(Lifetime.Singleton).As<ITickable>();
            builder.Register<VoyageSessionProgress>(Lifetime.Singleton).As<ISessionProgress>();
            builder.Register<VoyageEndScreenPresenter>(Lifetime.Singleton).As<ITickable>();
            builder.Register<VoyageBeaconPresenter>(Lifetime.Singleton).As<ITickable>();
            builder.Register<RestartVoyageMenuCommand>(Lifetime.Singleton).AsSelf().As<IMenuCommand>();
            builder.Register<VoyageLostChoices>(Lifetime.Singleton).As<IMenuRowSource>();
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
