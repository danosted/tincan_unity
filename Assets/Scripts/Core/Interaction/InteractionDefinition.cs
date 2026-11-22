using System;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// Configures how a world interaction is dispatched after server validation.
    /// </summary>
    [CreateAssetMenu(fileName = "New Interaction", menuName = "TinCan/Interactions/Interaction Definition")]
    public class InteractionDefinition : ScriptableObject
    {
        /// <summary>
        /// Determines who is granted/activates <see cref="Ability"/> and who receives its effect.
        /// </summary>
        public enum AbilityActivatorType
        {
            /// <summary>The interaction requester activates the ability; the resolved target's controller is passed as the effect recipient (e.g. a player repairing a module).</summary>
            Requester,

            /// <summary>The interaction target's own controller activates the ability on itself; the requester is not involved (e.g. toggling a station's own or its parent's ability).</summary>
            Target
        }

        [SerializeField]
        [HandlerTypeReference(typeof(IInteractionHandler))]
        private string _handlerTypeName;
        [SerializeField] private AbilityDefinition _ability;
        [SerializeField]
        [Tooltip("Requester: the interacting player activates the ability, targeting the interacted object (e.g. repairing a module).\nTarget: the interacted object activates the ability on itself (e.g. toggling a station).")]
        private AbilityActivatorType _abilityActivator;
        [SerializeField, Min(0f)]
        [Tooltip("0: Interact acts on the press. Above 0: the work takes this long; hold Interact on the same target until it is done (letting go, looking away or walking off cancels it).")]
        private float _holdSeconds;
        [SerializeField]
        [Tooltip("What Interact does here, shown on the HUD while the target is in reach (\"Mine crystal\"). Empty shows nothing.")]
        private string _prompt = string.Empty;

        public Type HandlerType => string.IsNullOrEmpty(_handlerTypeName) ? null : Type.GetType(_handlerTypeName);
        public AbilityDefinition Ability => _ability;

        /// <summary>Who activates <see cref="Ability"/> when this interaction fires; see <see cref="AbilityActivatorType"/>.</summary>
        public AbilityActivatorType AbilityActivator => _abilityActivator;

        /// <summary>How long Interact must be held on the target before the handler runs; 0 acts on the press.</summary>
        public float HoldSeconds
        {
            get => _holdSeconds;
            set => _holdSeconds = Mathf.Max(0f, value);
        }

        /// <summary>What Interact does on this target, for the HUD; empty shows nothing. A target may say more (<see cref="IInteractionPrompt"/>).</summary>
        public string Prompt
        {
            get => _prompt ?? string.Empty;
            set => _prompt = value ?? string.Empty;
        }
    }
}
