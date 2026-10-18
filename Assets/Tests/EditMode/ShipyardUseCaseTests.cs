#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Core.UI;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipyardUseCase"/>: open and leave, build with the Shipyard context's keys, save and load through the
    /// store, launch. The stage is a fake; the input is set directly.
    /// </summary>
    public class ShipyardUseCaseTests
    {
        private sealed class RecordingShipDesigns : IShipDesigns
        {
            public ShipDesign? Selected { get; private set; }
            public void Select(ShipDesign design) => Selected = design;

            public bool TryApply(IShipDesignState state, ShipDesign design, out string? error)
            {
                error = null;
                state.ServerSetDesign(ShipDesignHash.Compute(design), ShipDesignBinaryCodec.Encode(design));
                return true;
            }
        }

        private ShipDesignTestParts _parts = null!;
        private ShipyardConfig _config = null!;
        private ShipDesignsConfig _designs = null!;
        private ShipyardInputContext _controls = null!;
        private FakeInputReader _input = null!;
        private InputContextSwitch _switch = null!;
        private FakeShipyardStage _stage = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _mainMenu = null!;
        private FakeHudValues _hud = null!;
        private FakeNetworkService _network = null!;
        private RecordingShipDesigns _shipDesigns = null!;
        private FileShipDesignStore _store = null!;
        private string _directory = null!;
        private ShipyardUseCase _shipyard = null!;
        private readonly List<Object> _objects = new();
        private readonly ShipDesignJsonCodec _codec = new(ShipDesignLimits.Default);

        [SetUp]
        public void SetUp()
        {
            _parts = new ShipDesignTestParts();
            _config = Make<ShipyardConfig>();
            _config.Menu = Keep(MenuDefinition.Create("Shipyard", "Shipyard",
                new MenuItemDefinition { ItemId = ShipyardUseCase.NameField, Kind = MenuItemKind.TextField }));
            _designs = Make<ShipDesignsConfig>();
            _designs.DefaultDesign = "Starter";
            _controls = Make<ShipyardInputContext>();
            _controls.Point = Action("Point");
            _controls.Look = Action("Look");
            _controls.LookHold = Action("LookHold");
            _controls.Fly = Action("Fly");
            _controls.Rise = Action("Rise");
            _controls.Fast = Action("Fast");
            _controls.Zoom = Action("Zoom");
            _controls.Place = Action("Place");
            _controls.DeleteMode = Action("DeleteMode");
            _controls.Rotate = Action("Rotate");
            _controls.Mode = Action("Mode");
            _controls.Fill = Action("Fill");
            _controls.Height = Action("Height");
            _controls.Extrude = Action("Extrude");
            _controls.Railing = Action("Railing");
            _controls.Stairs = Action("Stairs");
            _controls.Undo = Action("Undo");
            _controls.Redo = Action("Redo");
            _controls.LevelUp = Action("LevelUp");
            _controls.LevelDown = Action("LevelDown");
            _input = new FakeInputReader();
            _switch = new InputContextSwitch();
            _stage = new FakeShipyardStage();
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[0]));
            _mainMenu = Keep(MenuDefinition.Create("Main", "TinCan"));
            _hud = new FakeHudValues();
            _network = new FakeNetworkService();
            _shipDesigns = new RecordingShipDesigns();
            _directory = Path.Combine(Path.GetTempPath(), "TinCanShipyard_" + System.Guid.NewGuid().ToString("N"));
            _store = new FileShipDesignStore(_directory,
                new Dictionary<string, string> { ["Starter"] = _codec.Encode(Design((Helm, 0, 0, 0), (Beam, 2, 0, 0))), ["Bare"] = _codec.Encode(ShipDesign.Empty("Bare")) }, _codec, 100_000);
            _shipyard = new ShipyardUseCase(_config, _controls, _input, new FakeInputBindings(), _switch, _stage, _parts.Catalog, _store, new ShipDesignEditProcessor(),
                new ShipDesignValidator(), new ShipStatsProcessor(), _designs, _shipDesigns, _hud, _network, new FakeActorRegistry());
        }

        [TearDown]
        public void TearDown()
        {
            _parts.Dispose();
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private T Make<T>() where T : ScriptableObject => Keep(ScriptableObject.CreateInstance<T>());

        private T Keep<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }

        private InputActionId Action(string name) => Keep(InputActionId.Create("Shipyard/" + name));

        /// <summary>One frame with these buttons just pressed.</summary>
        private void Press(params InputActionId?[] actions)
        {
            _input.Triggered.Clear();
            foreach (var action in actions) _input.Triggered.Add(action!);
            _shipyard.Tick();
            _input.Triggered.Clear();
        }

        /// <summary>The cursor straight down onto (x, z) metres: the starter's floor there.</summary>
        private void PointAtFloor(int x, int z) => _stage.Ray = new ShipyardRay(new Vector3(x, 10f, z), Vector3.down);

        /// <summary>The shipyard open on a design with no frame, for drawing one.</summary>
        private void OpenBare()
        {
            _shipyard.Open();
            _shipyard.Load("Bare");
        }

        [Test]
        public void Open_ShowsTheDefaultDesign_AndOpensTheShipyardContext()
        {
            _shipyard.Open();

            Assert.That(_shipyard.IsOpen && _stage.IsShown && _switch.IsOpen(_controls), Is.True);
            Assert.That(_stage.Shown!.Parts.Count, Is.EqualTo(2), "the Starter");
            Assert.That(_hud.All[ShipyardUseCase.HudTitle], Does.Contain("ready to fly"));
        }

        [Test]
        public void Exit_ClosesEverything_AndOfflineReturnsToTheMainMenu()
        {
            _shipyard.Open();
            var exit = new ShipyardExitMenuCommand(_shipyard, _network, _mainMenu);

            exit.Execute(new MenuContext(_menus, "Shipyard", "exit"));

            Assert.That(_shipyard.IsOpen || _stage.IsShown || _switch.IsOpen(_controls), Is.False);
            Assert.That(_hud.All.ContainsKey(ShipyardUseCase.HudTitle), Is.False);
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("Main"));
        }

        [Test]
        public void Place_PutsTheSelectedPartWhereTheCursorPoints_WithAGhostFirst()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            PointAtFloor(-2, 0);

            _shipyard.Tick();
            Assert.That(_stage.Ghost!.Value.Position, Is.EqualTo(new Vector3(-2f, -0.5f, 0f)), "standing on the floor where the cursor is");
            Assert.That(_stage.Ghost!.Value.Valid, Is.True);

            Press(_controls.Place);

            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(3));
            Assert.That(_shipyard.Design.Parts[^1].Anchor, Is.EqualTo(ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, -20, 0)));
            Assert.That(_stage.Shown, Is.SameAs(_shipyard.Design));
            Assert.That(_hud.All[ShipyardUseCase.HudTitle], Does.Contain("*"), "unsaved");
        }

        [Test]
        public void ASpotThatIsTaken_ShowsARedGhost_AndNothingIsPlaced()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            PointAtFloor(3, 0);

            Press(_controls.Place);

            Assert.That(_stage.Ghost!.Value.Valid, Is.False, "the beam is there");
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(2));
            Assert.That(_shipyard.Message, Does.Contain("in the way"));
        }

        [Test]
        public void OffTheFloor_NothingIsAimedAt_AndNothingIsPlaced()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            PointAtFloor(30, 30);

            Press(_controls.Place);

            Assert.That(_stage.Ghost, Is.Null);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(2));
            Assert.That(_shipyard.Message, Does.Contain("plank floor"));
            Assert.That(_shipyard.PlaceAt(ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, 300, 300)), Is.False, "through the API either");
        }

        [Test]
        public void ANodePart_GoesOnTheNodeUnderTheCursor()
        {
            _shipyard.Open();
            _shipyard.Select(Post);
            // Node 1, the floor's corner at (-10, -0.5, -10).
            PointAtFloor(-10, -10);

            Press(_controls.Place);

            Assert.That(_shipyard.Design!.Parts[^1].Anchor, Is.EqualTo(ShipPartAnchor.OnNode(1)));
            Assert.That(_hud.All[ShipyardUseCase.HudPart], Does.Contain("on a node"));
        }

        [Test]
        public void DeleteMode_HighlightsThePartUnderTheCursor_AndAClickDeletesIt_UndoBringsItBack()
        {
            _shipyard.Open();
            // The cursor on top of the beam (x 2 to 4 m, its top at 0.5 m).
            _stage.Ray = new ShipyardRay(new Vector3(3f, 5f, 0f), Vector3.down, true, new Vector3(3f, 0.5f, 0f), Vector3.up);

            Press(_controls.DeleteMode);
            Assert.That(_shipyard.DeleteMode, Is.True);
            Assert.That(_stage.Highlighted!.Value.Part.PartId, Is.EqualTo(Beam));
            Assert.That(_stage.Ghost, Is.Null, "no placing ghost in delete mode");
            Assert.That(_hud.All[ShipyardUseCase.HudPart], Does.Contain("DELETE MODE"));

            Press(_controls.Place);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(1));

            Press(_controls.Undo);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(2));

            Press(_controls.Redo);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(1));

            Press(_controls.DeleteMode);
            Assert.That((_shipyard.DeleteMode, _stage.Highlighted.HasValue), Is.EqualTo((false, false)), "X again: back to building");
        }

        [Test]
        public void ThePartsPanel_ListsEveryPart_AClickSelectsOne_AndRTurnsIt()
        {
            _shipyard.Open();

            Assert.That(_stage.Panel!.Mode, Is.EqualTo(ShipyardMode.Parts));
            Assert.That(_stage.Panel.Parts.Count, Is.EqualTo(_parts.Catalog.Parts.Count));

            _stage.ClickPart(Beam);
            Assert.That(_shipyard.SelectedPart!.PartId, Is.EqualTo(Beam));
            Assert.That(_stage.Panel.Parts.Single(p => p.Selected).PartId, Is.EqualTo(Beam));

            Press(_controls.Rotate, _controls.Rotate, _controls.Rotate);
            Assert.That(_shipyard.Orientation, Is.EqualTo(1), "one press per frame");
        }

        [Test]
        public void TheControlsPanel_ShowsTheKeysFromTheBindings_ForTheMode()
        {
            _shipyard.Open();
            Assert.That(_stage.Panel!.Controls, Has.Some.Matches<ShipyardControlRow>(r => r.Keys == "Shipyard/Fly" && r.Action == "Fly"));
            Assert.That(_stage.Panel.Controls, Has.Some.Matches<ShipyardControlRow>(r => r.Action == "Turn the part"));

            Press(_controls.Mode);

            Assert.That(_stage.Panel.Mode, Is.EqualTo(ShipyardMode.Frame));
            Assert.That(_stage.Panel.Parts, Is.Empty);
            Assert.That(_stage.Panel.Controls, Has.Some.Matches<ShipyardControlRow>(r => r.Keys == "Shipyard/Fill" && r.Action == "Plank or open a face"));
        }

        [Test]
        public void AClickOverAPanel_IsThePanels_NotTheBuilds()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            PointAtFloor(1, 0);
            _stage.OverPanel = true;

            Press(_controls.Place);

            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheCamera_FramesTheShip_FliesWithTheKeys_AndLooksOnlyWhileHeld()
        {
            _shipyard.Open();
            var start = _stage.CameraPose!.Value;
            Assert.That(Vector3.Dot(start.Forward, (Vector3.zero - start.Position).normalized), Is.GreaterThan(0.95f), "it looks at the ship");

            _input.Vectors[_controls.Look!] = new Vector2(10f, 0f);
            _input.Vectors[_controls.Fly!] = new Vector2(0f, 1f);
            _shipyard.Tick(0.5f);
            var flown = _stage.CameraPose!.Value;
            Assert.That(flown.Yaw, Is.EqualTo(start.Yaw), "no look without the button");
            Assert.That(Vector3.Distance(flown.Position, start.Position + start.Forward * (_config.FlySpeed * 0.5f)), Is.LessThan(1e-3f));

            _input.Pressed.Add(_controls.LookHold!);
            _shipyard.Tick(0f);
            Assert.That(_stage.Looking, Is.True);
            Assert.That(_stage.CameraPose!.Value.Yaw, Is.EqualTo(start.Yaw + 10f * _config.LookSpeed).Within(1e-4f));

            _input.Pressed.Remove(_controls.LookHold!);
            _shipyard.Tick(0f);
            Assert.That(_stage.Looking, Is.False);
        }

        [Test]
        public void FrameMode_ClickingFourCornersAndBack_DrawsAPlankedFace_UndoneInOneStep()
        {
            OpenBare();
            Press(_controls.Mode);
            foreach (var corner in new[] { new Vector3(3f, 0f, -1f), new Vector3(3f, 0f, 1f), new Vector3(5f, 0f, 1f), new Vector3(5f, 0f, -1f) })
            {
                Assert.That(_shipyard.FrameClickAt(corner), Is.True, _shipyard.Message);
            }

            Assert.That(_shipyard.FrameClickAt(new Vector3(3f, 0f, -1f)), Is.True, "back to the first corner closes the loop");

            var frame = _shipyard.Design!.Frame;
            Assert.That((frame.Nodes.Count, frame.Edges.Count, frame.Faces.Count), Is.EqualTo((4, 4, 1)));
            Assert.That(frame.Faces[0].Fill, Is.EqualTo(ShipFrameFill.Planks));
            Assert.That(frame.Base, Is.EqualTo(-5), "the first story's floor is the working level's");
            Assert.That(_stage.Marks!.Value.Selected, Is.EqualTo(frame.Nodes[0].Id));

            Press(_controls.Undo);
            Assert.That((_shipyard.Design!.Frame.Edges.Count, _shipyard.Design.Frame.Faces.Count), Is.EqualTo((3, 0)), "the closing edge and its face go together");
        }

        [Test]
        public void PartsMode_OnADrawnDeck_SetsAPartDownInHalfMetreSteps()
        {
            OpenBare();
            Press(_controls.Mode);
            foreach (var corner in new[] { new Vector3(1.5f, 0f, -1.5f), new Vector3(1.5f, 0f, 1.5f), new Vector3(4.5f, 0f, 1.5f), new Vector3(4.5f, 0f, -1.5f), new Vector3(1.5f, 0f, -1.5f) })
            {
                _shipyard.FrameClickAt(corner);
            }

            Press(_controls.Mode);
            _shipyard.Select(Block);
            Assert.That(_shipyard.PlaceDownAt(new Vector3(3.2f, 0f, 0.7f)), Is.True, _shipyard.Message);

            var placed = _shipyard.Design!.Parts[^1];
            Assert.That((placed.Anchor.X, placed.Anchor.Z), Is.EqualTo((30, 5)), "half-metre steps");
            Assert.That(placed.Anchor.Element, Is.EqualTo(_shipyard.Design.Frame.Faces.Single().Id), "on the drawn face");
            Assert.That(_shipyard.PlaceDownAt(new Vector3(3.4f, 0f, 0.9f)), Is.False, "the same spot is taken");
        }

        [Test]
        public void FrameMode_DraggingANodeMovesIt_AltDraggingRaisesIt_AndFillOpensAFace()
        {
            OpenBare();
            Press(_controls.Mode);
            foreach (var corner in new[] { new Vector3(3f, 0f, -1f), new Vector3(3f, 0f, 1f), new Vector3(5f, 0f, 1f), new Vector3(5f, 0f, -1f), new Vector3(3f, 0f, -1f) })
            {
                _shipyard.FrameClickAt(corner);
            }

            int corner0 = _shipyard.Design!.Frame.Nodes[0].Id;
            _stage.Ray = new ShipyardRay(new Vector3(3f, 10f, -1f), Vector3.down);
            _input.Vectors[_controls.Point!] = new Vector2(50f, 50f);
            Press(_controls.Place);
            _input.Pressed.Add(_controls.Place!);
            _stage.Ray = new ShipyardRay(new Vector3(2f, 10f, -1.5f), Vector3.down);
            _input.Vectors[_controls.Point!] = new Vector2(30f, 40f);
            _shipyard.Tick(0f);
            Assert.That(_stage.Shown!.Frame.Nodes[0].X, Is.EqualTo(20), "shown where it is dragged");
            Assert.That(_shipyard.Design!.Frame.Nodes[0].X, Is.EqualTo(30), "not yet committed");
            _input.Pressed.Remove(_controls.Place!);
            _shipyard.Tick(0f);

            _shipyard.Design!.Frame.TryGetNode(corner0, out var moved);
            Assert.That((moved.X, moved.Z), Is.EqualTo((20, -15)));
            Assert.That(_shipyard.SelectedNode, Is.EqualTo(corner0));

            // Alt held at the start of a drag: moving the mouse up raises the node, one unit per HeightPixelsPerUnit.
            _stage.Ray = new ShipyardRay(new Vector3(2f, 10f, -1.5f), Vector3.down);
            _input.Pressed.Add(_controls.Height!);
            _input.Vectors[_controls.Point!] = new Vector2(100f, 100f);
            Press(_controls.Place);
            _input.Pressed.Add(_controls.Place!);
            _input.Vectors[_controls.Point!] = new Vector2(100f, 100f + 3f * _config.HeightPixelsPerUnit);
            _shipyard.Tick(0f);
            _input.Pressed.Remove(_controls.Place!);
            _input.Pressed.Remove(_controls.Height!);
            _shipyard.Tick(0f);
            _shipyard.Design!.Frame.TryGetNode(corner0, out var raised);
            Assert.That((raised.X, raised.Y, raised.Z), Is.EqualTo((20, 3, -15)), "raised 30 cm, not moved");

            _stage.Ray = new ShipyardRay(new Vector3(4f, 10f, 0f), Vector3.down);
            Press(_controls.Fill);
            Assert.That(_shipyard.Design!.Frame.Faces[0].Fill, Is.EqualTo(ShipFrameFill.Open));
        }

        [Test]
        public void FrameMode_ClickingANode_SelectsIt_WithoutMovingIt_EvenOffCentre()
        {
            OpenBare();
            Press(_controls.Mode);
            _shipyard.FrameClickAt(new Vector3(3f, 0f, -1f));
            _shipyard.FrameClickAt(new Vector3(3f, 0f, 1f));
            int first = _shipyard.Design!.Frame.Nodes[0].Id;
            var before = _shipyard.Design.Frame.Position(first);

            // Pressed 0.3 m off the node's centre, the mouse jittering by two pixels before the release.
            _stage.Ray = new ShipyardRay(new Vector3(3.3f, 10f, -1f), Vector3.down);
            _input.Vectors[_controls.Point!] = new Vector2(50f, 50f);
            Press(_controls.Place);
            _input.Pressed.Add(_controls.Place!);
            _input.Vectors[_controls.Point!] = new Vector2(52f, 50f);
            _stage.Ray = new ShipyardRay(new Vector3(3.6f, 10f, -1f), Vector3.down);
            _shipyard.Tick(0f);
            _input.Pressed.Remove(_controls.Place!);
            _shipyard.Tick(0f);

            Assert.That(_shipyard.Design!.Frame.Position(first), Is.EqualTo(before));
            Assert.That(_shipyard.SelectedNode, Is.EqualTo(first));
        }

        [Test]
        public void FrameMode_ARightClickDeselects_ButLookingAroundDoesNot()
        {
            OpenBare();
            Press(_controls.Mode);
            _shipyard.FrameClickAt(new Vector3(3f, 0f, -1f));
            int node = _shipyard.SelectedNode!.Value;

            _input.Pressed.Add(_controls.LookHold!);
            _input.Vectors[_controls.Look!] = new Vector2(40f, 0f);
            _shipyard.Tick(0f);
            _input.Pressed.Remove(_controls.LookHold!);
            _shipyard.Tick(0f);
            Assert.That(_shipyard.SelectedNode, Is.EqualTo(node), "a right drag only looks around");

            _input.Vectors[_controls.Look!] = Vector2.zero;
            _input.Pressed.Add(_controls.LookHold!);
            _shipyard.Tick(0f);
            _input.Pressed.Remove(_controls.LookHold!);
            _shipyard.Tick(0f);
            Assert.That(_shipyard.SelectedNode, Is.Null);
        }

        [Test]
        public void SaveThenLoad_RoundTripsTheDesign()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            _shipyard.PlaceAt(ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, -20, 0));
            var built = ShipDesignHash.Compute(_shipyard.Design!.WithName("Sky Whale"));

            Assert.That(_shipyard.Save("Sky Whale"), Is.True, _shipyard.Message);
            _shipyard.New();
            Assert.That(_shipyard.Load("Sky Whale"), Is.True, _shipyard.Message);

            Assert.That(ShipDesignHash.Compute(_shipyard.Design!), Is.EqualTo(built));
            Assert.That(_hud.All[ShipyardUseCase.HudTitle], Does.Not.Contain("*"));
            Assert.That(_shipyard.Load("Nope"), Is.False);
            Assert.That(_shipyard.Message, Does.Contain("no design"));
        }

        [Test]
        public void New_IsAHelmOnADeck_ReadyToFly()
        {
            _shipyard.Open();

            _shipyard.New();

            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(2), "the helm and the cairn (the test parts have no engine)");
            Assert.That(new ShipDesignValidator().Validate(_shipyard.Design, _parts.Catalog, ShipDesignLimits.Default).IsValid, Is.True);
        }

        [Test]
        public void Launch_Offline_SelectsTheDesign_AndHosts()
        {
            _shipyard.Open();

            Assert.That(_shipyard.Launch(), Is.True, _shipyard.Message);

            Assert.That(_shipDesigns.Selected, Is.SameAs(_shipyard.Design));
            Assert.That(_network.StartHostCalls, Is.EqualTo(1));
            Assert.That(_shipyard.IsOpen, Is.False);
        }

        [Test]
        public void Launch_IsRefused_WhileTheDesignCannotFly()
        {
            _shipyard.Open();
            _shipyard.RemoveAt(new Vector3(0f, 0f, 0f));

            Assert.That(_shipyard.Launch(), Is.False);

            Assert.That(_shipyard.Message, Does.Contain("cannot fly"));
            Assert.That(_network.StartHostCalls, Is.Zero);
            Assert.That(_shipyard.IsOpen, Is.True);
        }

        [Test]
        public void Cancel_OpensTheShipyardMenu_WithTheDesignsName()
        {
            var handler = new ShipyardMenuInputHandler(_shipyard, _menus, _config);
            var command = Make<OpenShipyardMenuCommand>();
            Assert.That(handler.TryHandle(command), Is.False, "closed: the press passes on");

            _shipyard.Open();
            Assert.That(handler.TryHandle(command), Is.True);

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("Shipyard"));
            Assert.That(_menus.GetValue(ShipyardUseCase.NameField), Is.EqualTo("Test"));
            Assert.That(_shipyard.Message, Is.EqualTo("Designs: Bare (built in), Starter (built in)"));
        }
    }
}
