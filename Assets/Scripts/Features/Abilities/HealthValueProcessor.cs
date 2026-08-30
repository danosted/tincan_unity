#nullable enable

using UnityEngine;

namespace TinCan.Features.Abilities
{
    public static class HealthValueProcessor
    {
        public static float ApplyDamage(float currentHealth, float maxHealth, float amount)
        {
            return Mathf.Clamp(currentHealth - Mathf.Max(0f, amount), 0f, Mathf.Max(0f, maxHealth));
        }

        public static float Repair(float currentHealth, float maxHealth, float amount)
        {
            return Mathf.Clamp(currentHealth + Mathf.Max(0f, amount), 0f, Mathf.Max(0f, maxHealth));
        }
    }
}