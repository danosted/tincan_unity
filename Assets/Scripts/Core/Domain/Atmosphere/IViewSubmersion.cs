#nullable enable
namespace TinCan.Core.Domain.Atmosphere
{
    /// <summary>
    /// How deep the local view (the camera the player looks through) is inside the cloud deck, from the feature that
    /// knows where the clouds are (CloudBoundary). Presentation only, never replicated; all zero when the view is clear
    /// or there is no view yet. The atmosphere (<see cref="IAtmosphereOwner"/>) turns it into whiteout fog and dimmed
    /// light. Plan: .docs/plans/look-slice.md (L2).
    /// </summary>
    public interface IViewSubmersion
    {
        /// <summary>0 above the deck, 1 fully inside it.</summary>
        float Submersion { get; }

        /// <summary>How white the view goes, 0..1 (rises faster than <see cref="Submersion"/> near full).</summary>
        float Whiteout { get; }

        /// <summary>What the sun's intensity is multiplied by in the cloud, 1 in clear air.</summary>
        float LightMultiplier { get; }
    }
}
