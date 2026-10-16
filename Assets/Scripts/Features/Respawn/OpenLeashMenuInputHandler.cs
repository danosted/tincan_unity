#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Core.UI;

namespace TinCan.Features.Respawn
{
    /// <summary>Interact while hanging on a leash (the Leashed context's route): opens the respawn choice, without forcing it.</summary>
    public sealed class OpenLeashMenuInputHandler : InputCommandHandler<OpenLeashMenuCommand>
    {
        private readonly RespawnRules _rules;
        private readonly IActorRegistry _actors;
        private readonly IMenuSystem _menus;

        public OpenLeashMenuInputHandler(RespawnRules rules, IActorRegistry actors, IMenuSystem menus)
        {
            _rules = rules;
            _actors = actors;
            _menus = menus;
        }

        protected override bool Handle(OpenLeashMenuCommand command)
        {
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            if (_menus.IsOpen || local == null || _rules.LeashedTag == null || !local.HasTag(_rules.LeashedTag)) return false;

            _menus.Open(OverboardMenus.Leashed(RespawnProcessor.RuleFor(_rules.Rules, RespawnCause.Overboard)));
            return true;
        }
    }
}
