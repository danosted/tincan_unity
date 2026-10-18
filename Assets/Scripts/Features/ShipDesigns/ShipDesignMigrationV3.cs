#nullable enable
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Version 3 to 4: parts hang on the frame instead of standing at a position. A part whose origin block rests on a plank
    /// floor (its underside within <see cref="Tolerance"/> of the floor under its centre) goes on that face at its x and z;
    /// one whose underside is at a frame node goes on the node. A part that cannot be anchored (every part of a design
    /// without a frame, a part standing on another part, one tipped over) is moved, whole, to the top-level
    /// <c>dropped</c> list: kept in the file, never built. Railings written as true still read as railings. Golden file:
    /// <c>v3-golden.ship.json</c>. Plan: .docs/plans/frame-only-ships.md.
    /// </summary>
    public sealed class ShipDesignMigrationV3 : IShipDesignMigration
    {
        /// <summary>How far (m) a part's underside may be from the floor or node it is given.</summary>
        public const float Tolerance = 0.25f;

        public int FromVersion => 3;

        public void Migrate(JObject file)
        {
            if (file[ShipDesignFormat.PartsField] is not JArray parts) return;

            var shape = ShipFrameShape.From(ReadFrame(file));
            var dropped = new JArray();
            foreach (var token in parts.ToList())
            {
                if (token is not JObject part) continue;
                JObject? anchor = null;
                if (part[ShipDesignFormat.V3PartPosField] is JArray pos && pos.Count == 3 && pos.All(v => v.Type == JTokenType.Integer) && Upright(part))
                {
                    anchor = Anchor(shape, pos[0].Value<int>(), pos[1].Value<int>(), pos[2].Value<int>());
                }

                if (anchor == null)
                {
                    parts.Remove(part);
                    dropped.Add(part);
                    continue;
                }

                part.Remove(ShipDesignFormat.V3PartPosField);
                part[ShipDesignFormat.PartAnchorField] = anchor;
            }

            if (dropped.Count > 0) file[ShipDesignFormat.DroppedField] = dropped;
        }

        private static ShipFrame ReadFrame(JObject file) =>
            file[ShipDesignFormat.FrameField] is JObject frame && ShipFrameJson.TryRead(frame, ShipDesignLimits.Default, out var read) == null
                ? read
                : ShipFrame.Empty;

        /// <summary>Version 3's orientations 0–3 are the upright yaws; the rest tipped the part over.</summary>
        private static bool Upright(JObject part) =>
            part[ShipDesignFormat.PartOrientationField] is not { } rot || (rot.Type == JTokenType.Integer && rot.Value<int>() is >= 0 and < 4);

        /// <summary>The face or node under the origin block's underside (its centre was <c>pos</c>, half a metre above it).</summary>
        private static JObject? Anchor(ShipFrameShape shape, int x, int y, int z)
        {
            var underside = new Vector3(ShipFrameUnits.ToMetres(x), ShipFrameUnits.ToMetres(y) - 0.5f, ShipFrameUnits.ToMetres(z));
            foreach (var face in shape.PlankFloors)
            {
                foreach (var (a, b, c) in face.EachTriangle())
                {
                    if (!ShipFrameGeometry.TryHeightAt(underside.x, underside.z, a, b, c, out float height) || Mathf.Abs(height - underside.y) > Tolerance) continue;
                    return new JObject
                    {
                        [ShipDesignFormat.AnchorFaceField] = face.Face.Id,
                        [ShipDesignFormat.AnchorXField] = x,
                        [ShipDesignFormat.AnchorZField] = z,
                    };
                }
            }

            foreach (var node in shape.Frame.Nodes)
            {
                if (Vector3.Distance(shape.Frame.Position(node), underside) > Tolerance) continue;
                return new JObject { [ShipDesignFormat.AnchorNodeField] = node.Id };
            }

            return null;
        }
    }
}
