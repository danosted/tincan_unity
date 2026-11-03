#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.UI;
using TinCan.Features.ShipDesigns;
using TinCan.Features.ShipPicker;
using UnityEngine;
using TinCan.Features.ShipDesigns.Tests;
using TinCan.Tests.Fakes;
using static TinCan.Features.ShipDesigns.Tests.ShipDesignTestParts;

namespace TinCan.Features.ShipPicker.Tests
{
    /// <summary>
    /// <see cref="ShipPickerUseCase"/> (temporary, .docs/plans/ship-picker-temporary.md): the menu lists the store's designs,
    /// a pick is refused on a client, for a design that does not load or cannot fly, and otherwise selects the design and
    /// gives it to every ship in play. The commands are driven through a real <see cref="MenuUseCase"/>.
    /// </summary>
    public class ShipPickerUseCaseTests
    {
        private sealed class RecordingShipDesigns : IShipDesigns
        {
            public ShipDesign? Selected { get; private set; }
            public int Applied { get; private set; }

            public void Select(ShipDesign design) => Selected = design;

            public bool TryApply(IShipDesignState state, ShipDesign design, out string? error)
            {
                error = null;
                state.ServerSetDesign(ShipDesignHash.Compute(design), ShipDesignBinaryCodec.Encode(design));
                Applied++;
                return true;
            }
        }

        private ShipDesignTestParts _parts = null!;
        private ShipDesignsConfig _config = null!;
        private FakeSessionNetworkService _network = null!;
        private FakeActorRegistry _actors = null!;
        private RecordingShipDesigns _designs = null!;
        private FileShipDesignStore _store = null!;
        private string _directory = null!;
        private ShipPickerUseCase _picker = null!;
        private ShipDesign _starter = null!;
        private readonly List<FakeShipDesignState> _ships = new();
        private readonly List<Object> _objects = new();
        private readonly ShipDesignJsonCodec _codec = new(ShipDesignLimits.Default);

        [SetUp]
        public void SetUp()
        {
            _parts = new ShipDesignTestParts();
            _config = ScriptableObject.CreateInstance<ShipDesignsConfig>();
            _objects.Add(_config);
            _network = new FakeSessionNetworkService { IsServer = true, IsClient = true };
            _actors = new FakeActorRegistry();
            _designs = new RecordingShipDesigns();
            _starter = Design((Helm, 0, 0, 0), (Beam, 2, 0, 0));
            _directory = Path.Combine(Path.GetTempPath(), "TinCanShipPicker_" + System.Guid.NewGuid().ToString("N"));
            _store = new FileShipDesignStore(_directory,
                new Dictionary<string, string> { ["Starter"] = _codec.Encode(_starter), ["Bare"] = _codec.Encode(ShipDesign.Empty("Bare")) }, _codec, 100_000);
            _picker = new ShipPickerUseCase(_store, _designs, new ShipDesignValidator(), _parts.Catalog, _config, _network, _actors, new FakeEventPublisher());
        }

        [TearDown]
        public void TearDown()
        {
            _parts.Dispose();
            foreach (var ship in _ships) Object.DestroyImmediate(((FakeAirshipView)ship.Ship!).GameObject);
            _ships.Clear();
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private FakeShipDesignState AddShip()
        {
            var state = new FakeShipDesignState(new FakeAirshipView());
            _ships.Add(state);
            _actors.Register(state);
            return state;
        }

        private MenuDefinition Keep(MenuDefinition menu)
        {
            _objects.Add(menu);
            return menu;
        }

        [Test]
        public void Menu_ListsTheStoresDesignsInOrder_BuiltInsTagged_ThenBack()
        {
            Assert.That(_store.TrySave("Mine", _starter, out var error), Is.True, error);

            var menu = Keep(_picker.BuildMenu());

            Assert.That(menu.Items.Select(i => i.Label), Is.EqualTo(new[] { "Bare (built in)", "Starter (built in)", "Mine", "Back" }));
            Assert.That(menu.Items.Take(3).All(i => i.Kind == MenuItemKind.Command && i.CommandId == PickShipMenuCommand.Id), Is.True);
            Assert.That(menu.Items.Last().Kind, Is.EqualTo(MenuItemKind.Back));
        }

        [Test]
        public void Menu_WithANote_ShowsItAboveBack()
        {
            var menu = Keep(_picker.BuildMenu("Only the host changes the ship."));

            var note = menu.Items[menu.Items.Count - 2];
            Assert.That(note.Kind, Is.EqualTo(MenuItemKind.Note));
            Assert.That(note.Label, Is.EqualTo("Only the host changes the ship."));
        }

        [Test]
        public void Pick_OnAClient_IsRefused_AndAppliesNothing()
        {
            _network.IsServer = false;
            var ship = AddShip();

            Assert.That(_picker.Pick("Starter", out var message), Is.False);

            Assert.That(message, Is.EqualTo("Only the host changes the ship."));
            Assert.That(_designs.Selected, Is.Null);
            Assert.That(ship.Writes, Is.Zero);
        }

        [Test]
        public void Pick_AnUnknownKey_IsRefusedWithTheStoresReason_AndLeavesTheShips()
        {
            var ship = AddShip();

            Assert.That(_picker.Pick("Nowhere", out var message), Is.False);

            Assert.That(message, Does.Contain("Nowhere"));
            Assert.That(_designs.Selected, Is.Null);
            Assert.That(ship.Writes, Is.Zero);
        }

        [Test]
        public void Pick_ADesignThatCannotFly_IsRefusedWithTheReason_AndLeavesTheShips()
        {
            var ship = AddShip();

            Assert.That(_picker.Pick("Bare", out var message), Is.False);

            Assert.That(message, Does.Contain("cannot fly"));
            Assert.That(_designs.Selected, Is.Null);
            Assert.That(ship.Writes, Is.Zero);
        }

        [Test]
        public void Pick_AValidKey_SelectsIt_AndGivesItToEveryShip()
        {
            var first = AddShip();
            var second = AddShip();

            Assert.That(_picker.Pick("Starter", out var message), Is.True, message);

            ulong hash = ShipDesignHash.Compute(_starter);
            Assert.That(_designs.Selected, Is.Not.Null);
            Assert.That(ShipDesignHash.Compute(_designs.Selected!), Is.EqualTo(hash));
            Assert.That(first.DesignHash, Is.EqualTo(hash));
            Assert.That(second.DesignHash, Is.EqualTo(hash));
            Assert.That(message, Does.Contain("2 ship(s)"));
        }

        [Test]
        public void Pick_Offline_SelectsIt_ForTheNextShip()
        {
            _network.IsActive = false;
            _network.IsServer = false;

            Assert.That(_picker.Pick("Starter", out var message), Is.True, message);

            Assert.That(_designs.Selected, Is.Not.Null);
            Assert.That(_designs.Applied, Is.Zero);
        }

        [Test]
        public void Commands_OpenThePicker_PickCloses_ARefusalShowsTheReason()
        {
            var commands = new IMenuCommand[] { new OpenShipPickerMenuCommand(_picker), new PickShipMenuCommand(_picker) };
            var menus = new MenuUseCase(new MenuCommandRegistry(commands));
            var main = Keep(MenuDefinition.Create("Main", "TinCan",
                new MenuItemDefinition { ItemId = "change-ship", Kind = MenuItemKind.Command, CommandId = OpenShipPickerMenuCommand.Id }));
            AddShip();

            menus.Open(main);
            menus.Invoke("change-ship");
            Assert.That(menus.Current!.MenuId, Is.EqualTo(ShipPickerUseCase.MenuId));

            menus.Invoke(ShipPickerUseCase.RowPrefix + "Bare");
            Assert.That(menus.Current!.MenuId, Is.EqualTo(ShipPickerUseCase.MenuId), "a refusal keeps the picker open");
            Assert.That(menus.Current.Items.Any(r => r.Kind == MenuItemKind.Note && r.Label.Contains("cannot fly")), Is.True);

            menus.Invoke(ShipPickerUseCase.RowPrefix + "Starter");
            Assert.That(menus.IsOpen, Is.False, "a pick goes back to the game");
        }
    }
}
