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
    /// The shipyard, per frame while open: reads the Shipyard context, flies the camera (<see cref="ShipyardFlyProcessor"/>),
    /// and builds in one of two modes. Parts mode places, removes and turns catalog parts on the working level; frame mode
    /// draws the frame with the cursor (<see cref="ShipyardFramePickProcessor"/>, <see cref="ShipyardFrameToolProcessor"/>:
    /// click to add, join and split; drag to move and merge; Fill planks a face; Level Up/Down raise a node). Every change
    /// goes through the <see cref="ShipyardDocument"/> (undo, redo). The stage shows the preview, the ghost or the frame
    /// overlay, and the panels (parts to pick, controls for the mode); the HUD shows the design's state. Save, load, new,
    /// launch and exit come from the shipyard's menu. Plans: .docs/plans/modular-airship-builder.md (S3),
    /// .docs/plans/ship-frames.md (S2).
    /// </summary>
    public sealed class ShipyardUseCase : ITickable, IShipyard
    {
        public const string HudTitle = "Shipyard";
        public const string HudPart = "Building";
        public const string HudFlight = "Flight";
        public const string HudMessage = "Shipyard says";
        public const string NameField = "name";
        public const string DeckPartId = "hull.deck";
        public const string EnginePartId = "prop.engine";
        public const string BalloonPartId = "lift.balloon";

        private readonly ShipyardConfig _config;
        private readonly ShipyardInputContext _controls;
        private readonly IInputReader _input;
        private readonly IInputBindings _bindings;
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
        private readonly ShipyardSnapProcessor _snap = new();
        private readonly ShipyardFlyProcessor _fly = new();
        private readonly ShipyardFramePickProcessor _pick = new();
        private readonly ShipyardFrameToolProcessor _tools = new();
        private readonly ShipyardPanelPresenter _panels = new();
        private ShipyardDocument? _document;
        private ShipDesign? _shown;
        private int _partIndex;
        private ShipyardAim _aimed;
        private ShipyardFramePick _framePick;
        private bool _looking;
        private float _lookTravel;
        private int? _dragNode;
        private ShipDesign? _dragPreview;
        private bool _heightDrag;
        private bool _dragMoving;
        private Vector2 _dragStartScreen;
        private Vector3 _dragOrigin;
        private Vector3 _dragAnchor;
        private float _dragStartY;
        private int _dragStartDrift;

        public ShipyardUseCase(ShipyardConfig config, ShipyardInputContext controls, IInputReader input, IInputBindings bindings,
            IInputContextSwitch contexts, IShipyardStage stage, IShipPartCatalog catalog, IShipDesignStore store, ShipDesignEditProcessor edits,
            ShipDesignValidator validator, ShipStatsProcessor stats, ShipDesignsConfig designs, IShipDesigns shipDesigns, IHudValues hud,
            INetworkService network, IActorRegistry actors)
        {
            _config = config;
            _controls = controls;
            _input = input;
            _bindings = bindings;
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
            _stage.PartPicked += partId =>
            {
                Select(partId);
                Refresh();
            };
            _stage.ModePicked += SetMode;
        }

        public bool IsOpen { get; private set; }
        public ShipDesign? Design => _document?.Design;
        public ShipyardMode Mode { get; private set; }
        public ShipPartDefinition? SelectedPart => _catalog.Parts.Count > 0 ? _catalog.Parts[_partIndex % _catalog.Parts.Count] : null;
        public byte Orientation { get; private set; }

        /// <summary>The height level new parts go on (PgUp/PgDn). 0 is the helm's level; the starter's deck is -1.</summary>
        public int Level { get; private set; }
        public bool DeleteMode { get; private set; }
        public int? SelectedNode { get; private set; }

        /// <summary>Frame mode: the story new nodes go on (PgUp/PgDn); one above the top story starts a new one.</summary>
        public int Story { get; private set; }
        public ShipyardCameraPose Camera { get; private set; }
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
            FrameCamera();
            Say($"Building \"{_document.Design.Name}\". Esc for the menu.");
            Refresh();
        }

        public void Tick() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!IsOpen || _document == null) return;

            FlyCamera(deltaTime);

            if (_input.WasPressedThisFrame(_controls.Mode)) SetMode(Mode == ShipyardMode.Parts ? ShipyardMode.Frame : ShipyardMode.Parts);
            if (_input.WasPressedThisFrame(_controls.Undo)) Undo();
            if (_input.WasPressedThisFrame(_controls.Redo)) Redo();
            if (_input.WasPressedThisFrame(_controls.DeleteMode)) ToggleDeleteMode();

            var point = _input.ReadVector2(_controls.Point);
            bool free = !_looking && !_stage.IsPointerOverPanel(point);
            if (Mode == ShipyardMode.Parts) PartsTick(point, free);
            else FrameTick(point, free);

            Refresh();
        }

        public void SetMode(ShipyardMode mode)
        {
            if (mode == Mode) return;

            Mode = mode;
            EndDrag();
            Say(mode == ShipyardMode.Frame
                ? "Frame mode: click the floor to place nodes, click nodes to join them; a closed loop becomes a planked face."
                : "Parts mode: pick a part in the panel and place it.");
            Refresh();
        }

        public void ToggleDeleteMode()
        {
            DeleteMode = !DeleteMode;
            Say(DeleteMode ? "Delete mode: click what is highlighted to delete it. X to build again." : "Build mode.");
            Refresh();
        }

        public void ChangeLevel(int delta)
        {
            Level = Mathf.Clamp(Level + delta, -_designs.Limits.MaxExtent, _designs.Limits.MaxExtent);
            Refresh();
        }

        public void ChangeStory(int delta)
        {
            int top = _document?.Design.Frame.StoryHeights.Count ?? 0;
            Story = Mathf.Clamp(Story + delta, 0, Mathf.Min(top, _designs.Limits.MaxStories - 1));
            Say(Story >= top ? $"Story {Story}: a new story; the first node placed starts it." : $"Working on story {Story}.");
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

            var taken = ShipPartFootprints.Occupancy(_document.Design, _catalog);
            bool anything = taken.Count > 0 || _document.Design.Frame.Nodes.Count > 0;
            if (anything && !ShipyardSnapProcessor.Attaches(taken, ShipFrameShape.From(_document.Design.Frame), ShipPartFootprints.CellsOf(cell, Orientation, part).ToList(), _designs.Frames))
            {
                Say($"{cell} does not touch the ship: parts are built onto it, never floating.");
                Refresh();
                return false;
            }

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

        public bool FrameClickAt(Vector3 point)
        {
            if (_document == null) return false;
            var pick = PickFrame(Downwards(point));
            bool changed = Use(DeleteMode
                ? _tools.Delete(_document.Design, pick, SelectedNode, _designs.Limits)
                : _tools.Click(_document.Design, pick, SelectedNode, FrameSettings()));
            Refresh();
            return changed;
        }

        public bool FrameExtrudeAt(Vector3 point)
        {
            if (_document == null) return false;
            bool changed = Use(_tools.Extrude(_document.Design, PickFrame(Downwards(point)), SelectedNode, _config.ExtrudeLength, FrameSettings()));
            Refresh();
            return changed;
        }

        public bool FrameFillAt(Vector3 point)
        {
            if (_document == null) return false;
            bool changed = Use(_tools.ToggleFill(_document.Design, PickFrame(Downwards(point)), SelectedNode, _designs.Limits));
            Refresh();
            return changed;
        }

        public bool Undo()
        {
            EndDrag();
            bool undone = _document?.Undo() ?? false;
            Refresh();
            return undone;
        }

        public bool Redo()
        {
            EndDrag();
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
            SelectedNode = null;
            Story = 0;
            EndDrag();
            FrameCamera();
            Say($"Loaded \"{result.Design!.Name}\".");
            Refresh();
            return true;
        }

        public void New()
        {
            Document().Open(NewDesign());
            SelectedNode = null;
            Story = 0;
            EndDrag();
            FrameCamera();
            Say("A new ship: a helm on a deck. Build from there.");
            Refresh();
        }

        public bool Launch()
        {
            if (_document == null) return false;

            var validation = _validator.Validate(_document.Design, _catalog, _designs.Limits, _designs.Frames);
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
            EndDrag();
            _looking = false;
            _contexts.SetOpen(_controls, false);
            _stage.Hide();
            _hud.Remove(HudTitle);
            _hud.Remove(HudPart);
            _hud.Remove(HudFlight);
            _hud.Remove(HudMessage);
        }

        public void Say(string message) => Message = message;

        private void FlyCamera(float deltaTime)
        {
            bool looking = _input.IsPressed(_controls.LookHold);
            var look = _input.ReadVector2(_controls.Look);
            if (looking != _looking)
            {
                // A right click that hardly moved the mouse deselects in frame mode; a drag only looked around.
                if (!looking && _lookTravel <= _config.ClickSlop && Mode == ShipyardMode.Frame && SelectedNode != null)
                {
                    SelectedNode = null;
                    Say("Deselected.");
                }

                _looking = looking;
                _lookTravel = 0f;
                _stage.SetLooking(looking);
            }

            var pose = Camera;
            if (looking)
            {
                _lookTravel += look.magnitude;
                pose = _fly.Look(pose, look * _config.LookSpeed, _config.MaxPitch);
            }
            float speed = _config.FlySpeed * (_input.IsPressed(_controls.Fast) ? _config.FastMultiplier : 1f);
            pose = _fly.Fly(pose, _input.ReadVector2(_controls.Fly), _input.ReadAxis(_controls.Rise), _input.ReadAxis(_controls.Zoom) * _config.ZoomStep,
                speed, deltaTime);

            if (pose.Position == Camera.Position && pose.Yaw == Camera.Yaw && pose.Pitch == Camera.Pitch) return;
            Camera = pose;
            _stage.PlaceCamera(pose);
        }

        /// <summary>A view of the whole design (parts and frame), or of the working level's floor for an empty one.</summary>
        private void FrameCamera()
        {
            var design = _document?.Design;
            var bounds = design != null && ShipHullAssembler.TryGetBounds(design, _catalog, _designs.Frames, out var found)
                ? found
                : new Bounds(Vector3.zero, Vector3.one * 4f);
            Camera = _fly.Frame(bounds, _config.FieldOfView, _config.StartYaw, _config.StartPitch, _config.StartDistance);
            _stage.PlaceCamera(Camera);
        }

        private void PartsTick(Vector2 point, bool free)
        {
            if (_input.WasPressedThisFrame(_controls.Rotate)) Rotate();
            if (_input.WasPressedThisFrame(_controls.LevelUp)) ChangeLevel(1);
            if (_input.WasPressedThisFrame(_controls.LevelDown)) ChangeLevel(-1);

            _aimed = free ? _aim.Aim(_stage.CursorRay(point), Level) : ShipyardAim.None;
            if (free && _input.WasPressedThisFrame(_controls.Place)) Act();
        }

        /// <summary>
        /// Frame mode: a press on a node may become a drag (shown live, committed on release: a move, or a merge when
        /// dropped on another node); a press anywhere else is a click. Fill and Level Up/Down act on the face under the
        /// cursor and the selected node.
        /// </summary>
        private void FrameTick(Vector2 point, bool free)
        {
            if (_document == null) return;
            if (_input.WasPressedThisFrame(_controls.LevelUp)) ChangeStory(1);
            if (_input.WasPressedThisFrame(_controls.LevelDown)) ChangeStory(-1);

            var ray = _stage.CursorRay(point);
            _framePick = free || _dragNode != null ? PickFrame(ray, _dragNode ?? 0) : default;
            if (free && _input.WasPressedThisFrame(_controls.Fill)) Use(_tools.ToggleFill(_document.Design, _framePick, SelectedNode, _designs.Limits));
            if (free && _input.WasPressedThisFrame(_controls.Extrude)) Use(_tools.Extrude(_document.Design, _framePick, SelectedNode, _config.ExtrudeLength, FrameSettings()));

            if (_dragNode is { } dragged)
            {
                // Nothing moves until the mouse has moved a few pixels: a press and release in place is a click (select).
                if (!_dragMoving && (point - _dragStartScreen).magnitude >= _config.DragThresholdPixels) _dragMoving = true;
                if (_input.IsPressed(_controls.Place))
                {
                    if (!_dragMoving) return;
                    _dragPreview = (_heightDrag
                        ? _tools.HeightPreview(_document.Design, dragged, DraggedDrift(point), _designs.Limits)
                        : _tools.DragPreview(_document.Design, dragged, DragTarget(ray), FrameSettings())) ?? _dragPreview;
                    return;
                }

                // Released: a drag that moved is a move, a merge or (with Height held at the start) a new height; one that
                // did not is a click on that node.
                var result = _dragPreview != null
                    ? _heightDrag
                        ? _tools.SetHeight(_document.Design, dragged, DraggedDrift(point), _designs.Limits)
                        : _tools.Drop(_document.Design, dragged, _framePick, DragTarget(ray), FrameSettings())
                    : _tools.Click(_document.Design, new ShipyardFramePick(ShipyardFramePickKind.Node, dragged, Vector3.zero, false, Vector3.zero),
                        SelectedNode, FrameSettings());
                EndDrag();
                Use(result);
                return;
            }

            if (!free || !_input.WasPressedThisFrame(_controls.Place)) return;
            if (DeleteMode) Use(_tools.Delete(_document.Design, _framePick, SelectedNode, _designs.Limits));
            else if (_framePick.Is(ShipyardFramePickKind.Node)) StartDrag(_framePick.Id, point, ray);
            else Use(_tools.Click(_document.Design, _framePick, SelectedNode, FrameSettings()));
        }

        private bool Use(ShipyardFrameToolResult result)
        {
            if (result.Changed) _document!.Commit(result.Design!, result.Undo!);
            SelectedNode = result.Selected;
            if (result.Message.Length > 0) Say(result.Message);
            return result.Changed;
        }

        private void StartDrag(int node, Vector2 point, ShipyardRay ray)
        {
            var frame = _document!.Design.Frame;
            _dragNode = node;
            _dragMoving = false;
            _heightDrag = _input.IsPressed(_controls.Height);
            _dragStartScreen = point;
            _dragStartY = point.y;
            _dragStartDrift = frame.TryGetNode(node, out var start) ? start.Y : 0;
            _dragOrigin = frame.Position(node);
            _dragAnchor = AtHeight(ray, _dragOrigin.y) ?? _dragOrigin;
        }

        /// <summary>
        /// Where a drag puts the node: moved by as much as the cursor has moved on the plane at the node's own height, so
        /// it never jumps to where the cursor meets some other floor.
        /// </summary>
        private Vector3 DragTarget(ShipyardRay ray) => _dragOrigin + ((AtHeight(ray, _dragOrigin.y) ?? _dragAnchor) - _dragAnchor);

        private static Vector3? AtHeight(ShipyardRay ray, float y)
        {
            if (Mathf.Abs(ray.Direction.y) < 1e-4f) return null;
            float t = (y - ray.Origin.y) / ray.Direction.y;
            return t > 0f ? ray.Origin + ray.Direction * t : null;
        }

        /// <summary>An Alt-drag's drift: the start drift plus one frame unit per <see cref="ShipyardConfig.HeightPixelsPerUnit"/> moved up.</summary>
        private int DraggedDrift(Vector2 point) =>
            _dragStartDrift + Mathf.RoundToInt((point.y - _dragStartY) / Mathf.Max(1f, _config.HeightPixelsPerUnit));

        private void EndDrag()
        {
            _dragNode = null;
            _dragPreview = null;
            _heightDrag = false;
        }

        private ShipyardFramePick PickFrame(ShipyardRay ray, int ignoreNode = 0) =>
            _pick.Pick(ShipFrameShape.From(_document!.Design.Frame), ray, FramePlaneY(), _config.PickRadius, ignoreNode);

        /// <summary>A ray straight down onto a point, from well above the ship.</summary>
        private static ShipyardRay Downwards(Vector3 point) => new(new Vector3(point.x, point.y + 50f, point.z), Vector3.down);

        /// <summary>The frame's working plane: the working story's floor, or the working level's floor before there is a frame.</summary>
        private float FramePlaneY()
        {
            var frame = _document?.Design.Frame;
            return frame != null && frame.StoryHeights.Count > 0
                ? ShipFrameUnits.ToMetres(frame.StoryFloor(Story))
                : ShipyardAimProcessor.FloorY(Level);
        }

        private ShipyardFrameToolSettings FrameSettings() => new(Story, _config.FrameSnap,
            ShipFrameUnits.FromMetres(ShipyardAimProcessor.FloorY(Level)), _config.StoryHeight, _designs.Limits);

        private void Refresh()
        {
            if (!IsOpen || _document == null) return;

            var design = _dragPreview ?? _document.Design;
            if (!ReferenceEquals(design, _shown))
            {
                _stage.ShowDesign(design);
                _shown = design;
            }

            if (Mode == ShipyardMode.Parts) RefreshParts();
            else RefreshFrame(design);

            var shown = _document.Design;
            _hud.Set(HudTitle, $"{shown.Name}{(_document.IsDirty ? " *" : string.Empty)}: {shown.Parts.Count} parts, " +
                               $"{shown.Frame.Faces.Count} frame faces, {Problems(shown)}");
            _hud.Set(HudPart, Mode == ShipyardMode.Parts ? PartLine() : FrameLine(shown));
            _hud.Set(HudFlight, _stats.Compute(shown, _catalog, _designs.Flight, _designs.Frames).ToString());
            _hud.Set(HudMessage, Message);

            var cancel = _controls.Routes.FirstOrDefault(r => r.Command is OpenShipyardMenuCommand).Action;
            _stage.ShowPanel(_panels.Build(Mode, _catalog.Parts, SelectedPart?.PartId, DeleteMode, _controls, cancel,
                action => action == null ? "?" : _bindings.DescribeAction(action)));
        }

        private void RefreshParts()
        {
            _stage.HideFrameMarks();
            _stage.ShowLevel(ShipyardAimProcessor.FloorY(Level));

            var part = SelectedPart;
            if (DeleteMode)
            {
                if (Hovered(out var hovered, out var hoveredPart)) _stage.ShowHighlight(hoveredPart, hovered);
                else _stage.HideGhost();
            }
            else if (part != null && _aimed.IsValid)
            {
                bool attached = Target(out var cell);
                bool fits = attached && _document!.Try(ShipDesignEdit.Place(part.PartId, cell, Orientation)).Applied;
                _stage.ShowGhost(part, cell, Orientation, fits);
            }
            else
            {
                _stage.HideGhost();
            }
        }

        private void RefreshFrame(ShipDesign design)
        {
            _stage.HideGhost();
            _stage.ShowLevel(FramePlaneY());

            // The edge a click would make from the selected node: to the node under the cursor (a join, or a face split),
            // or to where a new node would go.
            Vector3? pending = null;
            if (SelectedNode is { } selected && _dragNode == null && !DeleteMode)
            {
                if (_framePick.Is(ShipyardFramePickKind.Node))
                {
                    if (_framePick.Id != selected) pending = design.Frame.Position(_framePick.Id);
                }
                else if (_framePick.OnPlane)
                {
                    var (x, z) = ShipyardFrameToolProcessor.Snap(_framePick.PlanePoint, _config.FrameSnap);
                    pending = new Vector3(ShipFrameUnits.ToMetres(x), _framePick.PlanePoint.y, ShipFrameUnits.ToMetres(z));
                }
            }

            _stage.ShowFrameMarks(design.Frame, new ShipyardFrameMarks(SelectedNode, _framePick, DeleteMode, pending));
        }

        private string PartLine()
        {
            var part = SelectedPart;
            return part == null
                ? "no parts loaded"
                : $"{part.DisplayName}, turned {Orientation % 4 * 90} deg, level {Level}{(DeleteMode ? ", DELETE MODE" : string.Empty)}";
        }

        private string FrameLine(ShipDesign design)
        {
            var frame = design.Frame;
            string selected = SelectedNode is { } id && frame.TryGetNode(id, out var node)
                ? $", node #{id} selected (drift {ShipFrameUnits.ToMetres(node.Y):0.0} m)"
                : string.Empty;
            return $"Frame: story {Story} of {frame.StoryHeights.Count}, {frame.Nodes.Count} nodes, {frame.Edges.Count} edges, {frame.Faces.Count} faces{selected}{(DeleteMode ? ", DELETE MODE" : string.Empty)}";
        }

        /// <summary>A click: delete the part under the cursor in delete mode, else place the selected part.</summary>
        private void Act()
        {
            if (DeleteMode)
            {
                if (_aimed.PartCell is { } partCell) RemoveAt(partCell);
                return;
            }

            if (!_aimed.IsValid) return;
            if (Target(out var cell)) PlaceAt(cell);
            else Say("Parts are built onto the ship: aim next to it, or change level with PgUp/PgDn.");
        }

        /// <summary>The part under the cursor, if any.</summary>
        private bool Hovered(out ShipPartPlacement placement, out ShipPartDefinition part)
        {
            placement = null!;
            part = null!;
            if (_document == null || _aimed.PartCell is not { } cell) return false;
            if (!ShipPartFootprints.Occupancy(_document.Design, _catalog).TryGetValue(cell, out var instanceId)) return false;
            return _document.Design.TryGetPart(instanceId, out placement) && _catalog.TryGet(placement.PartId, out part);
        }

        /// <summary>Where the selected part would go: the aimed cell, or the nearest spot that touches the ship.</summary>
        private bool Target(out ShipGridCell cell)
        {
            var part = SelectedPart;
            cell = _aimed.PlaceCell;
            return _document != null && part != null && _aimed.IsValid
                   && _snap.TrySnap(_document.Design, _catalog, part, Orientation, _aimed.PlaceCell, out cell, _designs.Frames);
        }

        private string Problems(ShipDesign design)
        {
            var validation = _validator.Validate(design, _catalog, _designs.Limits, _designs.Frames);
            var blocking = validation.Problems.Where(p => p.IsBlocking).ToList();
            return blocking.Count == 0 ? "ready to fly" : $"{blocking.Count} problem(s): {blocking[0].Message}";
        }

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
