#nullable enable
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: what the cursor's ray is on in frame mode. A node within <c>radius</c> of the ray wins (the one nearest the
    /// camera), then an edge within it, then a face the ray passes through (planked or open); a node or edge behind the
    /// first face hit is hidden by it. Separately, where the ray meets the floor plane at <c>planeY</c>. <c>ignoreNode</c>
    /// is left out (the node being dragged).
    /// </summary>
    public sealed class ShipyardFramePickProcessor
    {
        private const float Epsilon = 1e-6f;

        public ShipyardFramePick Pick(ShipFrameShape shape, ShipyardRay ray, float planeY, float radius, int ignoreNode = 0)
        {
            var origin = ray.Origin;
            var direction = ray.Direction.normalized;
            bool onPlane = TryPlane(origin, direction, planeY, out var planePoint);

            int bestFace = 0;
            float bestFaceT = float.MaxValue;
            foreach (var face in shape.Faces)
            {
                foreach (var (a, b, c) in face.EachTriangle())
                {
                    if (!Hits(origin, direction, a, b, c, out float t) || t >= bestFaceT) continue;
                    bestFace = face.Face.Id;
                    bestFaceT = t;
                }
            }

            // The first face hit hides what is behind it: a node or edge counts only up to it (and the pick radius past it).
            float reach = bestFace != 0 ? bestFaceT + radius : float.MaxValue;
            var frame = shape.Frame;
            int bestNode = 0;
            float bestNodeT = reach;
            foreach (var node in frame.Nodes)
            {
                if (node.Id == ignoreNode) continue;
                var p = frame.Position(node);
                float t = Vector3.Dot(p - origin, direction);
                if (t <= 0f || t >= bestNodeT || Vector3.Distance(origin + direction * t, p) > radius) continue;
                bestNode = node.Id;
                bestNodeT = t;
            }

            if (bestNode != 0) return new ShipyardFramePick(ShipyardFramePickKind.Node, bestNode, frame.Position(bestNode), onPlane, planePoint);

            int bestEdge = 0;
            float bestEdgeT = reach;
            var edgePoint = Vector3.zero;
            foreach (var edge in shape.Edges)
            {
                if (edge.Edge.A == ignoreNode || edge.Edge.B == ignoreNode) continue;
                var (t, onEdge, distance) = Closest(origin, direction, edge.From, edge.To);
                if (t <= 0f || t >= bestEdgeT || distance > radius) continue;
                bestEdge = edge.Edge.Id;
                bestEdgeT = t;
                edgePoint = onEdge;
            }

            if (bestEdge != 0) return new ShipyardFramePick(ShipyardFramePickKind.Edge, bestEdge, edgePoint, onPlane, planePoint);

            return bestFace != 0
                ? new ShipyardFramePick(ShipyardFramePickKind.Face, bestFace, origin + direction * bestFaceT, onPlane, planePoint)
                : new ShipyardFramePick(ShipyardFramePickKind.None, 0, Vector3.zero, onPlane, planePoint);
        }

        private static bool TryPlane(Vector3 origin, Vector3 direction, float planeY, out Vector3 point)
        {
            point = Vector3.zero;
            if (Mathf.Abs(direction.y) < 1e-4f) return false;
            float t = (planeY - origin.y) / direction.y;
            if (t <= 0f) return false;
            point = origin + direction * t;
            return true;
        }

        /// <summary>The ray's distance along itself at the closest approach to the segment, that point of the segment, and the gap.</summary>
        private static (float T, Vector3 OnSegment, float Distance) Closest(Vector3 origin, Vector3 direction, Vector3 from, Vector3 to)
        {
            var segment = to - from;
            var w = origin - from;
            float b = Vector3.Dot(direction, segment), c = Vector3.Dot(segment, segment);
            float d = Vector3.Dot(direction, w), e = Vector3.Dot(segment, w);
            float denominator = c - b * b;
            float s = denominator < Epsilon ? 0f : Mathf.Clamp01((e - b * d) / denominator);
            var onSegment = from + segment * s;
            float t = Vector3.Dot(onSegment - origin, direction);
            return (t, onSegment, Vector3.Distance(origin + direction * t, onSegment));
        }

        /// <summary>Möller-Trumbore, both sides.</summary>
        private static bool Hits(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;
            var ab = b - a;
            var ac = c - a;
            var p = Vector3.Cross(direction, ac);
            float det = Vector3.Dot(ab, p);
            if (Mathf.Abs(det) < Epsilon) return false;

            float inverse = 1f / det;
            var s = origin - a;
            float u = Vector3.Dot(s, p) * inverse;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f) return false;
            t = Vector3.Dot(ac, q) * inverse;
            return t > 0f;
        }
    }
}
