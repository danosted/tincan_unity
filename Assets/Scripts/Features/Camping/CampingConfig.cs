#nullable enable
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Items;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// The camping tunables: the items (firewood, the campfire bundle, the tent roll), how long a log burns, how fast
    /// deadwood gathers again, and how a player ties a bundle and places a campfire or tent. Each camp part and the hearth
    /// read it from their own field, so a spawned prefab needs no injection.
    /// </summary>
    [CreateAssetMenu(fileName = "CampingConfig", menuName = "TinCan/Camping/Camping Config")]
    public class CampingConfig : ScriptableObject
    {
        [Header("Items")]
        [Tooltip("What deadwood gives and what a fire burns.")]
        public ItemDefinition? FirewoodItem;
        [Tooltip("A log tied up to be set down as a campfire.")]
        public ItemDefinition? BundleItem;
        [Tooltip("What is set down as a tent, and what packing one gives back.")]
        public ItemDefinition? TentItem;

        [Header("Fire")]
        [Tooltip("How many seconds one log keeps a fire burning.")]
        [Min(1f)] public float BurnSecondsPerLog = 180f;
        [Tooltip("The most a fire holds, in seconds of burning. A fire with less than half a log of room refuses another.")]
        [Min(1f)] public float MaxBurnSeconds = 600f;

        [Header("Deadwood")]
        [Tooltip("How many logs a deadwood pile (a tree's canopy) holds when it is full.")]
        [Min(1)] public int DeadwoodLogs = 3;
        [Tooltip("Seconds for a pile to gather one log again.")]
        [Min(1f)] public float DeadwoodRegrowSeconds = 120f;

        [Header("Placing")]
        [Tooltip("The ability input that ties a held log into a bundle, and sets a held bundle or tent roll down.")]
        public GameplayInput? PlaceInput;
        [Tooltip("The carrying tags of the three items, so the predicted input and the preview know what is held.")]
        public GameplayTag? CarryingFirewoodTag;
        public GameplayTag? CarryingBundleTag;
        public GameplayTag? CarryingTentTag;
        [Tooltip("The networked campfire set down from a bundle (unlit; a log lights it).")]
        public GameObject? CampfirePrefab;
        [Tooltip("The networked tent set down from a roll (pitched; packing it gives the roll back and takes it away).")]
        public GameObject? TentPrefab;
        [Tooltip("What the local player sees where a campfire or tent would go (its renderers take the fits / does not fit material).")]
        public GameObject? CampfirePreview;
        public GameObject? TentPreview;
        public Material? FitsMaterial;
        public Material? NoFitMaterial;
        [Tooltip("How far from the eye a campfire or tent can be set down, metres.")]
        [Min(1f)] public float PlaceReach = 4.5f;
        [Tooltip("The steepest ground a campfire or tent stands on, degrees.")]
        [Range(0f, 60f)] public float MaxSlopeDegrees = 25f;
        [Tooltip("The room each keeps clear around it, metres.")]
        [Min(0.2f)] public float CampfireRadius = 0.8f;
        [Min(0.2f)] public float TentRadius = 1.3f;
        [Tooltip("How high above the ground that room must be clear, metres.")]
        [Min(0.2f)] public float ClearHeight = 1.6f;

        public float RadiusOf(CampPlaceable kind) => kind == CampPlaceable.Tent ? TentRadius : CampfireRadius;
    }
}
