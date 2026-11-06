#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Networking;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for hosting and LAN discovery (.docs/plans/hosting-and-joining.md, S2). Commands
    /// <c>BrowseLan</c> / <c>StopBrowsingLan</c> start and stop this peer's browsing. Probes: <c>LanServing</c> passes
    /// on the peer that serves the game (it answers discovery); <c>LanGameFound "port"</c> passes once this peer's list
    /// holds a game of this version on that game port (7777 when none is given).
    /// </summary>
    public sealed class SessionScenarioLibrary : IScenarioLibrary
    {
        private const ushort DefaultGamePort = 7777;

        private readonly ISessionService _session;
        private readonly ILanDiscovery _discovery;

        public SessionScenarioLibrary(ISessionService session, ILanDiscovery discovery)
        {
            _session = session;
            _discovery = discovery;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("BrowseLan", _ => Browse(true)),
            new ScenarioCommand("StopBrowsingLan", _ => Browse(false))
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("LanServing", _ => CheckServing()),
            new ScenarioProbe("LanGameFound", CheckFound)
        };

        private ScenarioCheck Browse(bool browsing)
        {
            _discovery.Browse(browsing);
            return ScenarioCheck.Pass(browsing ? "browsing" : "stopped browsing");
        }

        private ScenarioCheck CheckServing() =>
            _session.Hosted is { } hosted
                ? ScenarioCheck.Pass($"serving '{hosted.Name}', {_session.Players.Count}/{hosted.MaxPlayers}")
                : ScenarioCheck.Fail("this peer serves no game");

        private ScenarioCheck CheckFound(string portText)
        {
            ushort port = ushort.TryParse(portText, out var parsed) && parsed != 0 ? parsed : DefaultGamePort;
            string seen = _discovery.Games.Count == 0
                ? "none"
                : string.Join(", ", _discovery.Games.Select(g => $"'{g.Name}' at {g.Key} v{g.Version}"));

            if (!_discovery.Browsing) return ScenarioCheck.Fail("not browsing");
            return _discovery.Games.Any(g => g.Port == port && g.SameVersion)
                ? ScenarioCheck.Pass($"found {seen}")
                : ScenarioCheck.Fail($"no game of this version on port {port} yet (seen: {seen})");
        }
    }
}
