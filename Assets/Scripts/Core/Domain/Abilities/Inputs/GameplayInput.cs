using UnityEngine;

namespace TinCan.Core.Domain.Abilities.Inputs
{
    /// <summary>
    /// ScriptableObject defining the identity of a physical input mapped to an ability.
    /// Replaces hardcoded string action names.
    /// </summary>
    public abstract class GameplayInput : ScriptableObject
    {
        private int? _hash;

        /// <summary>
        /// Generates a deterministic hash based on the ScriptableObject's name.
        /// </summary>
        public int GetHash()
        {
            if (!_hash.HasValue)
            {
                _hash = Animator.StringToHash(name);
            }
            return _hash.Value;
        }

        /// <summary>
        /// A dynamically assigned index (0-63) used to pack this input into a bitmask over the network.
        /// Must be populated at startup by the InputService.
        /// </summary>
        public int BitIndex { get; set; } = -1;
    }
}
