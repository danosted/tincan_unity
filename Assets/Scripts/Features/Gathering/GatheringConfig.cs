#nullable enable
using TinCan.Core.Items;
using UnityEngine;

namespace TinCan.Features.Gathering
{
    /// <summary>
    /// The gathering tunables (Assets/Settings/Gathering/GatheringConfig, built by TinCan > Dev > Gathering > Build
    /// Assets). How long mining takes is the interaction's hold time (IA_MineCrystal). Plan: .docs/plans/fuel-gathering.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GatheringConfig", menuName = "TinCan/Gathering/Gathering Config")]
    public class GatheringConfig : ScriptableObject
    {
        [Tooltip("What mining a vein puts in your hands: a fuel crystal.")]
        public ItemDefinition? CrystalItem;

        [Tooltip("Crystals a full vein holds.")]
        [Min(1)] public int VeinCrystals = 3;

        [Tooltip("Seconds for a vein to grow one crystal back.")]
        [Min(1f)] public float VeinRegrowSeconds = 240f;
    }
}
