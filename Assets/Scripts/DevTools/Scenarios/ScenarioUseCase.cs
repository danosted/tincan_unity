#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.HumanoidMovement;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Runs the <c>-scenario</c> on this peer once the local player exists, writes this peer's report, and on the
    /// host waits for the clients' reports, writes <c>latest-summary.json</c> and leaves Play mode. So a whole run is:
    /// trigger the menu, wait for the summary file, read it. See .docs/NETWORK_TEST_HARNESS.md.
    /// </summary>
    public sealed class ScenarioUseCase : ITickable, IDisposable, IScenarioWorld
    {
        private const string LogSource = "Scenario";
        private const float PeerReportWait = 20f;
        private const float ExitDelay = 1f;

        private enum Phase
        {
            WaitingForSession,
            Running,
            AwaitingPeers,
            Exiting,
            Done
        }

        private readonly HarnessOptions _options;
        private readonly ScenarioEntry _entry;
        private readonly ScenarioTimeline _timeline;
        private readonly IScriptedInput _input;
        private readonly INetworkService _network;
        private readonly IActorRegistry _registry;
        private readonly IReadOnlyList<IScenarioLibrary> _libraries;
        private readonly IEventPublisher _events;
        private readonly Dictionary<string, ScenarioCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScenarioProbe> _probes = new(StringComparer.OrdinalIgnoreCase);

        private Phase _phase = Phase.WaitingForSession;
        private ScenarioRole _role;
        private ScenarioRunner? _runner;
        private DateTime _startedUtc;
        private float _phaseStart;
        private string _directory = string.Empty;
        private string _captureDirectory = string.Empty;
        private int _captureCount;

        public ScenarioUseCase(
            HarnessOptions options,
            ScenarioEntry entry,
            ScenarioTimeline timeline,
            IScriptedInput input,
            INetworkService network,
            IActorRegistry registry,
            IReadOnlyList<IScenarioLibrary> libraries,
            IEventPublisher events)
        {
            _options = options;
            _entry = entry;
            _timeline = timeline;
            _input = input;
            _network = network;
            _registry = registry;
            _libraries = libraries;
            _events = events;
        }

        private Scenario Scenario => _entry.Scenario;
        private float Now => Time.unscaledTime;
        private string RoleName => _network.IsHost ? "host" : _network.IsServer ? "server" : $"client{_network.LocalClientId}";
        private string Mode => _options.ScenarioSolo ? "solo" : "host+client";

        public void Tick()
        {
            switch (_phase)
            {
                case Phase.WaitingForSession:
                    TryStart();
                    break;
                case Phase.Running:
                    Run();
                    break;
                case Phase.AwaitingPeers:
                    AwaitPeers();
                    break;
                case Phase.Exiting when Now - _phaseStart >= ExitDelay:
                    ExitPlayMode();
                    _phase = Phase.Done;
                    break;
            }
        }

        public void Dispose()
        {
            if (_phase != Phase.Running || _runner == null) return;

            _runner.Abort("play mode ended before the scenario finished");
            WriteReport();
        }

        private void TryStart()
        {
            if (!_network.IsActive || _registry.GetLocalPlayerActor<IHumanoidCharacterView>() == null) return;

            _role = _options.ScenarioSolo ? ScenarioRole.Solo : _network.IsServer ? ScenarioRole.Server : ScenarioRole.Subject;
            IndexLibraries();

            _directory = ScenarioPaths.ScenarioDirectory(Scenario.Name);
            _captureDirectory = ScenarioPaths.CaptureDirectory(_directory, RoleName);
            PrepareDirectories();

            _startedUtc = DateTime.UtcNow;
            _timeline.Start(Now);
            _timeline.Add("start", Scenario.Name, true, $"role {_role} ({RoleName}), mode {Mode}, netsim {_options.NetworkPreset ?? "none"}");
            _runner = new ScenarioRunner(Scenario.StepsFor(_role), this, _timeline);
            _phase = Phase.Running;
            _events.LogInfo(LogSource, $"'{Scenario.Name}' started as {_role} ({RoleName}): {Scenario.Description}");
        }

        private void Run()
        {
            _timeline.SetTime(Now);
            if (_timeline.Elapsed > Scenario.TimeoutSeconds) _runner!.Abort($"scenario timeout ({Scenario.TimeoutSeconds:0} s)");
            _runner!.Tick(_timeline.Elapsed);
            if (!_runner.IsDone) return;

            WriteReport();
            _phaseStart = Now;
            _phase = _role == ScenarioRole.Subject ? Phase.Done : Phase.AwaitingPeers;
        }

        private void AwaitPeers()
        {
            var peers = ExpectedPeerRoles();
            bool allReported = peers.All(IsFreshReport);
            if (!allReported && Now - _phaseStart < PeerReportWait) return;

            WriteSummary(peers);
            _phaseStart = Now;
            _phase = Phase.Exiting;
        }

        private IReadOnlyList<string> ExpectedPeerRoles()
        {
            if (_role == ScenarioRole.Solo) return Array.Empty<string>();

            var clients = _registry.GetActors<IHumanoidCharacterView>()
                .Select(player => ((IPossessable)player).OwnerId)
                .Where(owner => owner is { } id && id != _network.LocalClientId)
                .Select(owner => $"client{owner}")
                .Distinct()
                .ToList();

            // A host + client run with no client connected still expects one, so the summary shows it as missing.
            return clients.Count > 0 ? clients : new List<string> { "client1" };
        }

        private bool IsFreshReport(string role)
        {
            string path = Path.Combine(_directory, ScenarioPaths.ReportFile(role));
            return File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _startedUtc;
        }

        private void IndexLibraries()
        {
            foreach (var library in _libraries)
            {
                foreach (var command in library.Commands) _commands[command.Name] = command;
                foreach (var probe in library.Probes) _probes[probe.Name] = probe;
            }
        }

        private void PrepareDirectories()
        {
            try
            {
                Directory.CreateDirectory(_captureDirectory);
                foreach (var old in Directory.GetFiles(_captureDirectory, "*.png")) File.Delete(old);
                if (_role != ScenarioRole.Subject) File.Delete(Path.Combine(_directory, ScenarioPaths.SummaryFile));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _events.LogWarning(LogSource, $"Could not prepare {_directory}: {exception.Message}");
            }
        }

        private void WriteReport()
        {
            var runner = _runner!;
            var report = new ScenarioReport
            {
                scenario = Scenario.Name,
                role = RoleName,
                mode = Mode,
                preset = _options.NetworkPreset ?? "none",
                startedUtc = _startedUtc.ToString("o"),
                durationS = (float)Math.Round(_timeline.Elapsed, 2),
                status = runner.Status.ToString(),
                passed = runner.Status == ScenarioStatus.Passed,
                failures = runner.Failures.ToList(),
                expectations = runner.Expectations.ToList(),
                checkpoints = runner.Captures.ToList(),
                timeline = _timeline.Entries.ToList()
            };

            WriteJson(ScenarioPaths.ReportFile(RoleName), report);
            _events.LogInfo(LogSource, $"'{Scenario.Name}' {report.status} on {RoleName} in {report.durationS:0.0} s" +
                                       (report.passed ? "." : $": {string.Join("; ", report.failures)}"));
        }

        private void WriteSummary(IReadOnlyList<string> peers)
        {
            var summary = new ScenarioSummary
            {
                scenario = Scenario.Name,
                mode = Mode,
                finishedUtc = DateTime.UtcNow.ToString("o")
            };

            summary.peers.Add(Verdict(RoleName));
            foreach (var peer in peers) summary.peers.Add(Verdict(peer));
            summary.passed = summary.peers.All(peer => peer.passed);

            WriteJson(ScenarioPaths.SummaryFile, summary);
            _events.LogInfo(LogSource, "SUMMARY " + JsonUtility.ToJson(summary, false));
        }

        private ScenarioPeerVerdict Verdict(string role)
        {
            string path = Path.Combine(_directory, ScenarioPaths.ReportFile(role)).Replace('\\', '/');
            if (!IsFreshReport(role))
            {
                return new ScenarioPeerVerdict
                {
                    role = role,
                    status = "Missing",
                    passed = false,
                    failures = new List<string> { $"no report from {role} within {PeerReportWait:0} s of the host finishing" },
                    report = path
                };
            }

            var report = JsonUtility.FromJson<ScenarioReport>(File.ReadAllText(path));
            return new ScenarioPeerVerdict
            {
                role = role,
                status = report.status,
                passed = report.passed,
                failures = report.failures,
                report = path
            };
        }

        private void WriteJson(string fileName, object value)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(Path.Combine(_directory, fileName), JsonUtility.ToJson(value, true));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _events.LogWarning(LogSource, $"Could not write {fileName}: {exception.Message}");
            }
        }

        private static void ExitPlayMode()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        void IScenarioWorld.Press(string action) => _input.Press(action);
        void IScenarioWorld.Release(string action) => _input.Release(action);
        void IScenarioWorld.Tap(string action) => _input.Tap(action);

        ScenarioCheck IScenarioWorld.Execute(string command, string argument) =>
            _commands.TryGetValue(command, out var found)
                ? found.Execute(argument)
                : ScenarioCheck.Fail($"unknown command '{command}'. Known: {string.Join(", ", _commands.Keys)}");

        ScenarioCheck IScenarioWorld.Evaluate(string probe, string argument) =>
            _probes.TryGetValue(probe, out var found)
                ? found.Evaluate(argument)
                : ScenarioCheck.Fail($"unknown probe '{probe}'. Known: {string.Join(", ", _probes.Keys)}");

        string IScenarioWorld.Capture(string checkpoint)
        {
            string path = Path.Combine(_captureDirectory, $"{++_captureCount:00}-{checkpoint}.png").Replace('\\', '/');
            ScreenCapture.CaptureScreenshot(path);
            return path;
        }
    }
}
