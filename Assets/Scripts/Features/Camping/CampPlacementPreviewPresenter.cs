#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Presentation, the local player only, per frame: while they carry a campfire bundle or a tent roll, a ghost of it
    /// stands where a press would set it down, in the fits material where it fits and the other where it does not
    /// (steep, on the ship, no room), hidden when no ground is in reach. It asks the server's own question
    /// (<see cref="CampPlacementFinder"/>) along the live look, so its colour is the answer the press gets.
    /// </summary>
    public sealed class CampPlacementPreviewPresenter : ITickable, IDisposable
    {
        private readonly IActorRegistry _actors;
        private readonly CampPlacementFinder _finder;
        private readonly CampingConfig _config;
        private readonly bool _drawsVisuals = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        private readonly Dictionary<CampPlaceable, (GameObject Ghost, Renderer[] Renderers)> _ghosts = new();

        public CampPlacementPreviewPresenter(IActorRegistry actors, CampPlacementFinder finder, CampingConfig config)
        {
            _actors = actors;
            _finder = finder;
            _config = config;
        }

        /// <summary>Where the local player would place what they carry this frame, or null when they carry nothing to place.</summary>
        public CampPlacement? Current { get; private set; }

        public void Tick()
        {
            Current = Aim();
            if (_drawsVisuals) Show(Current);
        }

        public void Dispose()
        {
            foreach (var (ghost, _) in _ghosts.Values)
            {
                if (ghost == null) continue;
                if (Application.isPlaying) Object.Destroy(ghost);
                else Object.DestroyImmediate(ghost);
            }
            _ghosts.Clear();
        }

        private CampPlacement? Aim()
        {
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            var movement = local?.Movement;
            if (local == null || movement == null) return null;

            bool bundle = _config.CarryingBundleTag != null && local.HasTag(_config.CarryingBundleTag);
            bool tent = _config.CarryingTentTag != null && local.HasTag(_config.CarryingTentTag);
            if (CampPlacementProcessor.Placing(bundle, tent) is not { } kind) return null;

            var body = movement.Transform;
            var eye = body.position + body.up * movement.EyeHeight;
            var facing = Quaternion.Euler(0f, movement.LookRotation.eulerAngles.y, 0f);
            return _finder.Find(kind, eye, CampPlacementProcessor.AimDirection(facing, movement.LookPitch), body);
        }

        private void Show(CampPlacement? placement)
        {
            foreach (var pair in _ghosts)
            {
                bool shown = placement is { HasGround: true } current && current.Kind == pair.Key;
                if (pair.Value.Ghost != null && pair.Value.Ghost.activeSelf != shown) pair.Value.Ghost.SetActive(shown);
            }
            if (placement is not { HasGround: true } at) return;

            var (ghost, renderers) = Ghost(at.Kind);
            if (ghost == null) return;
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(at.Point, at.Rotation);
            var material = at.Fits ? _config.FitsMaterial : _config.NoFitMaterial;
            if (material == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
            }
        }

        private (GameObject? Ghost, Renderer[] Renderers) Ghost(CampPlaceable kind)
        {
            if (_ghosts.TryGetValue(kind, out var made) && made.Ghost != null) return made;

            var prefab = kind == CampPlaceable.Tent ? _config.TentPreview : _config.CampfirePreview;
            if (prefab == null) return (null, Array.Empty<Renderer>());

            var ghost = Object.Instantiate(prefab);
            ghost.name = $"{kind}Preview";
            foreach (var collider in ghost.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            var renderers = ghost.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            _ghosts[kind] = (ghost, renderers);
            return (ghost, renderers);
        }
    }
}
