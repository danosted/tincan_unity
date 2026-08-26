using TinCan.Features.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Configures how a world interaction is dispatched after server validation.
    /// </summary>
    [CreateAssetMenu(fileName = "New Interaction", menuName = "TinCan/Interactions/Interaction Definition")]
    public class InteractionDefinition : ScriptableObject
    {
        [SerializeField] private GameplayTag _handlerTag;
        [SerializeField] private AbilityDefinition _ability;

        public GameplayTag HandlerTag => _handlerTag;
        public AbilityDefinition Ability => _ability;
    }
}
