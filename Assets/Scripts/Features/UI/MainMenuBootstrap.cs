#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Possession;
using VContainer.Unity;

namespace TinCan.Features.UI
{
    /// <summary>
    /// The single owner of the Cancel key for menus: opens the main menu while offline or when the player is in
    /// their own body, steps back/closes when a menu is open, and gates gameplay input while a menu is open.
    /// A Cancel press that changed possession this frame (vehicle exit) is not also used to open the menu.
    /// </summary>
    public class MainMenuBootstrap : IInitializable, ITickable
    {
        private readonly IMenuSystem _menus;
        private readonly INetworkService _networkService;
        private readonly IInputService _inputService;
        private readonly IPossessionState _possession;
        private readonly InputGate _inputGate;
        private readonly MenuDefinition _mainMenu;
        private NetworkState _lastState;
        private IPossessable? _lastPossession;

        public MainMenuBootstrap(
            IMenuSystem menus,
            INetworkService networkService,
            IInputService inputService,
            IPossessionState possession,
            InputGate inputGate,
            MenuDefinition mainMenu)
        {
            _menus = menus;
            _networkService = networkService;
            _inputService = inputService;
            _possession = possession;
            _inputGate = inputGate;
            _mainMenu = mainMenu;
        }

        public void Initialize()
        {
            _lastState = _networkService.State;
            _lastPossession = _possession.CurrentPossession;
            if (_lastState == NetworkState.Offline) _menus.Open(_mainMenu);
            _inputGate.GameplayBlocked = _menus.IsOpen;
        }

        public void Tick()
        {
            var state = _networkService.State;
            if (state != _lastState)
            {
                _lastState = state;
                HandleStateChanged(state);
            }
            else if (_inputService.WasActionTriggered(ActionNames.Cancel))
            {
                HandleCancel(state);
            }

            _lastPossession = _possession.CurrentPossession;
            _inputGate.GameplayBlocked = _menus.IsOpen;
        }

        private void HandleCancel(NetworkState state)
        {
            if (_menus.IsOpen)
            {
                _menus.Back();
                return;
            }

            bool possessionChangedThisFrame = _possession.CurrentPossession != _lastPossession;
            bool inOwnBody = _possession.CurrentPossession == _possession.PlayerActor;
            if (possessionChangedThisFrame) return;
            if (state != NetworkState.Offline && !inOwnBody) return;

            _menus.Open(_mainMenu);
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
