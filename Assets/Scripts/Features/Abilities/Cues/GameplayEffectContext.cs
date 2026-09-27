#nullable enable
namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// How an effect is being applied. Explicit rather than ambient: only the predicted ability simulation passes
    /// <see cref="Predicted"/>, so an owner-side apply outside prediction (the equipment binder) is never taken for one.
    /// </summary>
    public readonly struct GameplayEffectContext
    {
        public static readonly GameplayEffectContext Default = new(isPredicted: false);
        public static readonly GameplayEffectContext Predicted = new(isPredicted: true);

        /// <summary>True when the owner's predicted simulation applies it, and the server's run of the same input.</summary>
        public readonly bool IsPredicted;

        public GameplayEffectContext(bool isPredicted) => IsPredicted = isPredicted;
    }
}
