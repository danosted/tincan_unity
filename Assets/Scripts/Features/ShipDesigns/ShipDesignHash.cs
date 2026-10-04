#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A stable 64-bit hash of a design's content (FNV-1a over its canonical network form), the same on every peer and
    /// platform, unlike <c>GetHashCode</c>. Two peers holding the same design report the same hash. Unknown fields
    /// (<see cref="ShipDesign.Extensions"/>) are not part of it.
    /// </summary>
    public static class ShipDesignHash
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(ShipDesign design)
        {
            ulong hash = Offset;
            foreach (var b in ShipDesignBinaryCodec.Encode(design))
            {
                hash ^= b;
                hash *= Prime;
            }

            return hash;
        }

        public static string ToText(ulong hash) => hash.ToString("x16");
    }
}
