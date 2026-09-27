#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Core.Domain.Cues
{
    /// <summary>One cue happening on one peer: which cue, on which actor, and how this peer relates to that actor.</summary>
    public readonly struct GameplayCueEvent
    {
        public readonly GameplayTag Cue;
        /// <summary>The target actor's transform: parent spatial presentation here so it stays ship-local. Null when the
        /// controller is not a component (tests) or is being destroyed.</summary>
        public readonly Transform? Target;
        public readonly IAbilityControllerBase Controller;
        public readonly GameplayCuePeerRole Role;

        public GameplayCueEvent(GameplayTag cue, Transform? target, IAbilityControllerBase controller, GameplayCuePeerRole role)
        {
            Cue = cue;
            Target = target;
            Controller = controller;
            Role = role;
        }
    }
}
