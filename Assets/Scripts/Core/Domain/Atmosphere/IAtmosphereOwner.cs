#nullable enable
namespace TinCan.Core.Domain.Atmosphere
{
    /// <summary>
    /// Registered by the one feature that owns the scene's air and light (the Atmosphere feature): the fog, the sun's
    /// colour and intensity, and the ambient light. Anything else that would write them (CloudBoundary's submersion fog,
    /// the cloud tuning rig's sun) leaves them alone in Play while an owner is registered, so there is a single writer.
    /// Scenes without the feature (the test range) keep their plain settings. Plan: .docs/plans/look-slice.md (L2).
    /// </summary>
    public interface IAtmosphereOwner
    {
    }
}
