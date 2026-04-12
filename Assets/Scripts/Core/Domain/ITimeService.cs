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
    }
}
