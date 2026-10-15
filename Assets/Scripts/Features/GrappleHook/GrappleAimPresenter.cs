#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Presentation, the local player only, per frame: while they hold the hook and are not hooked, a marker shows where
    /// a press would go. It is green on a surface within <see cref="GrappleHookConfig.MaxRange"/> and red on one beyond
    /// it, up to <see cref="GrappleHookConfig.MarkerSightFactor"/> times further, and hidden when nothing is in sight. It
    /// runs the hook's own query from the eye along the live look, so it agrees with what the next press catches.
    /// </summary>
    public sealed class GrappleAimPresenter : ITickable, IDisposable
    {
        private readonly GrappleHookConfig _config;
        private readonly IGrappleHookQuery _query;
        private readonly IGrappleHooks _hooks;
        private readonly IActorRegistry _actors;
        private readonly bool _drawsVisuals = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        private MeshRenderer? _marker;

        public GrappleAimPresenter(GrappleHookConfig config, IGrappleHookQuery query, IGrappleHooks hooks, IActorRegistry actors)
        {
            _config = config;
            _query = query;
            _hooks = hooks;
            _actors = actors;
        }

        /// <summary>What the local player aims at this frame (tests and scenarios).</summary>
        public GrappleAimReach Reach { get; private set; }

        /// <summary>Where the marker sits this frame, while <see cref="Reach"/> is not None.</summary>
        public Vector3 MarkerPoint { get; private set; }

        public void Tick()
        {
            Reach = Aim(out var point, out var eye);
            MarkerPoint = point;
            if (_drawsVisuals) Show(Reach, point, eye);
        }

        public void Dispose()
        {
            if (_marker == null) return;
            if (Application.isPlaying) Object.Destroy(_marker.gameObject);
            else Object.DestroyImmediate(_marker.gameObject);
        }

        private GrappleAimReach Aim(out Vector3 point, out Vector3 eye)
        {
            point = default;
            eye = default;
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            var movement = local?.Movement;
            if (local == null || movement == null || _config.CarryingTag == null) return GrappleAimReach.None;
            if (!local.HasTag(_config.CarryingTag) || _hooks.TryGetHookPoint(local.Id, out _)) return GrappleAimReach.None;

            var body = movement.Transform;
            eye = body.position + body.up * movement.EyeHeight;
            var facing = Quaternion.Euler(0f, movement.LookRotation.eulerAngles.y, 0f);
            Vector3 direction = GrappleHookProcessor.AimDirection(facing, movement.LookPitch);
            float sight = _config.MaxRange * _config.MarkerSightFactor;

            if (!_query.TryCatch(eye, direction, sight, _config.HookableLayers, body, out _, out point)) return GrappleAimReach.None;
            return GrappleHookProcessor.Reach(Vector3.Distance(eye, point), _config.MaxRange);
        }

        private void Show(GrappleAimReach reach, Vector3 point, Vector3 eye)
        {
            if (reach == GrappleAimReach.None)
            {
                if (_marker != null) _marker.gameObject.SetActive(false);
                return;
            }

            var marker = _marker != null ? _marker : _marker = CreateMarker();
            marker.gameObject.SetActive(true);
            marker.transform.position = point;
            marker.transform.localScale = Vector3.one * (Vector3.Distance(eye, point) * _config.MarkerScreenSize);
            var material = reach == GrappleAimReach.InRange ? _config.InRangeMaterial : _config.OutOfRangeMaterial;
            if (material != null) marker.sharedMaterial = material;
        }

        private static MeshRenderer CreateMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "GrappleAimMarker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
