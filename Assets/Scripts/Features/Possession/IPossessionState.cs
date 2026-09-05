#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.Possession
{
    /// <summary>Read-only view of what the local player currently controls; lets UI code stay testable.</summary>
    public interface IPossessionState
    {
        IPossessable? CurrentPossession { get; }
        IPossessable? PlayerActor { get; }
    }
}
