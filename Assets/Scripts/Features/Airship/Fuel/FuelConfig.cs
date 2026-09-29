#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Tunables for the airship fuel loop. Referenced by the FuelTankNetworkMediator on the ship prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "FuelConfig", menuName = "TinCan/Airship/Fuel Config")]
    public class FuelConfig : ScriptableObject
    {
        [Header("Tank")]
        [Min(1f)] public float Capacity = 100f;
        [Min(0f)] public float InitialLevel = 100f;

        [Header("Consumption")]
        [Tooltip("Fuel units burned per second at full throttle (forward or reverse).")]
        [Min(0f)] public float DrainPerSecondAtFullThrottle = 0.75f;
        [Tooltip("Multiplier applied while the ship carries the boost tag.")]
        [Min(1f)] public float BoostMultiplier = 2f;

        [Header("Refuel")]
        [Min(0f)] public float JerryCanLitres = 25f;
        [Min(0)] public int InitialSupply = 1;

        [Tooltip("The item a player holds while carrying a jerry can (taken from the crate, poured into the motor).")]
        public TinCan.Core.Items.ItemDefinition? JerryCanItem;

        [Tooltip("Attribute on the ship holding the current leak rate (litres per second, base 0). Hull breaches raise it; the tank drains it even while parked.")]
        public TinCan.Core.Gas.FuelLeakRateAttribute? LeakRateAttribute;
        [Tooltip("Slice 1 stopgap: the motor refuels on interact without a jerry can. Turn off once the carry loop exists.")]
        public bool DebugFreeRefuel = true;

        [Header("Stall")]
        public bool StallWhenEmpty = true;
        [Tooltip("Tag the ship carries while boosting (State.FlightBoost.IsActive).")]
        public GameplayTag? BoostActiveTag;
        [Tooltip("Tag granted by the stall effect (State.Engine.Stalled). Used as the source of truth for stall state.")]
        public GameplayTag? StalledTag;
        [Tooltip("Toggleable ability whose Infinite ActiveEffect overrides Attr_FlightSpeed to 0 and grants StalledTag.")]
        public AbilityDefinition? StallAbility;
    }
}
