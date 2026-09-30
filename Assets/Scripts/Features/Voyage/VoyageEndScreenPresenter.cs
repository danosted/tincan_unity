#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.UI;
using VContainer.Unity;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Presentation, every peer: while the voyage is over, its end screen is up (Arrived or Lost, each with Restart and
    /// Quit). It reopens if closed, so Esc cannot drop a player into a dead session, and it closes itself when a new
    /// voyage starts. Menus silence gameplay input through the Menu input context.
    /// </summary>
    public class VoyageEndScreenPresenter : ITickable
    {
        private readonly IActorRegistry _actors;
        private readonly IMenuSystem _menus;
        private readonly VoyageConfig _config;

        public VoyageEndScreenPresenter(IActorRegistry actors, IMenuSystem menus, VoyageConfig config)
        {
            _actors = actors;
            _menus = menus;
            _config = config;
        }

        public void Tick()
        {
            var phase = _actors.GetActors<IVoyageState>().FirstOrDefault()?.Phase ?? VoyagePhase.Idle;
            var screen = phase switch
            {
                VoyagePhase.Arrived => _config.ArrivedMenu,
                VoyagePhase.Lost => _config.LostMenu,
                _ => null
            };

            string? showing = _menus.IsOpen ? _menus.Current?.MenuId : null;
            if (screen == null)
            {
                if (showing != null && IsEndScreen(showing)) _menus.CloseAll();
                return;
            }

            if (showing == screen.MenuId) return;
            if (showing != null && !IsEndScreen(showing)) return; // another menu (the main menu) is the player's choice
            _menus.CloseAll();
            _menus.Open(screen);
        }

        private bool IsEndScreen(string menuId) =>
            (_config.ArrivedMenu != null && _config.ArrivedMenu.MenuId == menuId) ||
            (_config.LostMenu != null && _config.LostMenu.MenuId == menuId);
    }
}
