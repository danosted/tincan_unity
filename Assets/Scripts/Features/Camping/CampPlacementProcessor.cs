#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Pure placing rules: what a held item sets down, whether the aimed-at ground takes it, which way it faces (toward
    /// the player, so a tent's door opens on them), and the aim along a look.
    /// </summary>
    public static class CampPlacementProcessor
    {
        /// <summary>What the player can set down with what they carry, or null.</summary>
        public static CampPlaceable? Placing(bool holdingBundle, bool holdingTent) => (holdingBundle, holdingTent) switch
        {
            (true, _) => CampPlaceable.Campfire,
            (_, true) => CampPlaceable.Tent,
            _ => null
        };

        /// <summary>Does the ground take it: off the ship, flat enough, with room (asked last, only when the rest holds)?</summary>
        public static CampPlacementVerdict Judge(in CampGround ground, float maxSlopeDegrees, System.Func<bool> hasRoom)
        {
            if (ground.OnShip) return CampPlacementVerdict.OnTheShip;
            if (Vector3.Angle(ground.Normal, Vector3.up) > maxSlopeDegrees) return CampPlacementVerdict.TooSteep;
            return hasRoom() ? CampPlacementVerdict.Fits : CampPlacementVerdict.NoRoom;
        }

        /// <summary>Upright, its front (+Z) toward the eye.</summary>
        public static Quaternion FacingBack(Vector3 eye, Vector3 point)
        {
            var back = new Vector3(eye.x - point.x, 0f, eye.z - point.z);
            return back.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(back, Vector3.up);
        }

        /// <summary>The aim along a level facing, pitched down by <paramref name="pitch"/> degrees (up when negative).</summary>
        public static Vector3 AimDirection(Quaternion facing, float pitch) => facing * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;

        /// <summary>
        /// How far over the aimed point the room test starts: over the ground a disc of this radius can rise to on the
        /// steepest slope allowed, so the slope is not taken for something in the way.
        /// </summary>
        public static float RoomLift(float radius, float maxSlopeDegrees) => radius * Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) + 0.1f;
    }
}
