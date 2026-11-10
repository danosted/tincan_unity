#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Features.SkillChecks.Challenges;
using TinCan.Features.SkillChecks.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Skill checks: short minigames on a ship part that grade a boost (the pulsing gauge and the button combo). The server opens a
    /// check on a player's peer and applies what its grade gives; the peer runs it in its own input context and draws
    /// it. A part runs checks through <see cref="SkillCheckHostNetworkMediator"/>; what starts one (pressing E on
    /// an engine station) belongs to the feature that owns the part. Challenges put checks together: their steps light
    /// stations, and an encounter starts one at an interval during a voyage (<see cref="SkillChallengesConfig"/>).
    /// Plan: .docs/plans/hazards-and-boons.md (B3, B4a).
    /// </summary>
    [CreateAssetMenu(fileName = "SkillChecksFeatureInstaller", menuName = "TinCan/Features/Skill Checks Feature Installer")]
    public class SkillChecksFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<InputContext>, FeatureInstaller.IExtension<StyleSheet>
    {
        [Tooltip("The skill check context (built by TinCan > Dev > Input > Build Assets).")]
        [SerializeField] private SkillCheckInputContext? _controls;
        [Tooltip("SkillGauge.uss and SkillCombo.uss.")]
        [SerializeField] private List<StyleSheet> _styles = new();
        [Tooltip("Encounters: the challenges a voyage throws at the crew, and how often. None: no encounters.")]
        [SerializeField] private SkillChallengesConfig? _challenges;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<PulsingGaugeProcessor>(Lifetime.Transient);
            builder.Register<ComboProcessor>(Lifetime.Transient);
            builder.Register<SkillChallengeUseCase>(Lifetime.Singleton).AsSelf().As<ISkillChallenges>().As<ISimulationTickable>();
            builder.Register<SkillCheckUseCase>(Lifetime.Singleton).As<ISkillChecks>().As<ISimulationTickable>();
            builder.Register<SkillChallengeHudPresenter>(Lifetime.Singleton).As<ITickable>();
            // Without a config the encounters have no challenges to start, and stay idle.
            builder.RegisterInstance(_challenges != null ? _challenges : CreateInstance<SkillChallengesConfig>());
            builder.Register<SkillChallengeEncounterUseCase>(Lifetime.Singleton).As<ISimulationTickable>().As<ISessionParticipant>();

            if (_controls == null)
            {
                Debug.LogWarning($"[{name}] No skill check context assigned; checks cannot be played on this peer.", this);
                return;
            }

            builder.RegisterInstance(_controls);
            builder.Register<SkillCheckRunner>(Lifetime.Singleton).As<ISkillCheckRunner>().As<ITickable>();
            builder.Register<SkillCheckPresenter>(Lifetime.Singleton).As<ITickable>();
        }

        IEnumerable<InputContext> FeatureInstaller.IExtension<InputContext>.Contributions
        {
            get
            {
                if (_controls != null) yield return _controls;
            }
        }

        IEnumerable<StyleSheet> FeatureInstaller.IExtension<StyleSheet>.Contributions => _styles;
    }
}
