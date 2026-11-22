#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Encounters: challenges are zones in the world, and outside them the world is quiet. Today only the quiet part
    /// exists (<see cref="EncounterPressureUseCase"/>); the zones come next. Loaded by the game profile (Profile_Galleon,
    /// cheesed_scene) in place of the voyage. Plan: .docs/plans/encounters.md.
    /// </summary>
    [CreateAssetMenu(fileName = "EncountersFeatureInstaller", menuName = "TinCan/Features/Encounters Feature Installer")]
    public class EncountersFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<EncounterPressureUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
        }
    }
}
