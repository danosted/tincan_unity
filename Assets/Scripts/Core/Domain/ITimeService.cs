namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: Interface for accessing time data.
    /// Allows decoupling game logic from Unity's static Time class and Netcode's ServerTime.
    /// </summary>
    public interface ITimeService
    {
        float Time { get; }
        float DeltaTime { get; }
        float FixedDeltaTime { get; }

        /// <summary>
        /// The simulation tick being simulated, or the last one simulated when read outside a tick. Each peer counts
        /// its own ticks, so compare two ticks from the same peer (a start and now), never ticks across peers.
        /// </summary>
        int Tick { get; }

        /// <summary>Simulation ticks per second.</summary>
        int TickRate { get; }
    }
}
