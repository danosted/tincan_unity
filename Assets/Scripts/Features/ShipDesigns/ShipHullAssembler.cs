#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Builds one design under one root and keeps it in step: each apply removes the parts that went or moved (a part
    /// moves with its frame element) and builds the new ones, keyed by instance id, and rebuilds the frame meshes of the
    /// stories whose frame data changed (<see cref="ShipFrameMeshProcessor"/>: planks, beams, stair steps and ramps per
    /// story). Used for ships in play (<see cref="ShipAssemblyUseCase"/>) and for the shipyard's preview, so what is built
    /// is what flies. A part builds its visual; with <c>buildFunctionalParts</c> a part without one builds its networked
    /// prefab instead (the preview's helm; the builder decides what to strip). Unknown parts, and parts their anchor does
    /// not hold, are kept as placeholders that build nothing.
    /// </summary>
    public sealed class ShipHullAssembler
    {
        private sealed class BuiltStory
        {
            public BuiltStory(ulong fingerprint, IReadOnlyList<object> meshes)
            {
                Fingerprint = fingerprint;
                Meshes = meshes;
            }

            public ulong Fingerprint { get; }
            public IReadOnlyList<object> Meshes { get; }
        }

        private readonly struct BuiltPart
        {
            public BuiltPart(ShipPartPlacement placement, Vector3 position, Quaternion rotation, object? handle)
            {
                Placement = placement;
                Position = position;
                Rotation = rotation;
                Handle = handle;
            }

            public ShipPartPlacement Placement { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public object? Handle { get; }
        }

        private readonly Dictionary<int, BuiltPart> _parts = new();
        private readonly Dictionary<int, BuiltStory> _stories = new();
        private readonly ShipFrameMeshProcessor _meshes = new();
        private readonly IShipPartCatalog _catalog;
        private readonly IShipHullBuilder _builder;
        private readonly bool _buildFunctionalParts;
        private readonly ShipFrameTuning _frameTuning;
        private readonly Material? _planks;
        private readonly Material? _beams;

        public ShipHullAssembler(IShipPartCatalog catalog, IShipHullBuilder builder, bool buildFunctionalParts, ShipDesignsConfig? designs = null)
        {
            _catalog = catalog;
            _builder = builder;
            _buildFunctionalParts = buildFunctionalParts;
            _frameTuning = designs != null ? designs.Frames : ShipFrameTuning.Default;
            _planks = designs != null ? designs.FramePlanks : null;
            _beams = designs != null ? designs.FrameBeams : null;
        }

        /// <summary>Parts with something built for them.</summary>
        public int BuiltCount => _parts.Values.Count(p => p.Handle != null);

        /// <summary>Frame meshes standing (each story's planks, beams, steps and ramps count one each when it has them).</summary>
        public int BuiltFrameMeshes => _stories.Values.Sum(s => s.Meshes.Count);

        public int UnknownCount { get; private set; }

        /// <summary>The parts with something built for them, and the builder's handle for each (a GameObject for real builders).</summary>
        public IEnumerable<(ShipPartPlacement Placement, object Handle)> Built =>
            _parts.Values.Where(p => p.Handle != null).Select(p => (p.Placement, p.Handle!));

        /// <summary>Brings the root in step with the design; returns the known, anchored parts added by this apply.</summary>
        public IReadOnlyList<(ShipPartPlacement Placement, ShipPartDefinition Part)> Apply(ShipDesign design, Transform root)
        {
            var shape = ShipFrameShape.From(design.Frame);
            var poses = new Dictionary<int, (ShipPartDefinition Part, Vector3 Position, Quaternion Rotation)>();
            foreach (var placement in design.Parts)
            {
                if (_catalog.TryGet(placement.PartId, out var part) && ShipPartPose.TryGetPose(placement, part, shape, out var position, out var rotation))
                {
                    poses[placement.InstanceId] = (part, position, rotation);
                }
            }

            var wanted = design.Parts.ToDictionary(p => p.InstanceId);
            foreach (var instanceId in _parts.Keys.ToList())
            {
                var built = _parts[instanceId];
                if (wanted.TryGetValue(instanceId, out var now) && now.PartId == built.Placement.PartId && poses.TryGetValue(instanceId, out var pose)
                    && pose.Position == built.Position && pose.Rotation == built.Rotation)
                {
                    _parts[instanceId] = new BuiltPart(now, built.Position, built.Rotation, built.Handle);
                    continue;
                }

                if (built.Handle != null) _builder.Remove(built.Handle);
                _parts.Remove(instanceId);
            }

            var added = new List<(ShipPartPlacement, ShipPartDefinition)>();
            foreach (var placement in design.Parts)
            {
                if (_parts.ContainsKey(placement.InstanceId)) continue;
                if (!poses.TryGetValue(placement.InstanceId, out var pose))
                {
                    _parts.Add(placement.InstanceId, new BuiltPart(placement, default, Quaternion.identity, null));
                    continue;
                }

                var prefab = pose.Part.Visual != null ? pose.Part.Visual : _buildFunctionalParts ? pose.Part.NetworkedPrefab : null;
                object? handle = prefab != null ? _builder.Build(root, prefab, pose.Position, pose.Rotation) : null;
                _parts.Add(placement.InstanceId, new BuiltPart(placement, pose.Position, pose.Rotation, handle));
                added.Add((placement, pose.Part));
            }

            UnknownCount = design.Parts.Count(p => !_catalog.TryGet(p.PartId, out _));

            ApplyFrame(shape, root);
            if (TryGetBounds(design, _catalog, shape, _frameTuning, out var bounds)) _builder.FitShipVolume(root, bounds);
            return added;
        }

        public void Clear()
        {
            foreach (var part in _parts.Values)
            {
                if (part.Handle != null) _builder.Remove(part.Handle);
            }

            _parts.Clear();
            foreach (var story in _stories.Values) RemoveStory(story);
            _stories.Clear();
            UnknownCount = 0;
        }

        /// <summary>Rebuilds the stories whose fingerprint changed and removes the stories the frame no longer has.</summary>
        private void ApplyFrame(ShipFrameShape shape, Transform root)
        {
            int storyCount = shape.Frame.StoryHeights.Count;
            foreach (int gone in _stories.Keys.Where(s => s >= storyCount).ToList())
            {
                RemoveStory(_stories[gone]);
                _stories.Remove(gone);
            }

            if (storyCount == 0) return;
            foreach (var story in _meshes.Build(shape, _frameTuning))
            {
                if (_stories.TryGetValue(story.Story, out var built))
                {
                    if (built.Fingerprint == story.Fingerprint) continue;
                    RemoveStory(built);
                }

                var meshes = new List<object>();
                void Add(string name, ShipFrameMesh mesh, Material? material, ShipFrameMeshUse use)
                {
                    if (!mesh.IsEmpty) meshes.Add(_builder.BuildMesh(root, $"{name}{story.Story}", mesh, material, use));
                }

                Add("Planks", story.Planks, _planks, ShipFrameMeshUse.Solid);
                Add("Beams", story.Beams, _beams, ShipFrameMeshUse.Solid);
                Add("Steps", story.Steps, _planks, ShipFrameMeshUse.Seen);
                Add("Ramps", story.Ramps, null, ShipFrameMeshUse.Collider);
                _stories[story.Story] = new BuiltStory(story.Fingerprint, meshes);
            }
        }

        private void RemoveStory(BuiltStory story)
        {
            foreach (var mesh in story.Meshes) _builder.Remove(mesh);
        }

        /// <summary>The ship-local space a design's known parts and frame fill, or false for a design with neither.</summary>
        public static bool TryGetBounds(ShipDesign design, IShipPartCatalog catalog, ShipFrameTuning tuning, out Bounds bounds) =>
            TryGetBounds(design, catalog, ShipFrameShape.From(design.Frame), tuning, out bounds);

        /// <summary>The space the parts and the frame fill (the frame down to the underside of its planks).</summary>
        private static bool TryGetBounds(ShipDesign design, IShipPartCatalog catalog, ShipFrameShape shape, ShipFrameTuning tuning, out Bounds bounds)
        {
            bool hasParts = ShipPartPose.TryGetBounds(design, catalog, shape, out bounds);
            if (!shape.TryGetBounds(out var frame)) return hasParts;

            frame.Encapsulate(frame.min + Vector3.down * (tuning.PlankThickness + tuning.BeamWidth));
            if (!hasParts) bounds = frame;
            else bounds.Encapsulate(frame);
            return true;
        }
    }
}
