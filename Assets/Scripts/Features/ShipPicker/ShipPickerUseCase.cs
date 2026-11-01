#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.UI;
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.ShipPicker
{
    /// <summary>
    /// Temporary ship picker: lists the store's designs as a menu, and on a pick validates the design, selects it and gives
    /// it to every ship in play, as the shipyard's Launch does (the swap is duplicated on purpose, so this feature stays
    /// easy to remove). Host only. Plan: .docs/plans/ship-picker-temporary.md.
    /// </summary>
    public sealed class ShipPickerUseCase : IShipPicker
    {
        public const string MenuId = "ShipPicker";
        public const string LogSource = "ShipPicker";
        /// <summary>A design row's id is this prefix and the design's key, so no key can clash with Back or the note.</summary>
        public const string RowPrefix = "ship/";

        private readonly IShipDesignStore _store;
        private readonly IShipDesigns _designs;
        private readonly ShipDesignValidator _validator;
        private readonly IShipPartCatalog _catalog;
        private readonly ShipDesignsConfig _config;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IEventPublisher _events;

        public ShipPickerUseCase(IShipDesignStore store, IShipDesigns designs, ShipDesignValidator validator, IShipPartCatalog catalog,
            ShipDesignsConfig config, INetworkService network, IActorRegistry actors, IEventPublisher events)
        {
            _store = store;
            _designs = designs;
            _validator = validator;
            _catalog = catalog;
            _config = config;
            _network = network;
            _actors = actors;
            _events = events;
        }

        public MenuDefinition BuildMenu(string? note = null)
        {
            var rows = new List<MenuItemDefinition>();
            foreach (var listing in _store.List())
            {
                rows.Add(new MenuItemDefinition
                {
                    ItemId = RowPrefix + listing.Key, Label = listing.ToString(), Kind = MenuItemKind.Command, CommandId = PickShipMenuCommand.Id,
                });
            }

            if (!string.IsNullOrEmpty(note)) rows.Add(new MenuItemDefinition { ItemId = "note", Label = note!, Kind = MenuItemKind.Note });
            rows.Add(new MenuItemDefinition { ItemId = "back", Label = "Back", Kind = MenuItemKind.Back });
            return MenuDefinition.Create(MenuId, "Change ship", rows.ToArray());
        }

        public bool Pick(string key, out string message)
        {
            if (_network.IsActive && !_network.IsServer) return Refuse("Only the host changes the ship.", out message);

            var loaded = _store.Load(key);
            if (!loaded.Succeeded) return Refuse($"\"{key}\" does not load: {loaded.Error}", out message);

            var design = loaded.Design!;
            var validation = _validator.Validate(design, _catalog, _config.Limits, _config.Frames, _config.Flight);
            if (!validation.IsValid) return Refuse($"\"{key}\" cannot fly: {validation.Problems.First(p => p.IsBlocking).Message}", out message);

            _designs.Select(design);
            int applied = 0;
            if (_network.IsServer)
            {
                foreach (var state in _actors.GetActors<IShipDesignState>().Where(s => s.Ship != null).ToList())
                {
                    if (_designs.TryApply(state, design, out _)) applied++;
                }
            }

            message = applied > 0
                ? $"{applied} ship(s) now fly \"{key}\"."
                : $"\"{key}\" is selected; no ship is in play, so the next one built flies it.";
            _events.LogInfo(LogSource, message);
            return true;
        }

        private bool Refuse(string reason, out string message)
        {
            message = reason;
            _events.LogWarning(LogSource, reason);
            return false;
        }
    }
}
