#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.FreeCamera;
using UnityEngine;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// The local view camera is the camera of whatever the local player possesses: the orbital camera of the body or the
    /// ship, or the camera on the free camera.
    /// </summary>
    public sealed class PossessedViewCamera : ILocalViewCamera
    {
        private readonly IPossessionState _possession;

        public PossessedViewCamera(IPossessionState possession) => _possession = possession;

        public Camera? Camera
        {
            get
            {
                var current = _possession.CurrentPossession;
                Camera? camera = current is IHasOrbitalCamera orbital
                    ? orbital.Look.Camera
                    : (current as Component)?.GetComponentInChildren<Camera>();
                return camera != null && camera.isActiveAndEnabled ? camera : null;
            }
        }
    }
}
