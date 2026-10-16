#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Builds one design under one root and keeps it in step: each apply removes the parts that went or changed and builds
    /// the new ones, keyed by instance id, and rebuilds the frame meshes of the stories whose frame data changed
    /// (<see cref="ShipFrameMeshProcessor"/>: planks and beams per story). Used for ships in play
    /// (<see cref="ShipAssemblyUseCase"/>) and for the shipyard's preview, so what is built is what flies. A part builds
    /// its visual; with <c>buildFunctionalParts</c> a part without one builds its networked prefab instead (the preview's
    /// helm; the builder decides what to strip). Unknown parts are kept as placeholders that build nothing.
    /// </summary>
    public sealed class ShipHullAssembler
    {
        private sealed class BuiltStory
        {
            public BuiltStory(ulong fingerprint, object? planks, object? beams)
            {
                Fingerprint = fingerprint;
                Planks = planks;
                Beams = beams;
            }

            public ulong Fingerprint { get; }
            public object? Planks { get; }
            public object? Beams { get; }
        }

        private readonly Dictionary<int, (ShipPartPlacement Placement, object? Handle)> _parts = new();
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

        /// <summary>Frame meshes standing (a story's planks and its beams count one each).</summary>
        public int BuiltFrameMeshes => _stories.Values.Sum(s => (s.Planks != null ? 1 : 0) + (s.Beams != null ? 1 : 0));

        public int UnknownCount { get; private set; }

        /// <summary>The parts with something built for them, and the builder's handle for each (a GameObject for real builders).</summary>
        public IEnumerable<(ShipPartPlacement Placement, object Handle)> Built =>
            _parts.Values.Where(p => p.Handle != null).Select(p => (p.Placement, p.Handle!));

        /// <summary>Brings the root in step with the design; returns the known parts added by this apply.</summary>
        public IReadOnlyList<(ShipPartPlacement Placement, ShipPartDefinition Part)> Apply(ShipDesign design, Transform root)
        {
            var wanted = design.Parts.ToDictionary(p => p.InstanceId);
            foreach (var instanceId in _parts.Keys.ToList())
            {
                var (placement, handle) = _parts[instanceId];
                if (wanted.TryGetValue(instanceId, out var now) && Same(placement, now)) continue;

                if (handle != null) _builder.Remove(handle);
                _parts.Remove(instanceId);
            }

            var added = new List<(ShipPartPlacement, ShipPartDefinition)>();
            foreach (var placement in design.Parts)
            {
                if (_parts.ContainsKey(placement.InstanceId)) continue;
                if (!_catalog.TryGet(placement.PartId, out var part))
                {
                    _parts.Add(placement.InstanceId, (placement, null));
                    continue;
                }

                var prefab = part.Visual != null ? part.Visual : _buildFunctionalParts ? part.NetworkedPrefab : null;
                object? handle = prefab != null
                    ? _builder.Build(root, prefab, ShipPartPose.LocalPosition(placement, part), ShipPartPose.LocalRotation(placement))
                    : null;
                _parts.Add(placement.InstanceId, (placement, handle));
                added.Add((placement, part));
            }

            UnknownCount = design.Parts.Count(p => !_catalog.TryGet(p.PartId, out _));

            var shape = ShipFrameShape.From(design.Frame);
            ApplyFrame(shape, root);
            if (TryGetBounds(design, _catalog, shape, _frameTuning, out var bounds)) _builder.FitShipVolume(root, bounds);
            return added;
        }

        public void Clear()
        {
            foreach (var (_, handle) in _parts.Values)
            {
                if (handle != null) _builder.Remove(handle);
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

                object? planks = story.Planks.IsEmpty ? null : _builder.BuildMesh(root, $"Planks{story.Story}", story.Planks, _planks);
                object? beams = story.Beams.IsEmpty ? null : _builder.BuildMesh(root, $"Beams{story.Story}", story.Beams, _beams);
                _stories[story.Story] = new BuiltStory(story.Fingerprint, planks, beams);
            }
        }

        private void RemoveStory(BuiltStory story)
        {
            if (story.Planks != null) _builder.Remove(story.Planks);
            if (story.Beams != null) _builder.Remove(story.Beams);
        }

        /// <summary>The ship-local space a design's known parts and frame fill, or false for a design with neither.</summary>
        public static bool TryGetBounds(ShipDesign design, IShipPartCatalog catalog, ShipFrameTuning tuning, out Bounds bounds) =>
            TryGetBounds(design, catalog, ShipFrameShape.From(design.Frame), tuning, out bounds);

        /// <summary>The space the parts and the frame fill (the frame down to the underside of its planks).</summary>
        private static bool TryGetBounds(ShipDesign design, IShipPartCatalog catalog, ShipFrameShape shape, ShipFrameTuning tuning, out Bounds bounds)
        {
            bool hasParts = ShipPartPose.TryGetBounds(design, catalog, out bounds);
            if (!shape.TryGetBounds(out var frame)) return hasParts;

            frame.Encapsulate(frame.min + Vector3.down * (tuning.PlankThickness + tuning.BeamWidth));
            if (!hasParts) bounds = frame;
            else bounds.Encapsulate(frame);
            return true;
        }

        private static bool Same(ShipPartPlacement a, ShipPartPlacement b) =>
            a.PartId == b.PartId && a.Cell == b.Cell && a.Orientation == b.Orientation;
    }
}
