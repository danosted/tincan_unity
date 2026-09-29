#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.HumanoidMovement;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Stations
{
    /// <summary>
    /// Presentation, local player only, every frame. While the local player occupies a station that has a
    /// <see cref="IStation.ViewCamera"/>, they look through it: its camera and ears switch on, and the body's orbital
    /// camera and ears switch off. Leaving swaps back. Only the view changes: the body keeps reading input, so aiming
    /// stays the player's own predicted look. Occupancy is read from the station's replicated occupant, so this works
    /// on clients, where occupancy itself is not known.
    /// </summary>
    public sealed class StationViewPresenter : ITickable, ILocalViewOverride
    {
        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private IStation? _active;
        private Camera? _bodyCamera;

        public StationViewPresenter(IActorRegistry actors, INetworkService network)
        {
            _actors = actors;
            _network = network;
        }

        public Camera? Camera => _active?.ViewCamera;

        public void Tick()
        {
            var local = _network.IsClient ? _actors.GetLocalPlayerActor<IHumanoidCharacterView>() : null;
            var station = local != null ? OccupiedByLocalPlayer() : null;
            if (ReferenceEquals(station, _active)) return;

            Release();
            if (station != null && local != null) Take(station, local);
        }

        private IStation? OccupiedByLocalPlayer()
        {
            foreach (var actor in _actors.AllActors)
            {
                if (actor is IStation { ViewCamera: not null } station && station.OccupantClientId == _network.LocalClientId) return station;
            }
            return null;
        }

        private void Take(IStation station, IHumanoidCharacterView local)
        {
            _active = station;
            SetView(station.ViewCamera, true);

            _bodyCamera = local.Look?.Camera;
            SetView(_bodyCamera, false);
        }

        private void Release()
        {
            if (_active != null) SetView(_active.ViewCamera, false);
            SetView(_bodyCamera, true);
            _active = null;
            _bodyCamera = null;
        }

        // A camera and the listener on it go together, so exactly one pair is active.
        private static void SetView(Camera? camera, bool on)
        {
            if (camera == null) return;
            camera.enabled = on;
            if (camera.TryGetComponent(out AudioListener listener)) listener.enabled = on;
        }
    }
}
