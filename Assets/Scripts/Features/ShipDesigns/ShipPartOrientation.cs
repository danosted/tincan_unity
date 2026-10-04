#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The 24 axis-aligned rotations a part can have, stored in a design as one byte. Index = up face * 4 + quarter
    /// turns about that up axis; up faces in order +Y, -Y, +X, -X, +Z, -Z. So 0–3 are the four yaw turns of an upright
    /// part, the only ones the first shipyard offers; the rest are there so walls and ceilings need no new format.
    /// </summary>
    public static class ShipPartOrientation
    {
        public const int Count = 24;

        private static readonly Quaternion[] UpRotations =
        {
            Quaternion.identity,
            Quaternion.Euler(180f, 0f, 0f),
            Quaternion.Euler(0f, 0f, -90f),
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(90f, 0f, 0f),
            Quaternion.Euler(-90f, 0f, 0f),
        };

        private static readonly Quaternion[] Rotations = new Quaternion[Count];
        private static readonly Vector3Int[] Right = new Vector3Int[Count];
        private static readonly Vector3Int[] Up = new Vector3Int[Count];
        private static readonly Vector3Int[] Forward = new Vector3Int[Count];

        static ShipPartOrientation()
        {
            for (int i = 0; i < Count; i++)
            {
                var rotation = UpRotations[i / 4] * Quaternion.Euler(0f, 90f * (i % 4), 0f);
                Rotations[i] = rotation;
                Right[i] = Vector3Int.RoundToInt(rotation * Vector3.right);
                Up[i] = Vector3Int.RoundToInt(rotation * Vector3.up);
                Forward[i] = Vector3Int.RoundToInt(rotation * Vector3.forward);
            }
        }

        public static bool IsValid(int orientation) => orientation >= 0 && orientation < Count;

        /// <summary>The next yaw turn of the same up face (the shipyard's rotate key).</summary>
        public static byte NextYaw(byte orientation) => (byte)(orientation / 4 * 4 + (orientation % 4 + 1) % 4);

        public static Quaternion ToRotation(byte orientation) => Rotations[IsValid(orientation) ? orientation : 0];

        /// <summary>Rotates a cell offset about the origin cell, exactly, in integers.</summary>
        public static ShipGridCell Rotate(Vector3Int offset, byte orientation)
        {
            int o = IsValid(orientation) ? orientation : 0;
            var rotated = Right[o] * offset.x + Up[o] * offset.y + Forward[o] * offset.z;
            return ShipGridCell.From(rotated);
        }
    }
}
