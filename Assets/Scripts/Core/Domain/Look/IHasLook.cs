using TinCan.Core.Domain;

namespace TinCan.Core.Domain.Look
{
    /// <summary>
    /// Composite interface indicating an actor has a look (and a camera) that should receive mouse input.
    /// </summary>
    public interface IHasLook : IActor
    {
        ILookView Look { get; }
    }
}
