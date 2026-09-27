#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using TinCan.Core.Domain.Cues;
using TinCan.Features.Abilities.Cues;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Probes for gameplay cues on this peer, fed by <see cref="IGameplayCueFeed"/> from the moment the scenario starts:
    /// <list type="bullet">
    /// <item><c>CueCount "Cue.Tag:Kind:n"</c>: exactly n Execute / Active / Removed events of that cue here, e.g.
    /// "Cue.Ship.Part.Break:Execute:1". Catches both a missing and a doubled cue.</item>
    /// <item><c>CueActive</c> / <c>CueInactive "Cue.Tag"</c>: whether any actor holds that state cue here now.</item>
    /// </list>
    /// </summary>
    public sealed class GameplayCueScenarioLibrary : IScenarioLibrary, IDisposable
    {
        private readonly IGameplayCueFeed? _feed;
        private readonly Dictionary<(string Cue, GameplayCueEventKind Kind), int> _counts = new();
        private readonly HashSet<(Guid Actor, string Cue)> _active = new();

        public GameplayCueScenarioLibrary(IObjectResolver resolver)
        {
            _feed = resolver.TryResolve<IGameplayCueFeed>(out var feed) ? feed : null;
            if (_feed != null) _feed.CueHandled += Record;
        }

        public void Dispose()
        {
            if (_feed != null) _feed.CueHandled -= Record;
        }

        public IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("CueCount", CheckCount),
            new ScenarioProbe("CueActive", cue => CheckActive(cue, expected: true)),
            new ScenarioProbe("CueInactive", cue => CheckActive(cue, expected: false))
        };

        private void Record(GameplayCueEventKind kind, GameplayCueEvent cueEvent)
        {
            string cue = cueEvent.Cue.name;
            _counts[(cue, kind)] = Count(cue, kind) + 1;

            var key = (cueEvent.Controller.Id, cue);
            if (kind == GameplayCueEventKind.Active) _active.Add(key);
            else if (kind == GameplayCueEventKind.Removed) _active.Remove(key);
        }

        private int Count(string cue, GameplayCueEventKind kind) => _counts.TryGetValue((cue, kind), out int count) ? count : 0;

        /// <summary>Argument "Cue.Tag:Kind:n".</summary>
        private ScenarioCheck CheckCount(string argument)
        {
            if (_feed == null) return ScenarioCheck.Fail("no IGameplayCueFeed (GameplayCuesFeatureInstaller missing)");

            var parts = argument.Split(':');
            if (parts.Length != 3 || !Enum.TryParse(parts[1], true, out GameplayCueEventKind kind) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int expected))
            {
                return ScenarioCheck.Fail($"expected 'Cue.Tag:Execute|Active|Removed:n', got '{argument}'");
            }

            int count = Count(parts[0], kind);
            string detail = $"{parts[0]} {kind} x{count}";
            return count == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail($"{detail}, expected {expected}");
        }

        private ScenarioCheck CheckActive(string cue, bool expected)
        {
            if (_feed == null) return ScenarioCheck.Fail("no IGameplayCueFeed (GameplayCuesFeatureInstaller missing)");

            int holders = 0;
            foreach (var (_, activeCue) in _active)
            {
                if (activeCue == cue) holders++;
            }
            string detail = $"{cue} active on {holders} actor(s)";
            return (holders > 0) == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
