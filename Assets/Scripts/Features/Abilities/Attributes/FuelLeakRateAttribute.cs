using UnityEngine;
using TinCan.Core.Domain.Abilities.Attributes;

namespace TinCan.Features.Abilities
{
    /// <summary>Litres per second the ship loses regardless of driving; base 0, raised by hull-breach effects.</summary>
    [CreateAssetMenu(fileName = "FuelLeakRate", menuName = "TinCan/Abilities/Attributes/Fuel Leak Rate")]
    public class FuelLeakRateAttribute : GameplayAttribute { }
}
