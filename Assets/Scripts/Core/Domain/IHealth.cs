#nullable enable

namespace TinCan.Core.Domain
{
    public interface IHealth
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        float HealthPercentage { get; }
        bool IsBroken { get; }

        void ApplyDamage(float amount);
        void Repair(float amount);
    }
}