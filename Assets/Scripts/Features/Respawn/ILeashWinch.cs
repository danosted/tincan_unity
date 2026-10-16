#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.Respawn
{
    /// <summary>A winch at a leash point: press Interact on it to reel in (or stop reeling) whoever hangs on that point.</summary>
    public interface ILeashWinch : IInteractable
    {
        ILeashPoint? Point { get; }
    }
}
