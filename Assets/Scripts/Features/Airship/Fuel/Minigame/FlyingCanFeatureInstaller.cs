#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Features.Carry;
using TinCan.Features.Interaction;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Flying jerry cans and the handheld net. The can prefab is registered with the network at runtime from here.
    /// Depends on the fuel feature (jerry-can supply, carry state), hence a later Order.
    /// </summary>
    [CreateAssetMenu(fileName = "FlyingCanFeatureInstaller", menuName = "TinCan/Features/Flying Can Feature Installer")]
    public class FlyingCanFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private FlyingCanConfig? _config;

        public override int Order => 10;

        public override void Install(IContainerBuilder builder)
        {
            var config = _config;
            if (config == null)
            {
                config = CreateInstance<FlyingCanConfig>();
                config.Enabled = false;
                Debug.LogWarning($"[{name}] No FlyingCanConfig assigned; flying cans disabled.", this);
            }

            builder.RegisterInstance(config);
            builder.Register<FlyingCanWaveProcessor>(Lifetime.Transient);
            builder.Register<FlyingCanMotionProcessor>(Lifetime.Transient);
            builder.Register<FlyingCanSpawningService>(Lifetime.Singleton).As<IFlyingCanSpawner>();
            builder.Register<FlyingCanUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<CatchProcessor>(Lifetime.Transient);
            builder.Register<NetCatchUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<TakeNetInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config != null && _config.CanPrefab != null) yield return _config.CanPrefab;
            }
        }
    }
}
