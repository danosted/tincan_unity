#nullable enable
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// The first encounter zone (Assets/Settings/Encounters/EncounterZoneConfig): where it is, how big, and its round's
    /// timing. Built by TinCan > Dev > Encounters > Build Assets. Plan: .docs/plans/encounters.md (E1).
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterZoneConfig", menuName = "TinCan/Encounters/Encounter Zone Config")]
    public sealed class EncounterZoneConfig : ScriptableObject
    {
        [Tooltip("What the HUD calls it (\"Storm\").")]
        public string DisplayName = "Storm";
        [Tooltip("The networked zone object the server spawns (EncounterZoneNetworkMediator, its placeholder look).")]
        public GameObject? Prefab;
        [Tooltip("Its centre in the world, on the map (x, z): a disc, at every height.")]
        public Vector2 Centre = new(0f, 1500f);
        [Tooltip("Its radius, metres: from the wall in to the centre.")]
        [Min(10f)] public float Radius = 750f;
        [Tooltip("The cue, seconds: turning back out before it ends means nothing starts.")]
        [Min(0f)] public float WarningSeconds = 8f;
        [Tooltip("What it does once its round ends.")]
        public EncounterAfterEnd AfterEnd = EncounterAfterEnd.Rest;
        [Tooltip("How long it rests before it can be entered again (with AfterEnd Rest).")]
        [Min(0f)] public float RestSeconds = 120f;
        [Tooltip("How hard its event is, 1 to 3. The storm: at 1 its heart is still; harder storms' hearts move, their " +
                 "targets stay up less long and the cure needs more hits (storm-crossing.md, \"Difficulty\").")]
        [Range(1, 3)] public int Severity = 1;
    }
}
