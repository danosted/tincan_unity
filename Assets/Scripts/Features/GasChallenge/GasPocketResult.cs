#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    public readonly struct GasPocketResult
    {
        public bool IsInGas { get; }
        public float DamageThisTick { get; }
        public float WarningLevel { get; }

        public GasPocketResult(bool isInGas, float damageThisTick, float warningLevel)
        {
            IsInGas = isInGas;
            DamageThisTick = damageThisTick;
            WarningLevel = Mathf.Clamp01(warningLevel);
        }
    }
}
