#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.Core.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Respawn: what happens when a player dies or respawns, decided by one <see cref="RespawnRules"/> asset.
    /// - **Overboard:** the cloud boundary hands the fallen player over (<see cref="IOverboardHandler"/>). They choose on
    ///   their own screen: respawn aboard by the rules, or hang on the ship's leash.
    /// - **The leash:** a ship fixture. The player is dragged behind the ship, moved by the server only, until they climb up
    ///   or the leash point's winch (the upgrade) reels them in.
    /// - **Without this feature:** the cloud boundary teleports fallen players home as before.
    /// Plan: .docs/plans/overboard-and-rescue.md.
    /// </summary>
    [CreateAssetMenu(fileName = "RespawnFeatureInstaller", menuName = "TinCan/Features/Respawn Feature Installer")]
    public class RespawnFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<InputContext>,
        FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [Tooltip("What happens on a respawn and on the leash: per cause, the carried item and the delay; rope and climb settings. Swap the asset per profile.")]
        [SerializeField] private RespawnRules? _rules;
        [Tooltip("The RespawnState fixture: carries a player's choice to the server.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;
        [Tooltip("The ship's leash point: a plain point (climb only) or one with a winch (the upgrade). None: no leash, only respawning.")]
        [SerializeField] private ShipFixtureDefinition? _leashFixture;
        [Tooltip("Context_Leashed: live while leashed; Interact opens the respawn choice.")]
        [SerializeField] private InputContext? _leashedControls;
        [Tooltip("The leash winch as a fitting players (or a ship's loadout) mount in a socket: designed ships such as the Galleon put it on a balcony.")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            if (_rules == null || _rules.OverboardEffect == null || _rules.OverboardTag == null)
            {
                Debug.LogWarning($"[{name}] No RespawnRules with an overboard effect and tag assigned; falling overboard teleports home as before.", this);
                return;
            }

            builder.RegisterInstance(_rules);
            builder.Register<LeashStore>(Lifetime.Singleton);
            builder.Register<OverboardUseCase>(Lifetime.Singleton).AsSelf().As<IOverboardHandler>().As<ISimulationTickable>();
            builder.Register<OverboardHoldUseCase>(Lifetime.Singleton).As<IHumanoidMotionContributor>();
            builder.Register<LeashRopeUseCase>(Lifetime.Singleton).As<IHumanoidMotionContributor>().As<IServerDrivenHumanoidRule>();
            builder.Register<OverboardChoice>(Lifetime.Singleton);
            builder.Register<OverboardMenuUseCase>(Lifetime.Singleton).As<ITickable>();
            builder.Register<RespawnAboardMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<HangOnLeashMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<OpenLeashMenuInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();
            builder.Register<CrankWinchInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<LeashRopePresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab;
                if (_leashFixture != null && _leashFixture.Prefab != null) yield return _leashFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_stateFixture != null) yield return _stateFixture;
                if (_leashFixture != null) yield return _leashFixture;
            }
        }

        IEnumerable<InputContext> FeatureInstaller.IExtension<InputContext>.Contributions
        {
            get { if (_leashedControls != null) yield return _leashedControls; }
        }

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
