#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.UI;

namespace TinCan.Features.Respawn
{
    /// <summary>Overboard menu row: ask the server to hang the local player on the ship's leash.</summary>
    public sealed class HangOnLeashMenuCommand : IMenuCommand
    {
        public const string Id = "HangOnLeash";

        private readonly IActorRegistry _actors;
        private readonly OverboardChoice _choice;
        private readonly ITimeService _time;

        public HangOnLeashMenuCommand(IActorRegistry actors, OverboardChoice choice, ITimeService time)
        {
            _actors = actors;
            _choice = choice;
            _time = time;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            OverboardRequest.Send(_actors, _choice, OverboardChoiceKind.Leash, _time.Time);
            context.Menus.CloseAll();
        }
    }
}
