#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Features.SkillChecks;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Server: E on an engine station (the telegraph, the boiler) opens its skill check for the player who pressed it. The check refuses (and
    /// says why in the log) while someone is doing it or while it rests.
    /// </summary>
    public sealed class StartEngineBoostInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "Boons";

        private readonly ISkillChecks _checks;
        private readonly IEventPublisher _events;

        public StartEngineBoostInteractionHandler(ISkillChecks checks, IEventPublisher events)
        {
            _checks = checks;
            _events = events;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IEngineStation station) return;
            if (context.Requester is not IPossessable { OwnerId: { } clientId }) return;

            var check = station.Check;
            if (check == null)
            {
                _events.LogWarning(LogSource, "This engine station has no skill check host.");
                return;
            }

            _checks.TryOpen(check, clientId);
        }
    }
}
