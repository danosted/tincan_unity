#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship;
using TinCan.Core.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// Ship loss: the ship lost with its whole crew (its health run out, or an <see cref="IShipLossSource"/> such as the
    /// cloud boundary), held still behind the Lost screen until a player presses Respawn, then back whole at its last
    /// safe spot. Works with or without a voyage. The ShipLossState fixture on the ship carries the replicated state.
    /// Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipLossFeatureInstaller", menuName = "TinCan/Features/Ship Loss Feature Installer")]
    public class ShipLossFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [Tooltip("The Lost screen (Respawn, the choices row source, Quit).")]
        [SerializeField] private MenuDefinition? _lostMenu;
        [Tooltip("Networked ShipLossState prefab (NetworkObject, EntityNetworkMediator, ShipLossNetworkMediator) on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;

        public override void Install(IContainerBuilder builder)
        {
            if (_lostMenu == null)
            {
                Debug.LogWarning($"[{name}] No Lost menu assigned; a lost ship is never brought back.", this);
                return;
            }

            builder.Register<ShipLossUseCase>(Lifetime.Singleton).As<IShipLoss>().As<ISimulationTickable>().As<IAirshipHold>();
            builder.Register<ShipLostScreenPresenter>(Lifetime.Singleton).As<ITickable>().WithParameter(_lostMenu);
            builder.Register<RespawnShipMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
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
