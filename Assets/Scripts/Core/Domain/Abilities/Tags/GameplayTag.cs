using UnityEngine;

namespace TinCan.Core.Domain.Abilities.Tags
{
    /// <summary>
    /// A hierarchical tag used for gameplay logic (e.g., "State.Stunned", "Ability.Sprint").
    /// Represented as a ScriptableObject to allow designer-driven tag creation.
    /// </summary>
    [CreateAssetMenu(fileName = "New Gameplay Tag", menuName = "TinCan/Abilities/Tag")]
    public class GameplayTag : ScriptableObject
    {
        [SerializeField] private GameplayTag _parent;

        public string TagName => name;
        public GameplayTag Parent => _parent;

        /// <summary>
        /// Checks if this tag is a sub-tag of another tag (e.g., "State.Stunned" is a child of "State").
        /// </summary>
        public bool IsChildOf(GameplayTag other)
        {
            if (other == null) return false;
            if (this == other) return true;

            var current = _parent;
            while (current != null)
            {
                if (current == other) return true;
                current = current.Parent;
            }

            return false;
        }
    }
}
