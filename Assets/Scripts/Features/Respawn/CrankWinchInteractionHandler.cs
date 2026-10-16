#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Server: Interact on a leash winch (IA_CrankWinch) starts reeling in whoever hangs on its leash point, or stops it.
    /// With nobody on the leash it does not start. The rope's shortening is <see cref="LeashRopeUseCase"/>'s.
    /// </summary>
    public sealed class CrankWinchInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Respawn";

        private readonly LeashStore _leashes;
        private readonly IEventPublisher _events;

        public CrankWinchInteractionHandler(LeashStore leashes, IEventPublisher events)
        {
            _leashes = leashes;
            _events = events;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not ILeashWinch winch || winch.Point is not { } point) return;

            if (!_leashes.IsWinching(point) && !_leashes.AnyOn(point))
            {
                _events.LogInfo(LogSource, "Nobody hangs on this leash; the winch stays still.");
                return;
            }

            bool reeling = _leashes.ToggleWinch(point);
            _events.Publish(new WinchToggledEvent(context.Requester?.Id ?? default, point.Id, reeling));
        }
    }
}
