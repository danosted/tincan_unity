using TinCan.Core.Domain.Abilities.Tags;
using System;

namespace TinCan.Core.Domain.Abilities
{
    /// <summary>
    /// Data structure for passing gameplay events between actors and abilities.
    /// Used for chains, triggers, and state notifications.
    /// </summary>
    public struct GameplayEventData
    {
        public GameplayTag EventTag;
        public IAbilityControllerBase Instigator;
        public IAbilityControllerBase Target;
        public float Score; // 0.0 to 1.0 if applicable
    }
}
