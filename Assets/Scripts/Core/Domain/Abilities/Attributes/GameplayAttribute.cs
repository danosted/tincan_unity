using UnityEngine;

namespace TinCan.Core.Domain.Abilities.Attributes
{
    /// <summary>
    /// ScriptableObject defining the identity of a gameplay attribute (e.g., Health, MoveSpeed).
    /// Used as a key for dictionary/list lookups to avoid string comparisons.
    /// </summary>
    public abstract class GameplayAttribute : ScriptableObject
    {
        private int? _hash;

        /// <summary>
        /// Generates a deterministic hash based on the ScriptableObject's name.
        /// Safe for network serialization.
        /// </summary>
        public int GetHash()
        {
            if (!_hash.HasValue)
            {
                _hash = Animator.StringToHash(name);
            }
            return _hash.Value;
        }
    }
}
