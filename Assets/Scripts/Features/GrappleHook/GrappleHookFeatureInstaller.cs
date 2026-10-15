#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Humanoid;
using TinCan.Core.Items;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// The grappling hook: an item from a rack on deck. Holding it, press Primary to fire at what you look at and hold to
    /// reel yourself in (back aboard after a fall, up to a higher deck). The pull is part of the predicted movement
    /// (<see cref="GrappleHookUseCase"/>, an <see cref="IHumanoidMotionContributor"/>); the GrappleHookState fixture
    /// replicates the lines so every peer sees them (<see cref="GrappleLinePresenter"/>). Plan: .docs/plans/grapple-hook.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GrappleHookFeatureInstaller", menuName = "TinCan/Features/Grapple Hook Feature Installer")]
    public class GrappleHookFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<ItemDefinition>
    {
        [SerializeField] private GrappleHookConfig? _config;
        [Tooltip("The networked rack that hands out the hook, and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _rackFixture;
        [Tooltip("The GrappleHookState fixture: replicates who is hooked where.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;
        [Tooltip("The items this feature owns (the hook).")]
        [SerializeField] private List<ItemDefinition> _items = new();

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null || _config.CarryingTag == null || _config.FireInput == null)
            {
                Debug.LogWarning($"[{name}] No GrappleHookConfig with a carrying tag and a fire input assigned; the hook does nothing.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<GrappleHookProcessor>(Lifetime.Transient);
            builder.Register<PhysicsGrappleHookQuery>(Lifetime.Singleton).As<IGrappleHookQuery>();
            builder.Register<GrappleHookUseCase>(Lifetime.Singleton).AsSelf().As<IHumanoidMotionContributor>().As<ISimulationTickable>().As<IGrappleHooks>();
            builder.Register<GrappleLinePresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<GrappleAimPresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_rackFixture != null && _rackFixture.Prefab != null) yield return _rackFixture.Prefab;
                if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_rackFixture != null) yield return _rackFixture;
                if (_stateFixture != null) yield return _stateFixture;
            }
        }

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;
    }
}
