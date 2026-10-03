#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Stations;
using UnityEngine;

namespace TinCan.Features.Helm
{
    /// <summary>
    /// Application Layer, server: the helm as the ship's pilot (<see cref="IAirshipPilotInput"/>). A ship whose helm is
    /// occupied takes the helmsman's station axes from the input the server last simulated for them; the airship ticks
    /// before the humanoid, so that is one tick old. An unmanned helm gives no input and the ship coasts down.
    /// </summary>
    public sealed class HelmSteeringUseCase : IAirshipPilotInput
    {
        private readonly IActorRegistry _actors;
        private readonly IStationOccupancy _occupancy;

        public HelmSteeringUseCase(IActorRegistry actors, IStationOccupancy occupancy)
        {
            _actors = actors;
            _occupancy = occupancy;
        }

        public bool TryGetInput(IAirshipView ship, out AirshipInputState input)
        {
            foreach (var helm in _actors.GetActors<IHelm>())
            {
                if (helm is not IStation station || !ReferenceEquals(helm.Ship, ship)) continue;
                if (_occupancy.OccupantOf(station) is not { } helmsman) continue;

                input = Steer(helmsman.InputState.StationAxes);
                return true;
            }

            input = default;
            return false;
        }

        /// <summary>Station axes (x throttle, y yaw, z pitch) to ship input, each clamped to -1..1.</summary>
        public static AirshipInputState Steer(Vector3 axes) => new()
        {
            Throttle = Mathf.Clamp(axes.x, -1f, 1f),
            Yaw = Mathf.Clamp(axes.y, -1f, 1f),
            Pitch = Mathf.Clamp(axes.z, -1f, 1f)
        };
    }
}
