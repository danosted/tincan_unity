#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Encounters;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Every peer with a view: the storm's depth, strength and region. From the nearest encounter zone
    /// (<see cref="IEncounterZones"/>): its region, the camera's depth in it, and a strength that follows the heart's
    /// replicated strength (<see cref="IStormHeart"/>: each hit of the cure lowers it) and calms to 0 while the zone rests
    /// or is gone, over <see cref="CalmSeconds"/> a step, and builds again when it returns. In the lab, its
    /// <see cref="StormLabControls"/> drive it instead unless they follow the zone.
    /// </summary>
    public sealed class StormIntensity : IStormIntensity, ITickable
    {
        public const float CalmSeconds = 20f;

        private readonly IEncounterZones? _zones;
        private readonly ILocalViewCamera _view;
        private StormLabControls? _lab;

        public StormIntensity(IEncounterZones? zones, ILocalViewCamera view, StormLabControls? lab = null)
        {
            _zones = zones;
            _view = view;
            _lab = lab;
        }

        [Inject]
        public StormIntensity(ILocalViewCamera view, IObjectResolver resolver)
            : this(resolver.TryResolve<IEncounterZones>(out var zones) ? zones : null, view)
        {
        }

        public float Depth { get; private set; }
        public float Strength { get; private set; } = 1f;
        public (Vector2 Centre, float Radius)? Region { get; private set; }

        /// <summary>The lab scene's controls, handed over when the scope injects them (only Lab_Storm has them).</summary>
        public void UseLab(StormLabControls lab) => _lab = lab;

        public void Tick()
        {
            Step(_view.Camera != null ? _view.Camera.transform.position : (Vector3?)null, Time.deltaTime);
        }

        /// <summary>One frame with the camera at <paramref name="camera"/> (null: no view yet).</summary>
        public void Step(Vector3? camera, float deltaTime)
        {
            if (_lab != null && !_lab.FollowZone)
            {
                Depth = _lab.Depth;
                Strength = _lab.Strength;
                Region = null;
                return;
            }

            var zone = camera is { } position ? _zones?.Nearest(position) : null;
            if (zone == null)
            {
                Depth = 0f;
                Region = null;
                return;
            }

            Depth = EncounterZoneProcessor.Depth(zone.Centre, zone.Radius, camera!.Value);
            Region = (zone.Centre, zone.Radius);
            // The cure's hits lower the heart's replicated strength; a spent zone is calm.
            bool spent = zone.State is EncounterZoneState.Resting or EncounterZoneState.Gone;
            float target = spent ? 0f : StormHearts.Of(zone)?.Strength ?? 1f;
            Strength = Mathf.MoveTowards(Strength, target, deltaTime / CalmSeconds);
        }
    }
}
