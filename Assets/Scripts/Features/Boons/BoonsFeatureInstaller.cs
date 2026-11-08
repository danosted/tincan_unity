#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Navigation;
using TinCan.Core.Gas.Cues;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Boons: gates (the Gates feature) laid off the route from the voyage seed, each giving the ship a GAS boon when it
    /// flies through (used up after). The gates are on the navigator's chart. What a boon looks and sounds like is its
    /// effect's cue (the notifies listed here). Needs the Gates installer in the same profile. Plan:
    /// .docs/plans/hazards-and-boons.md (B1).
    /// </summary>
    [CreateAssetMenu(fileName = "BoonsFeatureInstaller", menuName = "TinCan/Features/Boons Feature Installer")]
    public class BoonsFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<GameplayCueNotify>
    {
        [SerializeField] private BoonsConfig? _config;
        [Tooltip("What taking a boon looks and sounds like (GCN_Boon_*).")]
        [SerializeField] private List<GameplayCueNotify> _cueNotifies = new();

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No BoonsConfig assigned; boons are off.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<BoonGateLayoutProcessor>(Lifetime.Transient);
            builder.Register<BoonGateMap>(Lifetime.Singleton);
            builder.Register<BoonGateChartSource>(Lifetime.Singleton).As<IChartSource>();
            builder.Register<BoonGateUseCase>(Lifetime.Singleton).As<ISimulationTickable>().As<ISessionParticipant>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config == null) yield break;
                foreach (var gate in _config.Gates)
                {
                    if (gate != null && gate.Prefab != null) yield return gate.Prefab;
                }
            }
        }

        IEnumerable<GameplayCueNotify> FeatureInstaller.IExtension<GameplayCueNotify>.Contributions => _cueNotifies;
    }
}
