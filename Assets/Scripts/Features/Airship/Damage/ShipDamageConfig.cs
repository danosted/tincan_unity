#nullable enable
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Tunables for random breakage on the ship. Assigned on the ShipDamage installer and injected.</summary>
    [CreateAssetMenu(fileName = "ShipDamageConfig", menuName = "TinCan/Airship/Ship Damage Config")]
    public class ShipDamageConfig : ScriptableObject
    {
        [Tooltip("Parts break on their own. Scenarios switch this off and break parts on purpose.")]
        public bool AutoBreak = true;

        [Tooltip("Seconds after the ship first simulates before anything can break.")]
        [Min(0f)] public float FirstBreakDelay = 45f;

        [Tooltip("Seconds between breaks, picked uniformly in this range.")]
        [Min(1f)] public float MinInterval = 40f;
        [Min(1f)] public float MaxInterval = 90f;

        [Tooltip("No new break while this many parts are already broken.")]
        [Min(1)] public int MaxBroken = 2;

        [Tooltip("0 picks a new seed each session; any other value makes the break order repeatable.")]
        public int Seed;

        [Tooltip("Infinite effect applied to the ship per broken part (e.g. +fuel leak, grants State.Ship.Damaged). Removed on repair.")]
        public GameplayEffectDefinition? HullBreachEffect;

        [Tooltip("Infinite effect on the part itself while it is broken (grants State.Damaged, for cues and repair targeting). Removed on repair.")]
        public GameplayEffectDefinition? PartBrokenEffect;

        [Tooltip("Instant effect that breaks a part (sets its health to 0).")]
        public GameplayEffectDefinition? BreakEffect;

        [Tooltip("Instant effect that fully restores a part (health to max). Used by scenarios and debug tools; players repair with the tool.")]
        public GameplayEffectDefinition? RestoreEffect;

        [Header("Repairing (the repair tool)")]
        [Tooltip("Tag a player carries while the repair ability is active (granted by GA_RepairShip's active effect).")]
        public TinCan.Core.Domain.Abilities.Tags.GameplayTag? RepairingTag;

        [Tooltip("Instant effect applied to the targeted part every RepairInterval while repairing (adds health, clamped to max).")]
        public GameplayEffectDefinition? RepairEffect;

        [Min(0.05f)] public float RepairInterval = 0.25f;

        [Tooltip("The repair tool's ability. Its TargetingDefinition (TD_RepairScan) decides which broken part a repairing player works on.")]
        public AbilityDefinition? RepairAbility;

        [Header("Repair aim (the local player's view)")]
        [Tooltip("How far the marker of the broken part the repair tool points at moves from its own colour toward white, " +
                 "shown while the tool is held, trigger or not. 0: no highlight.")]
        [Range(0f, 1f)] public float AimHighlightBrightness = 0.45f;
    }
}
