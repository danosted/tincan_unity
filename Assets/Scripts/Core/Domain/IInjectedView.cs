#nullable enable
namespace TinCan.Core.Domain
{
    /// <summary>
    /// Marker for scene/prefab MonoBehaviours that want VContainer injection at container build (e.g. UI overlays
    /// living on the GameLifetimeScope prefab). The lifetime scope injects every active or inactive object that
    /// implements this, so features never need a line in ProjectLifetimeScope for their views.
    /// </summary>
    public interface IInjectedView
    {
    }
}
