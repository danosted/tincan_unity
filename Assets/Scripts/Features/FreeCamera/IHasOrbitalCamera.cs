using TinCan.Core.Domain;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Composite interface indicating an actor has an orbital camera that should receive mouse input.
    /// </summary>
    public interface IHasOrbitalCamera : IActor
    {
        IOrbitalLookView Look { get; }
    }
}
