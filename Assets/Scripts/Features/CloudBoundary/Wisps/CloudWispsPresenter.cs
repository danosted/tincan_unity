#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Atmosphere;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Every peer with a view: the wisps streaming past the local camera (plan .docs/plans/cloud-sea.md, step 3). One
    /// particle system that follows the camera and spawns its wisps in the world, so they stay where they appeared and
    /// stream past as the view moves, stretched along the travel. How many and how visible come from the view's speed
    /// (measured here, frame to frame) and its submersion (<see cref="IViewSubmersion"/>), through
    /// <see cref="CloudWispsProcessor"/>; a burst on entering the cloud. Presentation only.
    /// </summary>
    public sealed class CloudWispsPresenter : ILateTickable, IDisposable
    {
        /// <summary>A jump farther than this in one frame (a respawn, a teleport) is not speed.</summary>
        private const float MaxJump = 50f;

        private readonly ILocalViewCamera _view;
        private readonly ITimeService _time;
        private readonly CloudWispsConfig _config;
        private readonly CloudWispsProcessor _processor;
        private readonly IViewSubmersion? _submersion;
        private readonly Material? _material;
        private ParticleSystem? _particles;
        private Vector3? _lastEye;
        private float _speed;
        private float _lastSubmersion;

        public CloudWispsPresenter(ILocalViewCamera view, ITimeService time, CloudWispsConfig config, CloudWispsProcessor processor,
            IReadOnlyList<IViewSubmersion> submersion, Material? material)
        {
            _view = view;
            _time = time;
            _config = config;
            _processor = processor;
            _submersion = submersion.Count > 0 ? submersion[0] : null;
            _material = material;
        }

        public void LateTick()
        {
            Camera? camera = _view.Camera;
            if (_material == null || camera == null) return;

            _particles ??= Create(_material);
            Vector3 eye = camera.transform.position;
            float delta = _time.DeltaTime;
            if (_lastEye is { } last && delta > 0f && Vector3.Distance(eye, last) < MaxJump)
            {
                // Smoothed, so a frame's hitch does not flicker the wisps.
                _speed = Mathf.Lerp(_speed, Vector3.Distance(eye, last) / delta, 0.2f);
            }

            _lastEye = eye;
            float submersion = _submersion?.Submersion ?? 0f;
            CloudWispsFrame frame = _processor.Evaluate(_speed, submersion, _config);

            _particles.transform.position = eye;
            var emission = _particles.emission;
            emission.rateOverTime = frame.Rate;
            var main = _particles.main;
            main.startColor = new Color(1f, 1f, 1f, frame.Opacity);
            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.cameraVelocityScale = _config.Stretch;

            if (_processor.Entered(_lastSubmersion, submersion)) _particles.Emit(_config.Burst);
            _lastSubmersion = submersion;
        }

        public void Dispose()
        {
            if (_particles != null) Object.Destroy(_particles.gameObject);
        }

        private ParticleSystem Create(Material material)
        {
            var root = new GameObject("Cloud Wisps");
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = _config.Lifetime;
            main.startSpeed = 0.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(_config.Size.x, _config.Size.y);
            main.maxParticles = 1500;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _config.SpawnRadius;
            shape.radiusThickness = 0.5f; // the outer half: never right in the camera's face

            var emission = particles.emission;
            emission.rateOverTime = 0f;

            // Each wisp fades in and out over its life.
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.5f;
            renderer.velocityScale = 0f;
            renderer.cameraVelocityScale = _config.Stretch;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
            return particles;
        }
    }
}
