#nullable enable
using System.Collections.Generic;
using System.IO;
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
            _controls.Orbit = Action("Orbit");
            _controls.OrbitHold = Action("OrbitHold");
            _controls.Zoom = Action("Zoom");
            _controls.Place = Action("Place");
            _controls.Remove = Action("Remove");
            _controls.Rotate = Action("Rotate");
            _controls.NextPart = Action("NextPart");
            _controls.PreviousPart = Action("PreviousPart");
            _controls.Undo = Action("Undo");
            _controls.Redo = Action("Redo");
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
                new Dictionary<string, string> { ["Starter"] = _codec.Encode(Design((Helm, 0, 0, 0, 0), (Beam, -1, -1, 0, 0))) }, _codec, 100_000);
            _shipyard = new ShipyardUseCase(_config, _controls, _input, _switch, _stage, _parts.Catalog, _store, new ShipDesignEditProcessor(),
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

        /// <summary>The cursor over the empty build floor above this cell.</summary>
        private void PointAtFloor(int x, int z) => _stage.Ray = new ShipyardRay(new Vector3(x, 10f, z), Vector3.down);

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
            PointAtFloor(3, 2);

            _shipyard.Tick();
            Assert.That(_stage.Ghost!.Value.Cell, Is.EqualTo(new ShipGridCell(3, 0, 2)));
            Assert.That(_stage.Ghost!.Value.Valid, Is.True);

            Press(_controls.Place);

            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(3));
            Assert.That(_stage.Shown, Is.SameAs(_shipyard.Design));
            Assert.That(_hud.All[ShipyardUseCase.HudTitle], Does.Contain("*"), "unsaved");
        }

        [Test]
        public void TheGhost_TurnsRed_WhereThePartDoesNotFit()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            _stage.Ray = new ShipyardRay(Vector3.zero, Vector3.down, true, new Vector3(0f, 0.4f, 0.5f), Vector3.forward);

            _shipyard.Tick();

            // On the helm's side face at z 0.5: the cell in front, (0,0,1), is free...
            Assert.That(_stage.Ghost!.Value.Valid, Is.True);
            // ...but the beam turned to run along -Z from (0,0,1) would pass through the helm.
            _shipyard.Select(Beam);
            _shipyard.Rotate();
            _shipyard.Tick();
            Assert.That(_stage.Ghost!.Value.Valid, Is.False);
        }

        [Test]
        public void Remove_TakesThePartUnderTheCursor_AndUndoBringsItBack()
        {
            _shipyard.Open();
            // The cursor on top of the beam's middle cell, (0,-1,0).
            _stage.Ray = new ShipyardRay(new Vector3(1f, 5f, 0f), Vector3.down, true, new Vector3(1f, -0.5f, 0f), Vector3.up);

            Press(_controls.Remove);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(1));

            Press(_controls.Undo);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(2));

            Press(_controls.Redo);
            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(1));
        }

        [Test]
        public void Keys_CycleParts_AndTurnThem()
        {
            _shipyard.Open();
            var first = _shipyard.SelectedPart;

            Press(_controls.NextPart);
            Assert.That(_shipyard.SelectedPart, Is.Not.SameAs(first));
            Press(_controls.PreviousPart);
            Assert.That(_shipyard.SelectedPart, Is.SameAs(first));
            Press(_controls.PreviousPart);
            Assert.That(_shipyard.SelectedPart, Is.SameAs(_parts.Catalog.Parts[^1]), "wraps around");

            Press(_controls.Rotate, _controls.Rotate, _controls.Rotate);
            Assert.That(_shipyard.Orientation, Is.EqualTo(1), "one press per frame");
        }

        [Test]
        public void OrbitAndZoom_ReachTheStage_OrbitOnlyWhileHeld()
        {
            _shipyard.Open();
            _input.Vectors[_controls.Orbit!] = new Vector2(10f, 4f);
            _input.Axes[_controls.Zoom!] = 120f;

            _shipyard.Tick();
            Assert.That(_stage.Orbited, Is.EqualTo(Vector2.zero));
            Assert.That(_stage.Zoomed, Is.EqualTo(120f * _config.ZoomSpeed).Within(1e-4f));

            _input.Pressed.Add(_controls.OrbitHold!);
            _shipyard.Tick();
            Assert.That(_stage.Orbited, Is.EqualTo(new Vector2(10f, 4f) * _config.OrbitSpeed));
        }

        [Test]
        public void SaveThenLoad_RoundTripsTheDesign()
        {
            _shipyard.Open();
            _shipyard.Select(Block);
            _shipyard.PlaceAt(new ShipGridCell(2, -1, 0));
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

            Assert.That(_shipyard.Design!.Parts.Count, Is.EqualTo(26));
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
            _shipyard.RemoveAt(new ShipGridCell(0, 0, 0));

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
            Assert.That(_shipyard.Message, Is.EqualTo("Designs: Starter (built in)"));
        }
    }
}
