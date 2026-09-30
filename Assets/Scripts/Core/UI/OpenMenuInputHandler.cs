#nullable enable
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Possession;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Cancel that nobody above wanted: opens the main menu while offline or when the player is in their own body.
    /// Controlling something else (the helm) declines, so a stray Cancel there does nothing.
    /// </summary>
    public sealed class OpenMenuInputHandler : InputCommandHandler<OpenMenuCommand>
    {
        private readonly IMenuSystem _menus;
        private readonly INetworkService _network;
        private readonly IPossessionState _possession;
        private readonly MenuDefinition _mainMenu;

        public OpenMenuInputHandler(IMenuSystem menus, INetworkService network, IPossessionState possession, MenuDefinition mainMenu)
        {
            _menus = menus;
            _network = network;
            _possession = possession;
            _mainMenu = mainMenu;
        }

        protected override bool Handle(OpenMenuCommand command)
        {
            if (_menus.IsOpen) return false;

            bool inOwnBody = _possession.CurrentPossession == _possession.PlayerActor;
            if (_network.State != NetworkState.Offline && !inOwnBody) return false;

            _menus.Open(_mainMenu);
            return true;
        }
    }
}
