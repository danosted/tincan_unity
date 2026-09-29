using TinCan.Core.Domain;

namespace TinCan.Core.Domain.Look
{
    /// <summary>
    /// Composite interface indicating an actor has an orbital camera that should receive mouse input.
    /// </summary>
    public interface IHasOrbitalCamera : IActor
    {
        IOrbitalLookView Look { get; }
    }
}
