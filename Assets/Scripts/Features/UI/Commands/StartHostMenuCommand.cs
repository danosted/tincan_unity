#nullable enable
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.UI.Commands
{
    public class StartHostMenuCommand : IMenuCommand
    {
        public const string Id = "StartHost";

        private readonly INetworkService _networkService;

        public StartHostMenuCommand(INetworkService networkService)
        {
            _networkService = networkService;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_networkService.IsActive) return;
            _networkService.StartHost();
        }
    }
}
