namespace TinCan.Features.Possession
{
    /// <summary>
    /// Downstream interface for components that need to react to possession changes with player context.
    /// Used for chaining possession events from a Facade or Mediator.
    /// </summary>
    public interface IPossessionReceiver
    {
        void OnPossessed(ulong playerId);
        void OnUnpossessed();
    }
}
