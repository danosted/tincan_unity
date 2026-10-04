#nullable enable
namespace TinCan.Core.Ship
{
    /// <summary>
    /// Sets an airship's base performance on the server: its top speed, turn rate and maximum health (health is filled to
    /// it). Implemented by the airship itself; a feature that knows what the ship is built from (ship designs) calls it,
    /// so the core never knows about designs. The values replicate as the ship's attributes.
    /// </summary>
    public interface IAirshipTuning
    {
        void ServerSetBaseStats(float maxForwardSpeed, float turnSpeed, float maxHealth);
    }
}
