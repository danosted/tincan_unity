#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard, per frame while open: reads the Shipyard context (orbit, zoom, aim, place, remove, rotate, change
    /// part, undo, redo), applies edits to the <see cref="ShipyardDocument"/>, keeps the stage's preview and ghost in
    /// step, and writes its state to the HUD. Save, load, new, launch and exit come from the shipyard's menu.
    /// Plan: .docs/plans/modular-airship-builder.md (S3).
    /// </summary>
    public sealed class ShipyardUseCase : ITickable, IShipyard
    {
        public const string HudTitle = "Shipyard";
        public const string HudPart = "Part";
        public const string HudFlight = "Flight";
        public const string HudControls = "Controls";
        public const string HudMessage = "Shipyard says";
        public const string NameField = "name";
        public const string DeckPartId = "hull.deck";
        public const string EnginePartId = "prop.engine";
        public const string BalloonPartId = "lift.balloon";

        private readonly ShipyardConfig _config;
        private readonly ShipyardInputContext _controls;
        private readonly IInputReader _input;
        private readonly IInputContextSwitch _contexts;
        private readonly IShipyardStage _stage;
        private readonly IShipPartCatalog _catalog;
        private readonly IShipDesignStore _store;
        private readonly ShipDesignEditProcessor _edits;
        private readonly ShipDesignValidator _validator;
        private readonly ShipStatsProcessor _stats;
        private readonly ShipDesignsConfig _designs;
        private readonly IShipDesigns _shipDesigns;
        private readonly IHudValues _hud;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ShipyardAimProcessor _aim = new();
        private ShipyardDocument? _document;
        private ShipDesign? _shown;
        private int _partIndex;
        private ShipyardAim _aimed;

        public ShipyardUseCase(ShipyardConfig config, ShipyardInputContext controls, IInputReader input, IInputContextSwitch contexts,
            IShipyardStage stage, IShipPartCatalog catalog, IShipDesignStore store, ShipDesignEditProcessor edits,
            ShipDesignValidator validator, ShipStatsProcessor stats, ShipDesignsConfig designs, IShipDesigns shipDesigns, IHudValues hud, INetworkService network, IActorRegistry actors)
        {
            _config = config;
            _controls = controls;
            _input = input;
            _contexts = contexts;
            _stage = stage;
            _catalog = catalog;
            _store = store;
            _edits = edits;
            _validator = validator;
            _stats = stats;
            _designs = designs;
            _shipDesigns = shipDesigns;
            _hud = hud;
            _network = network;
            _actors = actors;
        }

        public bool IsOpen { get; private set; }
        public ShipDesign? Design => _document?.Design;
        public ShipPartDefinition? SelectedPart => _catalog.Parts.Count > 0 ? _catalog.Parts[_partIndex % _catalog.Parts.Count] : null;
        public byte Orientation { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public IReadOnlyList<ShipDesignListing> SavedDesigns => _store.List();

        public void Open()
        {
            if (IsOpen) return;

            _document ??= new ShipyardDocument(InitialDesign(), _edits, _catalog, _designs.Limits);
            IsOpen = true;
            _contexts.SetOpen(_controls, true);
            _stage.Show();
            _shown = null;
            Say($"Building \"{_document.Design.Name}\". Esc for the menu.");
            Refresh();
        }

        public void Tick()
        {
            if (!IsOpen || _document == null) return;

            if (_input.IsPressed(_controls.OrbitHold)) _stage.Orbit(_input.ReadVector2(_controls.Orbit) * _config.OrbitSpeed);
            float zoom = _input.ReadAxis(_controls.Zoom);
            if (zoom != 0f) _stage.Zoom(zoom * _config.ZoomSpeed);

            if (_input.WasPressedThisFrame(_controls.NextPart)) Cycle(1);
            if (_input.WasPressedThisFrame(_controls.PreviousPart)) Cycle(-1);
            if (_input.WasPressedThisFrame(_controls.Rotate)) Rotate();
            if (_input.WasPressedThisFrame(_controls.Undo)) Undo();
            if (_input.WasPressedThisFrame(_controls.Redo)) Redo();

            _aimed = _aim.Aim(_stage.CursorRay(_input.ReadVector2(_controls.Point)));
            if (_input.WasPressedThisFrame(_controls.Place) && _aimed.IsValid) PlaceAt(_aimed.PlaceCell);
            if (_input.WasPressedThisFrame(_controls.Remove) && _aimed.PartCell is { } partCell) RemoveAt(partCell);

            Refresh();
        }

        public bool Select(string partId)
        {
            for (int i = 0; i < _catalog.Parts.Count; i++)
            {
                if (_catalog.Parts[i].PartId != partId) continue;
                _partIndex = i;
                return true;
            }

            return false;
        }

        public void Rotate() => Orientation = ShipPartOrientation.NextYaw(Orientation);

        public bool PlaceAt(ShipGridCell cell)
        {
            var part = SelectedPart;
            if (_document == null || part == null) return false;

            var result = _document.Apply(ShipDesignEdit.Place(part.PartId, cell, Orientation));
            if (!result.Applied) Say(result.Refusal!);
            Refresh();
            return result.Applied;
        }

        public bool RemoveAt(ShipGridCell cell)
        {
            if (_document == null) return false;
            if (!ShipPartFootprints.Occupancy(_document.Design, _catalog).TryGetValue(cell, out var instanceId)) return false;

            var result = _document.Apply(ShipDesignEdit.Remove(instanceId));
            Refresh();
            return result.Applied;
        }

        public bool Undo()
        {
            bool undone = _document?.Undo() ?? false;
            Refresh();
            return undone;
        }

        public bool Redo()
        {
            bool redone = _document?.Redo() ?? false;
            Refresh();
            return redone;
        }

        public bool Save(string name)
        {
            if (_document == null) return false;

            var title = string.IsNullOrWhiteSpace(name) ? _document.Design.Name : name.Trim();
            _document.Rename(title);
            var key = ShipDesignKeys.FromName(title);
            if (!_store.TrySave(key, _document.Design, out var error))
            {
                Say(error!);
                return false;
            }

            _document.MarkSaved();
            Say($"Saved \"{title}\" as {key}.");
            return true;
        }

        public bool Load(string name)
        {
            var key = ShipDesignKeys.IsValid(name) ? name : ShipDesignKeys.FromName(name);
            var result = _store.Load(key);
            if (!result.Succeeded)
            {
                Say(result.Error!);
                return false;
            }

            Document().Open(result.Design!);
            Say($"Loaded \"{result.Design!.Name}\".");
            Refresh();
            return true;
        }

        public void New()
        {
            Document().Open(NewDesign());
            Say("A new ship: a helm on a deck. Build from there.");
            Refresh();
        }

        public bool Launch()
        {
            if (_document == null) return false;

            var validation = _validator.Validate(_document.Design, _catalog, _designs.Limits);
            if (!validation.IsValid)
            {
                Say($"It cannot fly yet: {validation.Problems.First(p => p.IsBlocking).Message}");
                return false;
            }

            if (_network.IsActive && !_network.IsServer)
            {
                Say("Only the host launches a ship.");
                return false;
            }

            var design = _document.Design;
            _shipDesigns.Select(design);
            if (_network.IsServer)
            {
                foreach (var state in _actors.GetActors<IShipDesignState>().Where(s => s.Ship != null).ToList())
                {
                    _shipDesigns.TryApply(state, design, out _);
                }
            }

            Close();
            if (!_network.IsActive) _network.StartHost();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _contexts.SetOpen(_controls, false);
            _stage.Hide();
            _hud.Remove(HudTitle);
            _hud.Remove(HudPart);
            _hud.Remove(HudFlight);
            _hud.Remove(HudControls);
            _hud.Remove(HudMessage);
        }

        private void Cycle(int step)
        {
            int count = _catalog.Parts.Count;
            if (count == 0) return;
            _partIndex = ((_partIndex + step) % count + count) % count;
        }

        private void Refresh()
        {
            if (!IsOpen || _document == null) return;

            var design = _document.Design;
            if (!ReferenceEquals(design, _shown))
            {
                _stage.ShowDesign(design);
                _shown = design;
            }

            var part = SelectedPart;
            if (part != null && _aimed.IsValid)
            {
                bool fits = _document.Try(ShipDesignEdit.Place(part.PartId, _aimed.PlaceCell, Orientation)).Applied;
                _stage.ShowGhost(part, _aimed.PlaceCell, Orientation, fits);
            }
            else
            {
                _stage.HideGhost();
            }

            _hud.Set(HudTitle, $"{design.Name}{(_document.IsDirty ? " *" : string.Empty)}: {design.Parts.Count} parts, {Problems(design)}");
            _hud.Set(HudPart, part == null
                ? "no parts loaded"
                : $"{part.DisplayName} ({_partIndex % _catalog.Parts.Count + 1}/{_catalog.Parts.Count}), turned {Orientation % 4 * 90} deg");
            _hud.Set(HudFlight, _stats.Compute(design, _catalog, _designs.Flight).ToString());
            _hud.Set(HudControls, "LMB place, RMB remove, Q/E part, R turn, MMB drag orbit, wheel zoom, Ctrl+Z/Y undo/redo, Esc menu");
            _hud.Set(HudMessage, Message);
        }

        private string Problems(ShipDesign design)
        {
            var validation = _validator.Validate(design, _catalog, _designs.Limits);
            var blocking = validation.Problems.Where(p => p.IsBlocking).ToList();
            return blocking.Count == 0 ? "ready to fly" : $"{blocking.Count} problem(s): {blocking[0].Message}";
        }

        public void Say(string message) => Message = message;

        private ShipyardDocument Document() =>
            _document ??= new ShipyardDocument(NewDesign(), _edits, _catalog, _designs.Limits);

        private ShipDesign InitialDesign()
        {
            var result = _store.Load(_designs.DefaultDesign);
            return result.Succeeded ? result.Design! : NewDesign();
        }

        /// <summary>A helm midships on a 5 x 5 deck, with an engine and two balloons when those parts are loaded: a small ship that flies.</summary>
        private ShipDesign NewDesign()
        {
            var core = _catalog.Parts.FirstOrDefault(p => p.IsCore);
            var floor = _catalog.TryGet(DeckPartId, out var deck) ? deck : _catalog.Parts.FirstOrDefault(p => !p.IsCore && p.Footprint.Count == 1);
            var parts = new List<ShipPartPlacement>();
            if (floor != null)
            {
                for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                    parts.Add(new ShipPartPlacement(parts.Count + 1, floor.PartId, new ShipGridCell(x, -1, z), 0));
            }

            if (core != null) parts.Add(new ShipPartPlacement(parts.Count + 1, core.PartId, new ShipGridCell(0, 0, 0), 0));
            // An engine off the stern and a balloon either side, touching the deck: enough to fly.
            if (_catalog.TryGet(EnginePartId, out _)) parts.Add(new ShipPartPlacement(parts.Count + 1, EnginePartId, new ShipGridCell(0, -1, -3), 0));
            if (_catalog.TryGet(BalloonPartId, out _))
            {
                parts.Add(new ShipPartPlacement(parts.Count + 1, BalloonPartId, new ShipGridCell(-4, -1, 0), 0));
                parts.Add(new ShipPartPlacement(parts.Count + 1, BalloonPartId, new ShipGridCell(4, -1, 0), 0));
            }

            return new ShipDesign("New ship", string.Empty, parts.Count + 1, parts);
        }
    }
}
