#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Presentation, every peer: a tall pillar of light at the voyage's destination, so the pilot has something to steer
    /// for. Shown during the briefing and underway, hidden otherwise. Purely local: built from the replicated destination.
    /// </summary>
    public sealed class VoyageBeaconPresenter : ITickable, IDisposable
    {
        private readonly IActorRegistry _actors;
        private readonly VoyageConfig _config;
        private GameObject? _beacon;

        public VoyageBeaconPresenter(IActorRegistry actors, VoyageConfig config)
        {
            _actors = actors;
            _config = config;
        }

        public void Tick()
        {
            var state = _actors.GetActors<IVoyageState>().FirstOrDefault();
            bool show = state != null && state.Phase is VoyagePhase.Briefing or VoyagePhase.Underway;
            if (!show)
            {
                if (_beacon != null) _beacon.SetActive(false);
                return;
            }

            var beacon = _beacon != null ? _beacon : _beacon = Create();
            beacon.transform.position = state!.Destination;
            beacon.SetActive(true);
        }

        public void Dispose()
        {
            if (_beacon != null) Object.Destroy(_beacon);
            _beacon = null;
        }

        private GameObject Create()
        {
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "VoyageBeacon";
            Object.Destroy(beacon.GetComponent<Collider>()); // seen, never touched
            // A unit cylinder is 2 m tall: half the height stands above the destination's altitude, half below.
            beacon.transform.localScale = new Vector3(_config.BeaconWidth, _config.BeaconHeight / 2f, _config.BeaconWidth);
            if (_config.BeaconMaterial != null) beacon.GetComponent<Renderer>().sharedMaterial = _config.BeaconMaterial;
            return beacon;
        }
    }
}
