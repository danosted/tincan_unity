namespace TinCan.Features.Possession
{
    /// <summary>
    /// A common interface for any component that needs to react when its
    /// parent IControllable is possessed or unpossessed.
    /// Distributed implementation: Attach components implementing this to the same GameObject (or children) as an IControllable.
    /// </summary>
    public interface IPossessionResponder
    {
        void OnPossessed();
        void OnUnpossessed();
    }
}
