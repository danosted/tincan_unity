#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.UI;

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// The Lost screen's Respawn row: any player asks the server to bring the ship back at its last safe spot (a server
    /// RPC on a client).
    /// </summary>
    public sealed class RespawnShipMenuCommand : IMenuCommand
    {
        public const string Id = "ShipRespawn";

        private readonly IActorRegistry _actors;

        public RespawnShipMenuCommand(IActorRegistry actors) => _actors = actors;

        public string CommandId => Id;

        public void Execute(MenuContext context) => _actors.GetActors<IShipLossState>().FirstOrDefault()?.RequestRespawn();
    }
}
