namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Runtime state of an active gameplay effect. A Duration effect lasts a whole number of simulation ticks
    /// (<see cref="GameplayTicks"/>), counted from the tick it was applied on.
    /// </summary>
    public class ActiveGameplayEffect
    {
        public GameplayEffectDefinition Definition { get; }
        public int StartTick { get; }
        public int ExpiryTick { get; }

        public ActiveGameplayEffect(GameplayEffectDefinition definition, int currentTick, int tickRate)
        {
            Definition = definition;
            StartTick = currentTick;
            ExpiryTick = definition.DurationType == DurationType.Duration
                ? currentTick + GameplayTicks.FromSeconds(definition.DurationSeconds, tickRate)
                : int.MaxValue;
        }

        public bool IsExpired(int currentTick)
        {
            if (Definition.DurationType == DurationType.Instant) return true;
            if (Definition.DurationType == DurationType.Infinite) return false;
            return currentTick >= ExpiryTick;
        }
    }
}
