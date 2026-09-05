#nullable enable
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.UI.Commands
{
    /// <summary>
    /// Connects as a client using the "address" and "port" values of the menu the command lives in.
    /// </summary>
    public class JoinGameMenuCommand : IMenuCommand
    {
        public const string Id = "JoinGame";
        public const string AddressItemId = "address";
        public const string PortItemId = "port";
        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        private readonly INetworkService _networkService;

        public JoinGameMenuCommand(INetworkService networkService)
        {
            _networkService = networkService;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_networkService.IsActive) return;

            var address = context.GetValue(AddressItemId);
            if (string.IsNullOrWhiteSpace(address)) address = DefaultAddress;

            if (!ushort.TryParse(context.GetValue(PortItemId), out var port) || port == 0) port = DefaultPort;

            _networkService.SetConnection(address.Trim(), port);
            _networkService.StartClient();
        }
    }
}
