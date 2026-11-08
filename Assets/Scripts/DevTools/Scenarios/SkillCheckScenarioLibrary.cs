#nullable enable
using System;
using System.Globalization;
using TinCan.Core.Domain.Networking;
using TinCan.Features.SkillChecks;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for skill checks (hazards-and-boons.md, B3). Probes: this peer runs a check
    /// (<c>SkillCheckRunning</c>); the needle is in the Perfect part and still heading for its middle, so a press on the
    /// next frame or two lands there (<c>SkillCheckInPerfect</c>); and, on the server, how many checks ended with a grade
    /// (<c>SkillChecksGraded "Perfect:1"</c>, exactly).
    /// </summary>
    public sealed class SkillCheckScenarioLibrary : IScenarioLibrary
    {
        private readonly INetworkService _network;
        private readonly ISkillCheckRunner _runner;
        private readonly ISkillChecks _checks;
        private float _lastOff = float.MaxValue;

        public SkillCheckScenarioLibrary(INetworkService network, ISkillCheckRunner runner, ISkillChecks checks)
        {
            _network = network;
            _runner = runner;
            _checks = checks;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SkillCheckRunning", _ => _runner.IsRunning ? ScenarioCheck.Pass("a check runs here") : ScenarioCheck.Fail("no check")),
            new ScenarioProbe("SkillCheckInPerfect", _ => CheckInPerfect()),
            new ScenarioProbe("SkillChecksGraded", CheckGraded)
        };

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
