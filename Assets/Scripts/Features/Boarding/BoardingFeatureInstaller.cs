#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Boarding
{
    /// <summary>
    /// Boarding: players who spawn while a ship exists start on its deck, not at the player prefab's spawn point.
    /// Plan: .docs/plans/crew-gate-and-boarding.md.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardingFeatureInstaller", menuName = "TinCan/Features/Boarding Feature Installer")]
    public class BoardingFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private BoardingConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No BoardingConfig assigned; players spawn where the player prefab puts them.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<BoardingUseCase>(Lifetime.Singleton).As<ISimulationTickable>();
        }
    }
}
