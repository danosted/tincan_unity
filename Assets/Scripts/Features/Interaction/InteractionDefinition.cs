using System;
using TinCan.Features.Abilities;
using UnityEngine;

namespace TinCan.Features.Interaction
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

        public Type HandlerType => string.IsNullOrEmpty(_handlerTypeName) ? null : Type.GetType(_handlerTypeName);
        public AbilityDefinition Ability => _ability;

        /// <summary>Who activates <see cref="Ability"/> when this interaction fires; see <see cref="AbilityActivatorType"/>.</summary>
        public AbilityActivatorType AbilityActivator => _abilityActivator;
    }
}
