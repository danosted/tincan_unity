#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Sky hazards: things in the sky with health that the cannon shoots down. With the field on, a few hang ahead of
    /// the ship; otherwise they appear only on demand (ISkyHazards.SpawnAt). Plan: .docs/plans/cannon-and-hazards.md.
    /// </summary>
    [CreateAssetMenu(fileName = "SkyHazardsFeatureInstaller", menuName = "TinCan/Features/Sky Hazards Feature Installer")]
    public class SkyHazardsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private SkyHazardConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No SkyHazardConfig assigned; sky hazards are off.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<SkyHazardFieldProcessor>(Lifetime.Transient);
            builder.Register<SkyHazardSpawningService>(Lifetime.Singleton).As<ISkyHazardSpawner>();
            builder.Register<SkyHazardUseCase>(Lifetime.Singleton).As<ISkyHazards>().As<ISimulationTickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config != null && _config.Prefab != null) yield return _config.Prefab;
            }
        }
    }
}
