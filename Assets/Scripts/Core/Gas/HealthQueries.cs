#nullable enable
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// Health state read from an actor's GAS controller: the <see cref="HealthAttributeSet"/> it registered. Features ask
    /// the controller instead of mirroring health on their own interfaces, so health has one source (its attributes).
    /// </summary>
    public static class HealthQueries
    {
        public static bool TryGetHealth(this IAbilityControllerBase? controller, out HealthAttributeSet health)
        {
            health = null!;
            return controller != null && controller.TryGetAttributeSet(out health);
        }

        /// <summary>Below max health. False without registered health.</summary>
        public static bool IsDamaged(this IAbilityControllerBase? controller) =>
            controller.TryGetHealth(out var health) && health.IsDamaged;

        /// <summary>No health left. False without registered health.</summary>
        public static bool IsDepleted(this IAbilityControllerBase? controller) =>
            controller.TryGetHealth(out var health) && health.IsDepleted;
    }
}
