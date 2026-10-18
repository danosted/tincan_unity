#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A part's yaw, stored in a design as one byte: 0–3 quarter turns clockwise seen from above. Parts stand on floors or
    /// sit on nodes, so they never tip over.
    /// </summary>
    public static class ShipPartOrientation
    {
        public const int Count = 4;

        public static bool IsValid(int orientation) => orientation >= 0 && orientation < Count;

        /// <summary>The next quarter turn (the shipyard's rotate key).</summary>
        public static byte NextYaw(byte orientation) => (byte)((orientation + 1) % Count);

        public static Quaternion ToRotation(byte orientation) => Quaternion.Euler(0f, 90f * (IsValid(orientation) ? orientation : 0), 0f);

        /// <summary>Turns a block offset about the origin block, exactly.</summary>
        public static Vector3Int Rotate(Vector3Int offset, byte orientation) => (IsValid(orientation) ? orientation : 0) switch
        {
            1 => new Vector3Int(offset.z, offset.y, -offset.x),
            2 => new Vector3Int(-offset.x, offset.y, -offset.z),
            3 => new Vector3Int(-offset.z, offset.y, offset.x),
            _ => offset,
        };
    }
}
