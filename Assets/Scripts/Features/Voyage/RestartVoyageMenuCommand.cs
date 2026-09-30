#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.UI;

namespace TinCan.Features.Voyage
{
    /// <summary>The end screen's Restart row: any player asks the server for a new voyage (a server RPC on a client).</summary>
    public class RestartVoyageMenuCommand : IMenuCommand
    {
        public const string Id = "VoyageRestart";

        private readonly IActorRegistry _actors;

        public RestartVoyageMenuCommand(IActorRegistry actors) => _actors = actors;

        public string CommandId => Id;

        public void Execute(MenuContext context) => _actors.GetActors<IVoyageState>().FirstOrDefault()?.RequestRestart();
    }
}
