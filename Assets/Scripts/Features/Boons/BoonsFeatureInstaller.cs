#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Navigation;
using TinCan.Core.Gas.Cues;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Boons: things that give the ship a boost.
    /// <list type="bullet">
    /// <item>Boon gates (the Gates feature), laid off the route from the voyage seed and on the navigator's chart: each
    /// gives a GAS boon when the ship flies through, and opens the boost window.</item>
    /// <item>The engine telegraph (a ship fixture, and the boon.engine_telegraph fitting): press E for its skill check
    /// (the SkillChecks feature), whose grade gives a speed boost, more inside a boost window.</item>
    /// </list>
    /// What a boon looks and sounds like is its effect's cue (the notifies listed here). Needs the Gates and SkillChecks
    /// installers in the same profile. Plan: .docs/plans/hazards-and-boons.md (B1, B3).
    /// </summary>
    [CreateAssetMenu(fileName = "BoonsFeatureInstaller", menuName = "TinCan/Features/Boons Feature Installer")]
    public class BoonsFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<GameplayCueNotify>,
        FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [SerializeField] private BoonsConfig? _config;
        [Tooltip("What taking a boon looks and sounds like (GCN_Boon_*).")]
        [SerializeField] private List<GameplayCueNotify> _cueNotifies = new();
        [Tooltip("The engine telegraph on the old hull (EngineTelegraphFixture).")]
        [SerializeField] private ShipFixtureDefinition? _telegraphFixture;
        [Tooltip("Socket fittings: the engine telegraph (boon.engine_telegraph).")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<StartEngineBoostInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();

            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No BoonsConfig assigned; boon gates are off.", this);
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
                if (_telegraphFixture != null && _telegraphFixture.Prefab != null) yield return _telegraphFixture.Prefab;
                foreach (var fitting in _fittings)
                {
                    if (fitting != null && fitting.Prefab != null) yield return fitting.Prefab;
                }

                if (_config == null) yield break;
                foreach (var gate in _config.Gates)
                {
                    if (gate != null && gate.Prefab != null) yield return gate.Prefab;
                }
            }
        }

        IEnumerable<GameplayCueNotify> FeatureInstaller.IExtension<GameplayCueNotify>.Contributions => _cueNotifies;

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_telegraphFixture != null) yield return _telegraphFixture;
            }
        }

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
