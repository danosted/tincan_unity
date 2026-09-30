#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.Domain.Abilities.Inputs
{
    /// <summary>
    /// An ability input: one bit of the predicted input state (<see cref="BitIndex"/>) that abilities and simulation
    /// systems test instead of keys. It is pressed while any of its <see cref="Actions"/> is pressed in an active
    /// context, so one bit can come from different actions in different situations (Primary on foot, Fire at a cannon).
    /// The bit order is the input config's ability-input list, the same on every peer.
    /// </summary>
    public abstract class GameplayInput : ScriptableObject
    {
        [Tooltip("The actions that press this input, one per context it is used in.")]
        [SerializeField] private List<InputActionId> _actions = new();

        private int? _hash;

        public IReadOnlyList<InputActionId> Actions => _actions;

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
        /// Assigned at startup by the input system from the input config.
        /// </summary>
        public int BitIndex { get; set; } = -1;

        /// <summary>Builder and test helper.</summary>
        public void SetActions(IEnumerable<InputActionId> actions) => _actions = new List<InputActionId>(actions);
    }
}
