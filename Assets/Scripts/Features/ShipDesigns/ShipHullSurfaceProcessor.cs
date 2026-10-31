#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: a frame's planked faces as <see cref="ShipHullSurface"/>s, in face order. The point of each is the centre of
    /// its largest triangle, which lies inside the face even when the face is not convex.
    /// </summary>
    public static class ShipHullSurfaceProcessor
    {
        public static IReadOnlyList<ShipHullSurface> Surfaces(ShipFrameShape shape)
        {
            var surfaces = new List<ShipHullSurface>();
            foreach (var face in shape.PlankFaces)
            {
                float area = 0f, largest = -1f;
                var point = Vector3.zero;
                foreach (var (a, b, c) in face.EachTriangle())
                {
                    float triangle = ShipFrameGeometry.Area(a, b, c);
                    area += triangle;
                    if (triangle <= largest) continue;
                    largest = triangle;
                    point = (a + b + c) / 3f;
                }

                if (largest <= 0f) continue;
                var normal = face.IsWall ? face.Facing.normalized : Vector3.up;
                surfaces.Add(new ShipHullSurface(face.Face.Id, face.Story, face.IsWall, point, normal, area));
            }

            return surfaces;
        }
    }
}
