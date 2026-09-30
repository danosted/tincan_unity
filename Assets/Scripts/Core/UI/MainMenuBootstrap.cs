#nullable enable
using TinCan.Core.Domain.Networking;
using VContainer.Unity;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Opens the main menu at startup and whenever the session ends, and closes every menu when a session starts. Keys
    /// are not read here: Cancel reaches the menus through the input contexts (<see cref="MenuBackInputHandler"/> while
    /// a menu is open, <see cref="OpenMenuInputHandler"/> otherwise), and the Menu context silences gameplay input while
    /// a menu is open.
    /// </summary>
    public class MainMenuBootstrap : IInitializable, ITickable
    {
        private readonly IMenuSystem _menus;
        private readonly INetworkService _networkService;
        private readonly MenuDefinition _mainMenu;
        private NetworkState _lastState;

        public MainMenuBootstrap(IMenuSystem menus, INetworkService networkService, MenuDefinition mainMenu)
        {
            _menus = menus;
            _networkService = networkService;
            _mainMenu = mainMenu;
        }

        public void Initialize()
        {
            _lastState = _networkService.State;
            if (_lastState == NetworkState.Offline) _menus.Open(_mainMenu);
        }

        public void Tick()
        {
            var state = _networkService.State;
            if (state == _lastState) return;

            _lastState = state;
            HandleStateChanged(state);
        }

        private void HandleStateChanged(NetworkState state)
        {
            switch (state)
            {
                case NetworkState.Host:
                case NetworkState.Server:
                case NetworkState.Client:
                    _menus.CloseAll();
                    break;
                case NetworkState.Offline when !_menus.IsOpen:
                    _menus.Open(_mainMenu);
                    break;
            }
        }
    }
}
