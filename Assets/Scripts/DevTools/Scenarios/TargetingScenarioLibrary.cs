#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship;
using TinCan.Features.Airship.Damage;
using TinCan.Features.Targeting;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for targeting. The subject can point its camera (pitch) on its own peer; probes read the pitch the
    /// input actually carries on this peer (the server sees it only through the input stream) and run a narrow EyeAim
    /// scan, so a scenario can prove owner and server reach the same answer from replicated input alone.
    /// </summary>
    public sealed class TargetingScenarioLibrary : IScenarioLibrary, IDisposable
    {
        private const float PitchTolerance = 1f;

        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITargetingService _targeting;
        private readonly TargetingDefinition _eyeScan;

        public TargetingScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, ITargetingService targeting, IObjectResolver resolver)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _targeting = targeting;

            // A deliberately narrow vertical window (+-8 deg around the aim), so only the pitch decides a hit.
            _eyeScan = ScriptableObject.CreateInstance<TargetingDefinition>();
            _eyeScan.name = "TD_ScenarioEyeScan";
            _eyeScan.Source = AimSource.EyeAim;
            _eyeScan.Shape = TargetShape.Cone;
            _eyeScan.Range = 3f;
            _eyeScan.HorizontalAngle = 60f;
            _eyeScan.VerticalAngle = 16f;
            var tags = resolver.TryResolve<IGameplayTagRegistry>(out var registry) ? registry : null;
            if (ScenarioTags.Find("State.Damaged", tags) is { } damaged) _eyeScan.RequiredTags = new List<GameplayTag> { damaged };
        }

        public void Dispose() => UnityEngine.Object.Destroy(_eyeScan);

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("SetSubjectPitch", SetPitch)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectAimPitch", CheckPitch),
            new ScenarioProbe("EyeScanHits", index => CheckScan(index, expected: true)),
            new ScenarioProbe("EyeScanMisses", index => CheckScan(index, expected: false))
        };

        /// <summary>Subject peer only: turns the camera, which feeds the next input like the mouse would.</summary>
        private ScenarioCheck SetPitch(string degrees)
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");
            if (subject.Look == null) return ScenarioCheck.Fail("subject has no look view");

            float pitch = Parse(degrees);
            subject.Look.ApplyLook(pitch, subject.Look.Yaw);
            return ScenarioCheck.Pass($"camera pitch {pitch:0.#} deg");
        }

        private ScenarioCheck CheckPitch(string degrees)
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            float carried = subject.InputState.LookPitch;
            string detail = $"input pitch {carried:0.#} deg on {(_network.IsServer ? "server" : "client")}";
            return Mathf.Abs(carried - Parse(degrees)) <= PitchTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckScan(string index, bool expected)
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            bool hit = _targeting.TryAcquire(new HumanoidTargeter(subject), _eyeScan, out var result) &&
                       result.Target is IShipDamagePoint point && point.Index.ToString(CultureInfo.InvariantCulture) == index;
            string detail = hit
                ? $"part {index} at {result.Distance:0.00} m (pitch {subject.InputState.LookPitch:0.#})"
                : $"part {index} not acquired (pitch {subject.InputState.LookPitch:0.#}, parts {Parts()})";
            return hit == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private int Parts() => _actors.GetActors<IAirshipView>().Sum(ship => ShipDamageLocator.FindPoints(ship).Count);

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
