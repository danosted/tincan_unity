#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Encounters;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Every peer with a view: the storm's depth and strength. From the lab's sliders, or (with Follow Zone) from the
    /// encounter zone the camera is in (<see cref="IEncounterZones"/>): its depth, and a strength that calms over
    /// <see cref="CalmSeconds"/> while the zone rests or is gone and builds again when it returns.
    /// </summary>
    public sealed class StormIntensity : IStormIntensity, ITickable
    {
        public const float CalmSeconds = 20f;

        private readonly StormLabSettings _lab;
        private readonly IEncounterZones? _zones;
        private readonly ILocalViewCamera _view;

        public StormIntensity(StormLabSettings lab, IEncounterZones? zones, ILocalViewCamera view)
        {
            _lab = lab;
            _zones = zones;
            _view = view;
        }

        [Inject]
        public StormIntensity(StormLabSettings lab, ILocalViewCamera view, IObjectResolver resolver)
            : this(lab, resolver.TryResolve<IEncounterZones>(out var zones) ? zones : null, view)
        {
        }

        public float Depth { get; private set; }
        public float Strength { get; private set; } = 1f;

        public void Tick() => Step(_view.Camera != null ? _view.Camera.transform.position : (Vector3?)null, Time.deltaTime);

        /// <summary>One frame with the camera at <paramref name="camera"/> (null: no view yet).</summary>
        public void Step(Vector3? camera, float deltaTime)
        {
            if (!_lab.FollowZone || _zones == null)
            {
                Depth = _lab.LabDepth;
                Strength = _lab.LabStrength;
                return;
            }

            var at = camera is { } position ? _zones.ZoneAt(position) : null;
            Depth = at?.Depth ?? 0f;
            bool spent = at?.Zone.State is EncounterZoneState.Resting or EncounterZoneState.Gone;
            Strength = Mathf.MoveTowards(Strength, spent ? 0f : 1f, deltaTime / CalmSeconds);
        }
    }
}
