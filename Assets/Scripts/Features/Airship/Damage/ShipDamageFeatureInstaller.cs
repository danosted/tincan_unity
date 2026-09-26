#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Ship damage: breakable spots on the deck (the ShipDamageSockets fixture), random breakage, the hull-breach
    /// effect on the ship (fuel leak) and a HUD line. Switch it on or off by listing it in a scene's feature profile.
    /// Repairing broken parts is added by the repair tool (plan step 4).
    /// </summary>
    [CreateAssetMenu(fileName = "ShipDamageFeatureInstaller", menuName = "TinCan/Features/Ship Damage Feature Installer")]
    public class ShipDamageFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private ShipDamageConfig? _config;
        [Tooltip("Networked ShipDamageSockets prefab and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _socketsFixture;

        public override void Install(IContainerBuilder builder)
        {
            var config = _config;
            if (config == null)
            {
                config = CreateInstance<ShipDamageConfig>();
                config.AutoBreak = false;
                Debug.LogWarning($"[{name}] No ShipDamageConfig assigned; random breakage disabled.", this);
            }

            builder.RegisterInstance(config);
            builder.Register<ShipBreakageProcessor>(Lifetime.Transient);
            builder.Register<ShipBreakageUseCase>(Lifetime.Singleton).AsSelf().As<IShipBreakage>().As<ISimulationTickable>();
            builder.Register<ShipDamageHudPresenter>(Lifetime.Singleton).As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_socketsFixture != null && _socketsFixture.Prefab != null) yield return _socketsFixture.Prefab;
            }
        }

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_socketsFixture != null) yield return _socketsFixture;
            }
        }
    }
}
