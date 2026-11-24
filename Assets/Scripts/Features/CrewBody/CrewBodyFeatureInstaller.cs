#nullable enable
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.CrewBody
{
    /// <summary>
    /// The crew body: every humanoid is drawn as an animated crewmate instead of the placeholder capsule, its clips
    /// following how it moves (idle, run, sprint, jump, fall). Profiles without it keep capsules. Plan: crew-animation.md.
    /// </summary>
    [CreateAssetMenu(fileName = "CrewBodyFeatureInstaller", menuName = "TinCan/Features/Crew Body Feature Installer")]
    public class CrewBodyFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Prefabs/CrewBody/CrewBody (built by TinCan > Dev > Crew Body > Build Assets).")]
        [SerializeField] private CrewBodyView? _body;

        public override void Install(IContainerBuilder builder)
        {
            if (_body == null)
            {
                Debug.LogWarning($"[{name}] No crew body prefab assigned; humanoids stay capsules.", this);
                return;
            }

            builder.RegisterInstance(_body);
            builder.RegisterEntryPoint<CrewBodyUseCase>();
        }
    }
}
