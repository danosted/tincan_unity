#nullable enable
using System.Linq;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Opens the main menu at startup and whenever the session ends, and closes every menu when a session starts. Keys
    /// are not read here: Cancel reaches the menus through the input contexts (<see cref="MenuBackInputHandler"/> while
    /// a menu is open, <see cref="OpenMenuInputHandler"/> otherwise), and the Menu context silences gameplay input while
    /// a menu is open.
    /// When the session ended with a reason (refused by the host, connection lost), the main menu opens with that line
    /// on top, even over the Join menu the attempt came from.
    /// </summary>
    public class MainMenuBootstrap : IInitializable, ITickable
    {
        public const string NoticeItemId = "session-notice";

        private readonly IMenuSystem _menus;
        private readonly INetworkService _networkService;
        private readonly ISessionService _session;
        private readonly MenuDefinition _mainMenu;
        private NetworkState _lastState;
        private MenuDefinition? _noticeMenu;

        public MainMenuBootstrap(IMenuSystem menus, INetworkService networkService, ISessionService session, MenuDefinition mainMenu)
        {
            _menus = menus;
            _networkService = networkService;
            _session = session;
            _mainMenu = mainMenu;
        }

        public void Initialize()
        {
            _lastState = _networkService.State;
            if (_lastState == NetworkState.Offline) OpenMain();
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
                case NetworkState.Offline when _session.LastEnd is { Message: { Length: > 0 } message }:
                    OpenMainWithNotice(message);
                    break;
                case NetworkState.Offline when !_menus.IsOpen:
                    OpenMain();
                    break;
            }
        }

        private void OpenMain() => _menus.Request(new MenuRequest(MenuOwners.Main, _mainMenu, MenuKind.Screen));

        private void OpenMainWithNotice(string message)
        {
            var notice = new MenuItemDefinition { ItemId = NoticeItemId, Label = message, Kind = MenuItemKind.Note };
            var menu = MenuDefinition.Create(_mainMenu.MenuId, _mainMenu.Title, _mainMenu.Items.Prepend(notice).ToArray());
            menu.name = _mainMenu.name;

            // Withdraw first: a request for the menu already open under this owner (the Join menu sits on its stack) is ignored.
            _menus.Withdraw(MenuOwners.Main);
            _menus.Request(new MenuRequest(MenuOwners.Main, menu, MenuKind.Screen));

            if (_noticeMenu != null) Object.DestroyImmediate(_noticeMenu);
            _noticeMenu = menu;
        }
    }
}
