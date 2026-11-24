#nullable enable
namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: a lost ship is brought back (the crew chose Respawn, or a new voyage starts after a loss), and each
    /// feature makes its part whole again: the hull repaired, the tank full, the cloud boundary's countdown cleared. Only
    /// the ship: the world keeps its progress (that is <c>ISessionParticipant.ResetForSession</c>). Register with
    /// <c>.As&lt;IShipRecovery&gt;()</c>. Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public interface IShipRecovery
    {
        void Recover(IAirshipView ship);
    }
}
