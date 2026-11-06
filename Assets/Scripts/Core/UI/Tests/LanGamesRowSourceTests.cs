#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Core.UI;
using TinCan.Core.UI.Commands;
using TinCan.Tests.Fakes;

namespace TinCan.Core.UI.Tests
{
    public class LanGamesRowSourceTests
    {
        private FakeLanDiscovery _discovery = null!;
        private FakeSessionService _session = null!;
        private LanGamesRowSource _source = null!;
        private MenuUseCase _menus = null!;

        private static DiscoveredGame Game(string name, int players = 1, int max = 8, bool sameVersion = true, string address = "192.168.1.20") =>
            new(name, address, 7777, players, max, sameVersion ? "0.4.2" : "0.3.9", sameVersion, false);

        [SetUp]
        public void SetUp()
        {
            _discovery = new FakeLanDiscovery();
            _session = new FakeSessionService();
            var join = new JoinGameMenuCommand(_session);
            _source = new LanGamesRowSource(_discovery, join);
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { join }));
        }

        [TearDown]
        public void TearDown() => _source.Dispose();

        [Test]
        public void Browses_OnlyWhileShown()
        {
            _source.SetShown(true);
            Assert.That(_discovery.Browsing, Is.True);

            _source.SetShown(false);
            Assert.That(_discovery.Browsing, Is.False);
        }

        [Test]
        public void OneRow_PerGame()
        {
            _discovery.SetGames(Game("Ada's game", players: 2), Game("Bo's game", address: "192.168.1.21"));

            Assert.That(_source.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Ada's game  2/8", "Bo's game  1/8" }));
            Assert.That(_source.Rows.All(r => r.Enabled), Is.True);
        }

        [Test]
        public void AnotherVersion_OrAFullGame_CannotBePicked()
        {
            _discovery.SetGames(Game("Old game", sameVersion: false), Game("Busy game", players: 8, address: "192.168.1.21"));

            Assert.That(_source.Rows[0].Label, Is.EqualTo("Old game  1/8  (version 0.3.9)"));
            Assert.That(_source.Rows[1].Label, Is.EqualTo("Busy game  8/8  (full)"));
            Assert.That(_source.Rows.Any(r => r.Enabled), Is.False);
        }

        [Test]
        public void PickingAGame_JoinsIt()
        {
            _discovery.SetGames(Game("Ada's game"));

            _source.Invoke(_source.Rows[0].Key, new MenuContext(_menus, "join", "join"));

            Assert.That(_session.LastJoin!.Value.ToString(), Is.EqualTo("192.168.1.20:7777"));
        }

        [Test]
        public void WhileEmpty_SaysItIsLooking()
        {
            Assert.That(_source.Rows, Is.Empty);
            Assert.That(_source.EmptyLabel, Is.EqualTo(LanGamesRowSource.LookingLabel));
        }
    }
}
