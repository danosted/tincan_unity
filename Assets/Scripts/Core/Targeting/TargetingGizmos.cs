#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Core.Targeting
{
    /// <summary>One coloured segment of a targeting visualisation.</summary>
    public readonly struct GizmoLine
    {
        public readonly Vector3 From;
        public readonly Vector3 To;
        public readonly Color Color;

        public GizmoLine(Vector3 from, Vector3 to, Color color)
        {
            From = from;
            To = to;
            Color = color;
        }
    }

    /// <summary>
    /// Shows what a targeting query sees: the aim source, the definition's actual shape (ray, cone edges and arcs, or
    /// sphere rings) and a line to the acquired target. Green when something was acquired, red when not. The geometry is
    /// built once as lines (pure, tested) and drawn either as Debug lines (Scene view, and Game view with Gizmos on) or
    /// from OnDrawGizmos.
    /// </summary>
    public static class TargetingGizmos
    {
        public static readonly Color Hit = new(0.2f, 0.9f, 0.3f);
        public static readonly Color Miss = new(0.95f, 0.25f, 0.2f);
        public static readonly Color Target = new(1f, 0.85f, 0.1f);

        private const int ArcSegments = 8;
        private const int RingSegments = 16;
        private const float SourceMarker = 0.08f;
        private const float TargetMarker = 0.15f;

        private static readonly TargetingProcessor Processor = new();
        private static readonly List<GizmoLine> Scratch = new();

        public static void Build(in TargetingOrigin origin, TargetingDefinition definition, ITargetable? target, List<GizmoLine> lines)
        {
            lines.Clear();
            Color shape = target != null ? Hit : Miss;
            Vector3 source = Processor.SourcePoint(origin, definition);
            Vector3 direction = Processor.Direction(origin, definition).normalized;
            Vector3 up = origin.Up;
            Vector3 right = Vector3.Cross(up, direction);
            right = right.sqrMagnitude > 0.0001f ? right.normalized : origin.BodyRotation * Vector3.right;

            Cross(lines, source, SourceMarker, shape);

            switch (definition.Shape)
            {
                case TargetShape.Sphere:
                    Ring(lines, source, up, right, definition.Radius, shape);
                    Ring(lines, source, right, direction, definition.Radius, shape);
                    Ring(lines, source, direction, up, definition.Radius, shape);
                    break;
                case TargetShape.Cone:
                    Cone(lines, source, direction, up, right, definition, shape);
                    break;
                default:
                    lines.Add(new GizmoLine(source, source + direction * definition.Range, shape));
                    break;
            }

            if (target == null) return;
            lines.Add(new GizmoLine(source, target.AimPoint, Target));
            Cross(lines, target.AimPoint, TargetMarker, Target);
        }

        /// <summary>
        /// Draws with Debug.DrawLine (safe from any code; Scene view, and Game view with Gizmos on). With the default 0 the
        /// lines last one frame, right for per-frame callers. Callers on the network tick pass a hold time, or the lines
        /// would show only on the few frames a tick ran.
        /// </summary>
        public static void DrawDebug(in TargetingOrigin origin, TargetingDefinition definition, ITargetable? target, float seconds = 0f)
        {
            TargetingDebug.RecordDrawn(definition, target != null);
            Build(origin, definition, target, Scratch);
            foreach (var line in Scratch) Debug.DrawLine(line.From, line.To, line.Color, seconds);
        }

        /// <summary>Draws with the Gizmos API; call from OnDrawGizmos.</summary>
        public static void DrawGizmos(in TargetingOrigin origin, TargetingDefinition definition, ITargetable? target)
        {
            Build(origin, definition, target, Scratch);
            foreach (var line in Scratch)
            {
                Gizmos.color = line.Color;
                Gizmos.DrawLine(line.From, line.To);
            }
        }

        private static void Cone(List<GizmoLine> lines, Vector3 source, Vector3 direction, Vector3 up, Vector3 right, TargetingDefinition definition, Color color)
        {
            float halfWidth = Mathf.Min(definition.HorizontalAngle, 359f) * 0.5f;
            float halfHeight = definition.VerticalAngle * 0.5f;
            float range = definition.Range;

            lines.Add(new GizmoLine(source, source + direction * range, color));
            Arc(lines, source, direction, up, -halfWidth, halfWidth, range, color, withEdges: true);
            Arc(lines, source, direction, right, -halfHeight, halfHeight, range, color, withEdges: true);
        }

        private static void Arc(List<GizmoLine> lines, Vector3 source, Vector3 direction, Vector3 axis, float from, float to, float radius, Color color, bool withEdges)
        {
            Vector3 previous = source + Quaternion.AngleAxis(from, axis) * direction * radius;
            if (withEdges) lines.Add(new GizmoLine(source, previous, color));

            for (int i = 1; i <= ArcSegments; i++)
            {
                float angle = Mathf.Lerp(from, to, i / (float)ArcSegments);
                Vector3 next = source + Quaternion.AngleAxis(angle, axis) * direction * radius;
                lines.Add(new GizmoLine(previous, next, color));
                previous = next;
            }

            if (withEdges) lines.Add(new GizmoLine(source, previous, color));
        }

        private static void Ring(List<GizmoLine> lines, Vector3 centre, Vector3 axis, Vector3 start, float radius, Color color)
        {
            Vector3 previous = centre + start * radius;
            for (int i = 1; i <= RingSegments; i++)
            {
                Vector3 next = centre + Quaternion.AngleAxis(360f * i / RingSegments, axis) * start * radius;
                lines.Add(new GizmoLine(previous, next, color));
                previous = next;
            }
        }

        private static void Cross(List<GizmoLine> lines, Vector3 point, float size, Color color)
        {
            lines.Add(new GizmoLine(point - Vector3.right * size, point + Vector3.right * size, color));
            lines.Add(new GizmoLine(point - Vector3.up * size, point + Vector3.up * size, color));
            lines.Add(new GizmoLine(point - Vector3.forward * size, point + Vector3.forward * size, color));
        }
    }

    /// <summary>
    /// Dev switch: draw every query the targeting service runs (all players, server and owner). Off by default; toggled
    /// from TinCan > Dev > Targeting > Draw All Queries. Stored as a flag file in the main project's Library (not
    /// EditorPrefs, which MPPM clones do not share), so the host and every virtual player see the same switch, and it
    /// survives entering Play mode. Always off in builds.
    /// </summary>
    public static class TargetingDebug
    {
        private const float RecheckSeconds = 0.5f;

        private static float _checkedAt = float.NegativeInfinity;
        private static bool _cached;
        private static readonly Dictionary<string, (int Frame, bool Acquired)> Drawn = new();

        /// <summary>Records that a query was drawn, so tooling can confirm what was visualised (debug lines cannot be read back).</summary>
        public static void RecordDrawn(TargetingDefinition definition, bool acquired) => Drawn[definition.name] = (Time.frameCount, acquired);

        /// <summary>The last frame a definition was drawn on, and whether it had acquired something; null if never.</summary>
        public static (int Frame, bool Acquired)? LastDrawn(string definitionName) =>
            Drawn.TryGetValue(definitionName, out var drawn) ? drawn : null;

        /// <summary>
        /// <c>&lt;main project&gt;/Library/TinCanDev/DrawAllTargetingQueries</c>. MPPM clones run from
        /// <c>Library/VP/&lt;id&gt;</c>, so the path is resolved to the main project (same rule as the harness reports).
        /// </summary>
        public static string FlagPath
        {
            get
            {
                string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
                int clone = root.IndexOf("/Library/VP/", System.StringComparison.OrdinalIgnoreCase);
                if (clone >= 0) root = root.Substring(0, clone);
                return System.IO.Path.Combine(root, "Library", "TinCanDev", "DrawAllTargetingQueries");
            }
        }

        /// <summary>Turns the switch on or off for every Editor of this project (main and MPPM clones).</summary>
        public static void SetDrawAllQueries(bool on)
        {
            string path = FlagPath;
            if (on)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, "on");
            }
            else if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
            _checkedAt = float.NegativeInfinity;
        }

        public static bool DrawAllQueries
        {
            get
            {
#if UNITY_EDITOR
                float now = Time.realtimeSinceStartup;
                if (now - _checkedAt < RecheckSeconds) return _cached;
                _checkedAt = now;
                _cached = System.IO.File.Exists(FlagPath);
                return _cached;
#else
                return false;
#endif
            }
        }
    }
}
