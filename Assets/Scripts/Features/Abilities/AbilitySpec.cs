using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Runtime state of a granted ability. Times are simulation ticks (<see cref="GameplayTicks"/>).
    /// </summary>
    public class AbilitySpec
    {
        public AbilityDefinition Definition { get; }
        public int? LastActivatedTick { get; private set; } // null until the first activation: no cooldown yet
        public int StartTick { get; private set; }
        public bool IsActive { get; set; }
        public HashSet<GameplayTag> ActiveWindowTags { get; } = new();
        public ActiveGameplayEffect AppliedActiveEffect { get; set; }
        public TinCan.Core.Domain.Abilities.IAbilityControllerBase EffectRecipient { get; set; }

        public AbilitySpec(AbilityDefinition definition)
        {
            Definition = definition;
        }

        public bool IsOnCooldown(int currentTick, int tickRate)
        {
            if (Definition.CooldownEffect == null || LastActivatedTick == null) return false;
            return currentTick < LastActivatedTick.Value + GameplayTicks.FromSeconds(Definition.CooldownEffect.DurationSeconds, tickRate);
        }

        public void Activate(int currentTick)
        {
            IsActive = true;
            StartTick = currentTick;
            LastActivatedTick = currentTick;
            ActiveWindowTags.Clear();
        }
    }
}
