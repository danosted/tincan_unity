#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Tunables for scattered stationary fuel pickups. New batches follow the ship's travel direction.
    /// </summary>
    [CreateAssetMenu(fileName = "FlyingCanConfig", menuName = "TinCan/Airship/Flying Can Config")]
    public class FlyingCanConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Networked prefab registered by FlyingCanFeatureInstaller.")]
        public GameObject? CanPrefab;

        [Header("World pickups")]
        public bool Enabled = true;
        [Tooltip("Travel distance between new batches of two cans. Also spaces the initial spawn volumes.")]
        [Min(1f)] public float RowSpacing = 20f;
        [Tooltip("Maximum live pickups. Nearby cans are never removed just to make room.")]
        [Min(2)] public int MaxAlive = 48;
        [Tooltip("Remove cans only beyond this distance from every simulating ship. No age limit.")]
        [Min(1f)] public float DespawnDistance = 200f;
        [Tooltip("Avoid duplicate pickups when revisiting an existing field.")]
        [Min(0.1f)] public float MinimumSeparation = 10f;
        [Tooltip("No new can may spawn within this radius of any ship's centre. Includes room for the hull and balloon.")]
        [Min(0f)] public float MinimumShipDistance = 50f;

        [Header("Spawn volume (ship-local)")]
        [Tooltip("Distance at which new cans appear in the travel direction; tune to the visible horizon.")]
        [Min(1f)] public float AheadDistance = 120f;
        [Tooltip("Centre of the nearest initial spawn volume, ahead of the ship.")]
        [Min(0f)] public float InitialAheadDistance = 60f;
        [Tooltip("Random forward/backward offset within each spawn volume, so cans do not form rows.")]
        [Min(0f)] public float DepthSpread = 15f;
        public float LateralMin = 0f;
        public float LateralMax = 55f;
        public float HeightMin = -25f;
        public float HeightMax = 25f;

        [Header("Catch (handheld net)")]
        [Tooltip("Tag the player carries while the net swing effect is active (State.Net.Swinging).")]
        public GameplayTag? SwingingTag;
        [Tooltip("How far in front of the player the net head is, in metres.")]
        [Min(0f)] public float NetReach = 1.8f;
        [Tooltip("Net head height above the player pivot.")]
        public float NetHeight = 1.0f;
        [Tooltip("A can within this distance of the net head is caught. Generous: the client sees interpolated cans.")]
        [Min(0.1f)] public float CatchRadius = 2.5f;

        public FlyingCanSpawnParameters SpawnParameters => new(LateralMin, LateralMax, HeightMin, HeightMax, DepthSpread);
    }
}
