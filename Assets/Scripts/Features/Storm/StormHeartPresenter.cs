#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Features.Encounters;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Presentation, every peer, every frame: the storm's heart, a glowing knot that pulses and shrinks as the storm
    /// calms (<see cref="IStormIntensity.Strength"/>), and, while the round runs, its target, which the gunners look for in
    /// the dark. Both come from <see cref="StormHeartProcessor"/> on the shared clock, so they stand where the server
    /// checks the beam against. Draws the first storm zone (there is one today). Plan: storm-crossing.md, ST5.
    /// </summary>
    public sealed class StormHeartPresenter : ITickable, IDisposable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IStormIntensity _intensity;
        private readonly StormConfig _config;
        private readonly EncounterZoneConfig _zoneConfig;
        private Transform? _heart;
        private Transform? _target;

        public StormHeartPresenter(IActorRegistry actors, ITimeService time, IStormIntensity intensity, StormConfig config, EncounterZoneConfig zoneConfig)
        {
            _actors = actors;
            _time = time;
            _intensity = intensity;
            _config = config;
            _zoneConfig = zoneConfig;
        }

        /// <summary>Where the live target is drawn on this peer, or null (scenario probe and aim).</summary>
        public Vector3? TargetShown { get; private set; }

        public void Tick()
        {
            IEncounterZone? zone = null;
            IStormHeart? heart = null;
            foreach (var candidate in _actors.GetActors<IEncounterZone>())
            {
                if (!candidate.IsSimulating || StormHearts.Of(candidate) is not { } found) continue;
                zone = candidate;
                heart = found;
                break;
            }

            TargetShown = null;
            if (zone == null || heart == null || zone.State == EncounterZoneState.Gone)
            {
                Hide(_heart);
                Hide(_target);
                return;
            }

            double time = _time.Time;
            int severity = _zoneConfig.Severity;
            float strength = _intensity.Strength;

            var heartView = _heart ??= Glow("StormHeart", _config.HeartColor);
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)(time * _config.HeartPulseRate % 1.0) * 2f * Mathf.PI);
            heartView.position = StormHeartProcessor.Heart(zone.Centre, heart.Altitude, severity, _config.Seed, time, _config);
            heartView.localScale = Vector3.one * (_config.HeartDiameter * (0.85f + 0.15f * pulse) * Mathf.Sqrt(Mathf.Clamp01(strength)));
            heartView.gameObject.SetActive(strength > 0.01f);

            if (zone.State == EncounterZoneState.Running && heart.Strength > 0f &&
                StormHeartProcessor.LiveTarget(zone.Centre, heart.Altitude, severity, _config.Seed, time, heart.HitSlot, _config) is { } target)
            {
                var targetView = _target ??= Glow("StormTarget", _config.TargetColor);
                targetView.position = target.Position;
                targetView.localScale = Vector3.one * (_config.TargetDiameter * (0.9f + 0.1f * pulse));
                targetView.gameObject.SetActive(true);
                TargetShown = target.Position;
            }
            else
            {
                Hide(_target);
            }
        }

        private static void Hide(Transform? view)
        {
            if (view != null) view.gameObject.SetActive(false);
        }

        private Transform Glow(string name, Color color)
        {
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = name;
            Object.Destroy(glow.GetComponent<Collider>()); // drawn only; the server checks the beam against the rules
            var renderer = glow.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_config.GlowMaterial != null) renderer.sharedMaterial = _config.GlowMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
            return glow.transform;
        }

        public void Dispose()
        {
            if (_heart != null) Object.Destroy(_heart.gameObject);
            if (_target != null) Object.Destroy(_target.gameObject);
        }
    }
}
