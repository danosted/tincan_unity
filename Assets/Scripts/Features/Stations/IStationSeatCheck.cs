#nullable enable
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.Stations
{
    /// <summary>
    /// Whether occupying a station may put the player on its seat. A station mounted where its seat hangs past a rail
    /// (a cannon at the stern) must not teleport the player overboard; the player then occupies it where they stand.
    /// </summary>
    public interface IStationSeatCheck
    {
        /// <summary>True when there is a floor just under <paramref name="seat"/> and room there for the player's body.</summary>
        bool CanSeat(IHumanoidCharacterView player, IStation station, Transform seat);
    }
}
