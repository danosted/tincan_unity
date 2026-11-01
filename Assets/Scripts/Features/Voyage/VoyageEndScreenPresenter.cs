#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.UI;
using VContainer.Unity;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Presentation, every peer: while the voyage is over, its end screen is up (Arrived or Lost, each with Restart and
    /// Quit) as a pinned modal request, so Esc cannot drop a player into a dead session (it opens the pause menu over the
    /// screen instead) and it covers any other menu; it is withdrawn when a new voyage starts. Menus silence gameplay
    /// input through the Menu input context.
    /// </summary>
    public class VoyageEndScreenPresenter : ITickable
    {
        public const string MenuOwner = "Voyage.End";

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

            if (screen == null) _menus.Withdraw(MenuOwner);
            else _menus.Request(new MenuRequest(MenuOwner, screen, MenuKind.Modal, pinned: true));
        }
    }
}
