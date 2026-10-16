#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.UI;

namespace TinCan.Features.Respawn
{
    /// <summary>Overboard or leash menu row: ask the server to respawn the local player aboard (it applies the respawn rules).</summary>
    public sealed class RespawnAboardMenuCommand : IMenuCommand
    {
        public const string Id = "RespawnAboard";

        private readonly IActorRegistry _actors;
        private readonly OverboardChoice _choice;
        private readonly ITimeService _time;

        public RespawnAboardMenuCommand(IActorRegistry actors, OverboardChoice choice, ITimeService time)
        {
            _actors = actors;
            _choice = choice;
            _time = time;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            OverboardRequest.Send(_actors, _choice, OverboardChoiceKind.Respawn, _time.Time);
            context.Menus.CloseAll();
        }
    }
}
