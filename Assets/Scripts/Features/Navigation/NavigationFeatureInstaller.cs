#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Navigation;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Navigation: every ship has a route (waypoints ending at a point of interest) that a navigator plots at the ship's
    /// chart table, and a spherical compass at its helm points at the route's active waypoint. The session's destination
    /// is the first point of interest. The NavigationState fixture on the ship carries the replicated route. Other features
    /// add points of interest and obstacles through <see cref="IChartSource"/>; this feature references none of them.
    /// Builds on Helm (the compass sits at it) and Stations (the chart table is one). Plan: .docs/plans/navigation-compass.md.
    /// </summary>
    [CreateAssetMenu(fileName = "NavigationFeatureInstaller", menuName = "TinCan/Features/Navigation Feature Installer")]
    public class NavigationFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>,
        FeatureInstaller.IExtension<InputContext>, FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [SerializeField] private NavigationConfig? _config;
        [Tooltip("Networked NavigationState prefab (NetworkObject, EntityNetworkMediator, NavigationNetworkMediator) on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;
        [Tooltip("The chart table and where it stands on the ship (for ships with fixed fixtures, such as Airship_Prefab).")]
        [SerializeField] private ShipFixtureDefinition? _tableFixture;
        [Tooltip("Assets/Input/Contexts/Context_Navigator: the chart's controls while seated at the table.")]
        [SerializeField] private NavigatorInputContext? _controls;
        [Tooltip("The chart table as a fitting players (or a ship's loadout) mount in a socket: designed ships such as the Galleon.")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No NavigationConfig assigned; ships have no routes.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<SessionDestinationChartSource>(Lifetime.Singleton).As<IChartSource>();
            builder.Register<ChartContents>(Lifetime.Singleton);
            builder.Register<RouteProgressProcessor>(Lifetime.Transient);
            builder.Register<ChartProjectionProcessor>(Lifetime.Transient);
            builder.Register<RouteEditProcessor>(Lifetime.Transient);
            builder.Register<NavigationUseCase>(Lifetime.Singleton).As<ISimulationTickable>().As<ISessionParticipant>().As<INavigation>();
            builder.Register<CompassPresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();

            if (_controls == null)
            {
                Debug.LogWarning($"[{name}] No NavigatorInputContext assigned; nobody can plot at the chart table.", this);
                return;
            }

            builder.RegisterInstance(_controls);
            builder.Register<ChartNavigatorUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<ChartPresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<NavigatorHudPresenter>(Lifetime.Singleton).As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab;
                if (_tableFixture != null && _tableFixture.Prefab != null) yield return _tableFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_stateFixture != null) yield return _stateFixture;
                if (_tableFixture != null) yield return _tableFixture;
            }
        }

        IEnumerable<InputContext> FeatureInstaller.IExtension<InputContext>.Contributions
        {
            get { if (_controls != null) yield return _controls; }
        }

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
