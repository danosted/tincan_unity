#nullable enable
using TinCan.Core.Domain;
using UnityEngine;
using VContainer;
using TinCan.Core.Domain.Look;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// The local view camera is the camera of whatever the local player possesses: the orbital camera of the body or the
    /// ship, or the camera on the free camera. A view override (the camera of an occupied station) wins while it has one.
    /// </summary>
    public sealed class PossessedViewCamera : ILocalViewCamera
    {
        private readonly IPossessionState _possession;
        private readonly IObjectResolver? _resolver;
        private ILocalViewOverride? _override;
        private bool _overrideResolved;

        public PossessedViewCamera(IPossessionState possession) => _possession = possession;

        // The override comes from a feature installer (Stations) that a profile may leave out, so it is looked up
        // optionally, and on first use rather than at construction.
        [Inject]
        public PossessedViewCamera(IPossessionState possession, IObjectResolver resolver)
        {
            _possession = possession;
            _resolver = resolver;
        }

        public Camera? Camera
        {
            get
            {
                var overridden = Override()?.Camera;
                if (overridden != null && overridden.isActiveAndEnabled) return overridden;

                var current = _possession.CurrentPossession;
                Camera? camera = current is IHasOrbitalCamera orbital
                    ? orbital.Look.Camera
                    : (current as Component)?.GetComponentInChildren<Camera>();
                return camera != null && camera.isActiveAndEnabled ? camera : null;
            }
        }

        private ILocalViewOverride? Override()
        {
            if (_overrideResolved) return _override;
            _overrideResolved = true;
            if (_resolver != null && _resolver.TryResolve<ILocalViewOverride>(out var viewOverride)) _override = viewOverride;
            return _override;
        }
    }
}
