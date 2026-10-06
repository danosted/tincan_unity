#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Fixtures;
using TinCan.Core.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Ship sockets: the integration between ships' parts and the systems that mount things on them. Free sockets (from
    /// the parts' <c>ShipSocket</c> markers, through <c>IShipSockets</c>) become interaction targets; E on one opens the
    /// fitting menu; the server mounts the choice and replicates what is mounted where. Fittings come from other features
    /// (<c>IExtension&lt;ShipFittingDefinition&gt;</c>). Plan: .docs/plans/modular-airship-builder.md (S5).
    /// </summary>
    [CreateAssetMenu(fileName = "ShipSocketsFeatureInstaller", menuName = "TinCan/Features/Ship Sockets Feature Installer")]
    public class ShipSocketsFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private ShipSocketsConfig? _config;
        [Tooltip("The ShipSocketsState fixture: replicates what is mounted in each ship's sockets.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null || _config.MountInteraction == null)
            {
                Debug.LogWarning($"[{name}] No ShipSocketsConfig with a mount interaction assigned; sockets stay empty.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<ShipFittingCatalog>(Lifetime.Singleton).As<IShipFittingCatalog>();
            builder.Register<ShipFittingUseCase>(Lifetime.Singleton).As<IShipFittings>().As<ITickable>();
            builder.Register<ShipSocketTargetsUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<MountFittingInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<ShipFittingChoice>(Lifetime.Singleton);
            builder.Register<ShipFittingMenuUseCase>(Lifetime.Singleton).As<ITickable>();
            builder.Register<MountFittingMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ShipSocketsFixtureFilter>(Lifetime.Singleton).As<IShipFixtureFilter>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get { if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab; }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get { if (_stateFixture != null) yield return _stateFixture; }
        }
    }
}
