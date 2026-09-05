#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Tunables for the flying jerry-can minigame. Offsets are in airship-local space (x = starboard, y = up, z = forward).
    /// </summary>
    [CreateAssetMenu(fileName = "FlyingCanConfig", menuName = "TinCan/Airship/Flying Can Config")]
    public class FlyingCanConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Networked prefab with FlyingCanNetworkMediator + NetworkTransformMediator. Must also be in DefaultNetworkPrefabs.")]
        public GameObject? CanPrefab;

        [Header("Waves")]
        public bool Enabled = true;
        [Tooltip("Only spawn while someone is at the helm and throttling.")]
        public bool SpawnOnlyWhileDriven = false;
        [Min(0.1f)] public float SpawnInterval = 4f;
        [Range(1, 12)] public int MaxAlive = 4;
        [Min(0f)] public float Lifetime = 15f;

        [Header("Spawn volume (ship-local)")]
        [Min(1f)] public float AheadDistance = 60f;
        public float LateralMin = 4.5f;
        public float LateralMax = 6.5f;
        public float HeightMin = -2.5f;
        public float HeightMax = -1.0f;

        [Header("Motion")]
        [Min(0.1f)] public float CanSpeed = 8f;

        [Header("Catch (handheld net)")]
        [Tooltip("Tag the player carries while the net swing effect is active (State.Net.Swinging).")]
        public GameplayTag? SwingingTag;
        [Tooltip("How far in front of the player the net head is, in metres.")]
        [Min(0f)] public float NetReach = 1.8f;
        [Tooltip("Net head height above the player pivot.")]
        public float NetHeight = 1.0f;
        [Tooltip("A can within this distance of the net head is caught. Generous: the client sees interpolated cans.")]
        [Min(0.1f)] public float CatchRadius = 2.5f;

        public FlyingCanSpawnParameters SpawnParameters => new(AheadDistance, LateralMin, LateralMax, HeightMin, HeightMax, CanSpeed);
    }
}
