#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The frame section of a design file: its base, story heights, then one line per node (<c>id</c>, <c>story</c>,
    /// <c>pos</c> [x, drift, z]), edge (<c>id</c>, <c>a</c>, <c>b</c>, <c>rail</c> only when true) and face (<c>id</c>,
    /// <c>nodes</c>, <c>fill</c>), each in id order. Reading checks the counts against the limits before building anything.
    /// Unknown fields inside the frame are not kept (the design's top-level ones are). Plan: .docs/plans/ship-frames.md.
    /// </summary>
    public static class ShipFrameJson
    {
        public static void Write(JsonWriter writer, ShipFrame frame)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(ShipDesignFormat.FrameBaseField);
            writer.WriteValue(frame.Base);
            writer.WritePropertyName(ShipDesignFormat.FrameStoriesField);
            writer.WriteStartArray();
            Inline(writer, () =>
            {
                foreach (int height in frame.StoryHeights) writer.WriteValue(height);
                writer.WriteEndArray();
            });

            writer.WritePropertyName(ShipDesignFormat.FrameNodesField);
            writer.WriteStartArray();
            foreach (var node in frame.Nodes)
            {
                Line(writer, () =>
                {
                    writer.WritePropertyName(ShipDesignFormat.ElementIdField);
                    writer.WriteValue(node.Id);
                    writer.WritePropertyName(ShipDesignFormat.NodeStoryField);
                    writer.WriteValue(node.Story);
                    writer.WritePropertyName(ShipDesignFormat.NodePositionField);
                    writer.WriteStartArray();
                    writer.WriteValue(node.X);
                    writer.WriteValue(node.Y);
                    writer.WriteValue(node.Z);
                    writer.WriteEndArray();
                });
            }

            writer.WriteEndArray();

            writer.WritePropertyName(ShipDesignFormat.FrameEdgesField);
            writer.WriteStartArray();
            foreach (var edge in frame.Edges)
            {
                Line(writer, () =>
                {
                    writer.WritePropertyName(ShipDesignFormat.ElementIdField);
                    writer.WriteValue(edge.Id);
                    writer.WritePropertyName(ShipDesignFormat.EdgeAField);
                    writer.WriteValue(edge.A);
                    writer.WritePropertyName(ShipDesignFormat.EdgeBField);
                    writer.WriteValue(edge.B);
                    if (!edge.Railing) return;
                    writer.WritePropertyName(ShipDesignFormat.EdgeRailingField);
                    writer.WriteValue(true);
                });
            }

            writer.WriteEndArray();

            writer.WritePropertyName(ShipDesignFormat.FrameFacesField);
            writer.WriteStartArray();
            foreach (var face in frame.Faces)
            {
                Line(writer, () =>
                {
                    writer.WritePropertyName(ShipDesignFormat.ElementIdField);
                    writer.WriteValue(face.Id);
                    writer.WritePropertyName(ShipDesignFormat.FaceNodesField);
                    writer.WriteStartArray();
                    foreach (int node in face.Nodes) writer.WriteValue(node);
                    writer.WriteEndArray();
                    writer.WritePropertyName(ShipDesignFormat.FaceFillField);
                    writer.WriteValue(FillName(face.Fill));
                });
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>Reads a frame section; null, or why it cannot be read.</summary>
        public static string? TryRead(JObject source, ShipDesignLimits limits, out ShipFrame frame)
        {
            frame = ShipFrame.Empty;
            try
            {
                var stories = Array(source, ShipDesignFormat.FrameStoriesField);
                var nodes = Array(source, ShipDesignFormat.FrameNodesField);
                var edges = Array(source, ShipDesignFormat.FrameEdgesField);
                var faces = Array(source, ShipDesignFormat.FrameFacesField);

                if (stories.Count > limits.MaxStories) return $"The frame has {stories.Count} stories; at most {limits.MaxStories}.";
                int elements = nodes.Count + edges.Count + faces.Count;
                if (elements > limits.MaxFrameElements) return $"The frame has {elements} nodes, edges and faces; at most {limits.MaxFrameElements}.";

                int @base = source.ContainsKey(ShipDesignFormat.FrameBaseField) ? ToInt(source[ShipDesignFormat.FrameBaseField]!, "the frame's base") : 0;
                frame = new ShipFrame(@base,
                    stories.Select((t, i) => ToInt(t, $"story {i}")).ToList(),
                    nodes.Select((t, i) => ReadNode(Object(t, $"node {i}"), i)).ToList(),
                    edges.Select((t, i) => ReadEdge(Object(t, $"edge {i}"), i)).ToList(),
                    faces.Select((t, i) => ReadFace(Object(t, $"face {i}"), i, limits)).ToList());
                return null;
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException)
            {
                return $"The frame is not readable: {e.Message}";
            }
        }

        public static string FillName(ShipFrameFill fill) => fill switch
        {
            ShipFrameFill.Planks => ShipDesignFormat.FillPlanks,
            _ => ShipDesignFormat.FillOpen,
        };

        private static ShipFrameNode ReadNode(JObject node, int index)
        {
            string where = $"node {index}";
            if (node[ShipDesignFormat.NodePositionField] is not JArray pos || pos.Count != 3)
            {
                throw new FormatException($"{where}: \"{ShipDesignFormat.NodePositionField}\" must be three whole numbers.");
            }

            return new ShipFrameNode(Int(node, ShipDesignFormat.ElementIdField, where), Int(node, ShipDesignFormat.NodeStoryField, where),
                ToInt(pos[0], where), ToInt(pos[1], where), ToInt(pos[2], where));
        }

        private static ShipFrameEdge ReadEdge(JObject edge, int index)
        {
            string where = $"edge {index}";
            bool railing = edge[ShipDesignFormat.EdgeRailingField] switch
            {
                null => false,
                { Type: JTokenType.Boolean } token => token.Value<bool>(),
                _ => throw new FormatException($"{where}: \"{ShipDesignFormat.EdgeRailingField}\" is not true or false."),
            };
            return new ShipFrameEdge(Int(edge, ShipDesignFormat.ElementIdField, where), Int(edge, ShipDesignFormat.EdgeAField, where),
                Int(edge, ShipDesignFormat.EdgeBField, where), railing);
        }

        private static ShipFrameFace ReadFace(JObject face, int index, ShipDesignLimits limits)
        {
            string where = $"face {index}";
            if (face[ShipDesignFormat.FaceNodesField] is not JArray loop) throw new FormatException($"{where} has no \"{ShipDesignFormat.FaceNodesField}\" list.");
            if (loop.Count > limits.MaxFaceNodes) throw new FormatException($"{where} has {loop.Count} corners; at most {limits.MaxFaceNodes}.");

            var fill = face[ShipDesignFormat.FaceFillField] switch
            {
                null => ShipFrameFill.Open,
                { Type: JTokenType.String } token when token.Value<string>() == ShipDesignFormat.FillPlanks => ShipFrameFill.Planks,
                { Type: JTokenType.String } token when token.Value<string>() == ShipDesignFormat.FillOpen => ShipFrameFill.Open,
                var token => throw new FormatException($"{where}: there is no fill {token.ToString(Formatting.None)}."),
            };
            return new ShipFrameFace(Int(face, ShipDesignFormat.ElementIdField, where), loop.Select(t => ToInt(t, where)).ToList(), fill);
        }

        private static void Inline(JsonWriter writer, Action write)
        {
            var formatting = writer.Formatting;
            writer.Formatting = Formatting.None;
            write();
            writer.Formatting = formatting;
        }

        /// <summary>One object on one line: compact inside, indented between elements (as parts are).</summary>
        private static void Line(JsonWriter writer, Action writeFields)
        {
            writer.WriteStartObject();
            writer.Formatting = Formatting.None;
            writeFields();
            writer.WriteEndObject();
            writer.Formatting = Formatting.Indented;
        }

        private static IReadOnlyList<JToken> Array(JObject source, string field) => source[field] switch
        {
            null => System.Array.Empty<JToken>(),
            JArray array => array.ToList(),
            _ => throw new FormatException($"the frame's \"{field}\" is not a list."),
        };

        private static JObject Object(JToken token, string where) => token as JObject ?? throw new FormatException($"{where} is not an object.");

        private static int Int(JObject source, string field, string where) =>
            source[field] is { } token ? ToInt(token, $"{where}, \"{field}\"") : throw new FormatException($"{where} has no \"{field}\".");

        private static int ToInt(JToken token, string where) =>
            token.Type == JTokenType.Integer ? checked((int)token.Value<long>()) : throw new FormatException($"{where} is not a whole number.");
    }
}
