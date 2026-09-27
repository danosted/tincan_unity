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
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Interaction;
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
        private readonly IHumanoidRespawnService _respawn;
        private readonly InteractionTargetingSettings? _interaction;

        private const float StandOff = 1.3f;

        public TargetingScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, ITargetingService targeting,
            IHumanoidRespawnService respawn, IObjectResolver resolver)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _targeting = targeting;
            _respawn = respawn;
            _interaction = resolver.TryResolve<InteractionTargetingSettings>(out var interaction) ? interaction : null;

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
            new ScenarioCommand("SetSubjectPitch", SetPitch),
            new ScenarioCommand("PlaceSubjectNear", PlaceNear),
            new ScenarioCommand("FaceObject", Face)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectAimPitch", CheckPitch),
            new ScenarioProbe("EyeScanHits", index => CheckScan(index, expected: true)),
            new ScenarioProbe("EyeScanMisses", index => CheckScan(index, expected: false)),
            new ScenarioProbe("InteractTargetIs", CheckInteractTarget),
            new ScenarioProbe("SubjectNear", CheckNear)
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

        /// <summary>Server: stands the subject this far from a named object on the ship, on the side it already stands, on the deck.</summary>
        private ScenarioCheck PlaceNear(string objectName)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var target = FindOnShip(objectName);
            if (subject == null || body == null || target == null) return ScenarioCheck.Fail($"no subject, or nothing named '{objectName}' on a ship");

            Vector3 away = Vector3.ProjectOnPlane(body.position - target.position, target.up);
            if (away.sqrMagnitude < 0.01f) away = -target.forward;
            Vector3 stand = target.position + away.normalized * StandOff;
            stand = ScenarioPlacement.OnGround(body, stand, target.up);
            _respawn.ResetCharacter(subject, stand, body.rotation);
            return ScenarioCheck.Pass($"subject placed {StandOff} m from {target.name}");
        }

        /// <summary>Subject peer: turns the camera (world yaw, level pitch) toward a named object, as the mouse would.</summary>
        private ScenarioCheck Face(string objectName)
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var target = FindOnShip(objectName);
            if (subject?.Look == null || body == null || target == null) return ScenarioCheck.Fail($"no subject look view, or nothing named '{objectName}'");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            Vector3 to = target.position - body.position;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            subject.Look.ApplyLook(0f, yaw);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg toward {target.name}");
        }

        /// <summary>What TD_Interact (the server's interaction query, and the prompt) acquires for the subject on this peer.</summary>
        private ScenarioCheck CheckInteractTarget(string objectName)
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");
            if (_interaction == null) return ScenarioCheck.Fail("InteractionFeatureInstaller is not active");

            string found = _targeting.TryAcquire(new HumanoidTargeter(subject), _interaction.Targeting, out var result) && result.Target is Component component
                ? component.name
                : "nothing";
            string detail = $"{_interaction.Targeting.name} acquires {found}";
            return found.StartsWith(objectName, StringComparison.Ordinal) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The subject stands within reach of a named object on this peer (used to wait for PlaceSubjectNear).</summary>
        private ScenarioCheck CheckNear(string objectName)
        {
            var body = _subject.Body;
            var target = FindOnShip(objectName);
            if (body == null || target == null) return ScenarioCheck.Fail($"no subject, or nothing named '{objectName}'");

            float distance = Vector3.ProjectOnPlane(body.position - target.position, target.up).magnitude;
            string detail = $"{distance:0.00} m from {target.name}";
            return distance <= StandOff + 0.3f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private Transform? FindOnShip(string objectName)
        {
            foreach (var ship in _actors.GetActors<IAirshipView>())
            {
                if (ship.Transform == null) continue;
                foreach (var child in ship.Transform.GetComponentsInChildren<Transform>())
                {
                    if (child.name.StartsWith(objectName, StringComparison.Ordinal)) return child;
                }
            }
            return null;
        }

        private int Parts() => _actors.GetActors<IAirshipView>().Sum(ship => ShipDamageLocator.FindPoints(ship).Count);

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
