#nullable enable
namespace TinCan.Core.Ship
{
    /// <summary>
    /// Sets how an airship flies and its maximum health on the server (health is filled to it). Implemented by the
    /// airship itself; a feature that knows what the ship is built from (ship designs: its core part's
    /// <see cref="ShipHandlingProfile"/>) calls it, so the core never knows about designs. Top speed and turn rate
    /// replicate as the ship's attributes; the rest of the handling is read only where the ship is simulated (the server).
    /// </summary>
    public interface IAirshipTuning
    {
        /// <param name="handling">The profile it flies on from now on (edits to it apply live); null keeps what it flies
        /// on (its prefab's profile).</param>
        void ServerSetHandling(ShipHandlingProfile? handling, float maxHealth);
    }
}
