#nullable enable
using System.Linq;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Cancel while the shipyard is open: opens its menu, with the design's name in the Name field and the saved designs
    /// listed in the HUD.
    /// </summary>
    public sealed class ShipyardMenuInputHandler : InputCommandHandler<OpenShipyardMenuCommand>
    {
        private readonly IShipyard _shipyard;
        private readonly IMenuSystem _menus;
        private readonly ShipyardConfig _config;

        public ShipyardMenuInputHandler(IShipyard shipyard, IMenuSystem menus, ShipyardConfig config)
        {
            _shipyard = shipyard;
            _menus = menus;
            _config = config;
        }

        protected override bool Handle(OpenShipyardMenuCommand command)
        {
            if (!_shipyard.IsOpen || _config.Menu == null) return false;

            _menus.Open(_config.Menu);
            _menus.SetValue(ShipyardUseCase.NameField, _shipyard.Design?.Name ?? string.Empty);
            var saved = _shipyard.SavedDesigns.Select(d => d.ToString()).ToList();
            _shipyard.Say(saved.Count == 0 ? "No saved designs yet." : $"Designs: {string.Join(", ", saved)}");
            return true;
        }
    }
}
