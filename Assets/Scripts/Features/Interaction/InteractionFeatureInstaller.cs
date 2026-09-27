#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Features;
using TinCan.Features.Targeting;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Interaction through targeting: the Interact key is a predicted input bit, and <see cref="InteractInputUseCase"/>
    /// acquires the target on the server with TD_Interact. The owner's prompt (InteractorControllerView) and the legacy
    /// E-press path (InteractivityUseCase) switch to it when this installer is active. Without it, interaction falls back to
    /// the old client-chosen target RPC. The core interaction services are still registered in ProjectLifetimeScope.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionFeatureInstaller", menuName = "TinCan/Features/Interaction Feature Installer")]
    public class InteractionFeatureInstaller : FeatureInstaller
    {
        [Tooltip("The Interact input bit (Input_Interact), bound in DefaultInputBindingConfig.")]
        [SerializeField] private GameplayInput? _interactInput;
        [Tooltip("How the Interact target is acquired (TD_Interact).")]
        [SerializeField] private TargetingDefinition? _targeting;

        public override int Order => -10;

        public override void Install(IContainerBuilder builder)
        {
            if (_interactInput == null || _targeting == null)
            {
                Debug.LogWarning($"[{name}] Interact input or targeting definition missing; interaction stays on the legacy RPC path.", this);
                return;
            }

            builder.RegisterInstance(new InteractionTargetingSettings(_interactInput, _targeting));
            builder.Register<InteractInputUseCase>(Lifetime.Singleton).As<ISimulationTickable>();
        }
    }
}
