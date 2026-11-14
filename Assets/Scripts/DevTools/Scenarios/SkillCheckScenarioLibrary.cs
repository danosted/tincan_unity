#nullable enable
using System;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.SkillChecks;
using TinCan.Features.SkillChecks.Challenges;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for skill checks and challenges (hazards-and-boons.md, B3, B4a). Probes: this peer runs a check
    /// (<c>SkillCheckRunning</c>); the needle is in the Perfect part and still heading for its middle, so a press on the
    /// next frame or two lands there (<c>SkillCheckInPerfect</c>); and, on the server, how many checks ended with a grade
    /// (<c>SkillChecksGraded "Perfect:1"</c>, exactly). Commands, for the button combo, through real input: tap the next
    /// step (<c>TapComboNext</c>), or a wrong one, which restarts the row (<c>TapComboWrong</c>). Challenges: start one by
    /// its asset's name (<c>StartChallenge "CH_EngineRelay"</c>, from the encounters' list); the host's own player does a
    /// lit step without input (<c>FinishChallengeStep "SC_EngineBoost:Perfect"</c>, server); a station waits lit on this
    /// peer (<c>ChallengeLit "Boiler"</c>, by its station name); how many challenges ended with a grade
    /// (<c>ChallengesEnded "Perfect:1"</c>, server).
    /// </summary>
    public sealed class SkillCheckScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly SkillCheckHostRegistry _hosts;
        private readonly ISkillCheckRunner _runner;
        private readonly ISkillChecks _checks;
        private readonly SkillChallengeUseCase _challenges;
        private readonly SkillChallengesConfig _encounters;
        private readonly ScriptedActionDriver _input;
        private float _lastOff = float.MaxValue;

        public SkillCheckScenarioLibrary(INetworkService network, SkillCheckHostRegistry hosts, ISkillCheckRunner runner, ISkillChecks checks,
            SkillChallengeUseCase challenges, SkillChallengesConfig encounters, ScriptedActionDriver input)
        {
            _network = network;
            _hosts = hosts;
            _runner = runner;
            _checks = checks;
            _challenges = challenges;
            _encounters = encounters;
            _input = input;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("TapComboNext", _ => TapCombo(wrong: false)),
            new ScenarioCommand("TapComboWrong", _ => TapCombo(wrong: true)),
            new ScenarioCommand("StartChallenge", StartChallenge),
            new ScenarioCommand("FinishChallengeStep", FinishChallengeStep)
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SkillCheckRunning", _ => _runner.IsRunning ? ScenarioCheck.Pass("a check runs here") : ScenarioCheck.Fail("no check")),
            new ScenarioProbe("SkillCheckInPerfect", _ => CheckInPerfect()),
            new ScenarioProbe("SkillChecksGraded", CheckGraded),
            new ScenarioProbe("ChallengeLit", CheckLit),
            new ScenarioProbe("ChallengesEnded", CheckChallengesEnded)
        };

        private ScenarioCheck StartChallenge(string name)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var challenge = _encounters.Challenges.FirstOrDefault(candidate => candidate != null && candidate.name == name);
            if (challenge == null) return ScenarioCheck.Fail($"no challenge '{name}' in the encounters' list");
            return _challenges.TryStart(challenge) ? ScenarioCheck.Pass($"{name} started") : ScenarioCheck.Fail($"{name} did not start (see the log)");
        }

        private ScenarioCheck FinishChallengeStep(string arg)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var parts = arg.Split(':');
            if (parts.Length != 2 || !Enum.TryParse(parts[1], out SkillCheckGrade grade)) return ScenarioCheck.Fail($"expected Check:Grade, got '{arg}'");

            var host = _hosts.All.FirstOrDefault(candidate => candidate.Definition != null && candidate.Definition.name == parts[0]);
            if (host == null) return ScenarioCheck.Fail($"no station runs {parts[0]}");
            if (_challenges.TryClaim(host, _network.LocalClientId, out int stepId, out _) != ChallengeClaim.Claimed)
                return ScenarioCheck.Fail($"{parts[0]} is not lit for client {_network.LocalClientId}");

            _challenges.Report(stepId, grade, _network.LocalClientId);
            return ScenarioCheck.Pass($"{parts[0]}: {grade} by client {_network.LocalClientId}");
        }

        private ScenarioCheck CheckLit(string station)
        {
            var host = _hosts.All.FirstOrDefault(candidate => candidate.Definition != null && candidate.Definition.Station == station);
            if (host == null) return ScenarioCheck.Fail($"no station '{station}'");
            string detail = $"{station}: {host.LitChallenge ?? "not lit"}, {host.LitSecondsLeft} s";
            return host.LitChallenge != null && host.LitSecondsLeft >= 0 ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckChallengesEnded(string arg)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            if (!TryParseCount(arg, out var grade, out int want)) return ScenarioCheck.Fail($"expected Grade:count, got '{arg}'");

            int count = _challenges.Count(grade);
            string detail = $"{count} {grade}";
            return count == want ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private static bool TryParseCount(string arg, out SkillCheckGrade grade, out int count)
        {
            count = 0;
            var parts = arg.Split(':');
            grade = default;
            return parts.Length == 2 && Enum.TryParse(parts[0], out grade) &&
                   int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
        }

        private ScenarioCheck TapCombo(bool wrong)
        {
            var combo = _runner.Combo;
            if (!_runner.IsRunning || _runner.ComboDone >= combo.Count) return ScenarioCheck.Fail("no combo runs here");

            var next = combo[_runner.ComboDone];
            // A wrong step is neither the next one nor the row's first (that one would count as a fresh start).
            var pressed = wrong ? Other(next, combo[0]) : next;
            _input.Tap(pressed switch
            {
                ComboDirection.Left => ScriptedAction.SkillCheckLeft,
                ComboDirection.Right => ScriptedAction.SkillCheckRight,
                ComboDirection.Up => ScriptedAction.SkillCheckUp,
                _ => ScriptedAction.SkillCheckDown
            });
            return ScenarioCheck.Pass($"{pressed} (step {_runner.ComboDone + 1} of {combo.Count} is {next})");
        }

        private static ComboDirection Other(ComboDirection a, ComboDirection b)
        {
            for (int i = 0; i < 4; i++)
            {
                var direction = (ComboDirection)i;
                if (direction != a && direction != b) return direction;
            }
            return a;
        }

        private ScenarioCheck CheckInPerfect()
        {
            if (!_runner.IsRunning)
            {
                _lastOff = float.MaxValue;
                return ScenarioCheck.Fail("no check");
            }

            var band = _runner.Band;
            float off = Mathf.Abs(_runner.Needle - band.Center);
            bool closing = off < _lastOff;
            _lastOff = off;
            string detail = $"needle {_runner.Needle:0.00}, band {band.Center:0.00} +- {band.PerfectWidth * 0.5f:0.00}";
            return off <= band.PerfectWidth * 0.5f && closing ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckGraded(string arg)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var parts = arg.Split(':');
            if (parts.Length != 2 || !Enum.TryParse(parts[0], out SkillCheckGrade grade)) return ScenarioCheck.Fail($"expected Grade:count, got '{arg}'");

            int want = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int count = _checks.Count(grade);
            string detail = $"{count} {grade}";
            return count == want ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
