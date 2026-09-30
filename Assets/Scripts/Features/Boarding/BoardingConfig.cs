#nullable enable
using UnityEngine;

namespace TinCan.Features.Boarding
{
    /// <summary>
    /// Tunables for boarding (Assets/Settings/Boarding/BoardingConfig). Plan: .docs/plans/crew-gate-and-boarding.md.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardingConfig", menuName = "TinCan/Airship/Boarding Config")]
    public class BoardingConfig : ScriptableObject
    {
        [Tooltip("Where a joining player lands, in ship-local space, when the ship has no AirshipRespawnPoint. " +
                 "Starts equal to CloudBoundaryConfig's fallback respawn offset, so both land players on the same spot.")]
        public Vector3 BoardingOffset = new(0f, 3f, 0f);
    }
}
