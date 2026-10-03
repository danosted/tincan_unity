#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Helm;
using TinCan.Features.Stations;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the helm station. Commands: stand the subject behind the helm (server), turn the subject's
    /// camera to it (subject peer), and remember the ship's pose (server). Probes: the subject stands by the helm or on
    /// its seat, who mans it (replicated), how far the ship moved and turned since the recorded pose, and whether the
    /// ship's input is idle (server).
    /// </summary>
    public sealed class HelmScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private Vector3 _recordedPosition;
        private Quaternion _recordedRotation;
        private bool _recorded;

        // Where PlaceSubjectAtHelm stands the subject: this far behind the wheel, within Interact reach.
        private const float StandBehind = 2.2f;
        private const float NearTolerance = 0.8f;
        private const float SeatTolerance = 0.6f;

        public HelmScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, IHumanoidRespawnService respawn)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _respawn = respawn;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtHelm", _ => PlaceAtHelm()),
            new ScenarioCommand("FaceHelm", _ => FaceHelm()),
            new ScenarioCommand("RecordShipPose", _ => RecordShipPose())
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectAtHelm", _ => CheckAtHelm()),
            new ScenarioProbe("SubjectOnHelmSeat", _ => CheckOnSeat()),
            new ScenarioProbe("HelmManned", _ => CheckManned(expected: true)),
            new ScenarioProbe("HelmFree", _ => CheckManned(expected: false)),
            new ScenarioProbe("ShipMoved", CheckMoved),
            new ScenarioProbe("ShipTurned", CheckTurned),
            new ScenarioProbe("ShipInputIdle", _ => CheckInputIdle())
        };

        /// <summary>Server: stands the subject behind the helm on the deck, through the respawn service (the owner snaps).</summary>
        private ScenarioCheck PlaceAtHelm()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var helm = HelmTransform();
            if (subject == null || body == null || helm == null) return ScenarioCheck.Fail("no subject or no helm");

            Vector3 stand = ScenarioPlacement.OnGround(body, helm.position - helm.forward * StandBehind, helm.up);
            _respawn.ResetCharacter(subject, stand, helm.rotation);
            return ScenarioCheck.Pass($"subject placed {StandBehind} m behind the helm");
        }

        /// <summary>Subject peer: turns the subject's camera toward the helm.</summary>
        private ScenarioCheck FaceHelm()
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var helm = HelmTransform();
            if (subject?.Look == null || body == null || helm == null) return ScenarioCheck.Fail("no subject look view, or no helm");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            Vector3 to = helm.position - body.position;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            subject.Look.ApplyLook(0f, yaw);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg toward {helm.name}");
        }

        /// <summary>Server: remembers where the ship is now, for ShipMoved and ShipTurned.</summary>
        private ScenarioCheck RecordShipPose()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            _recordedPosition = ship.Transform.position;
            _recordedRotation = ship.Transform.rotation;
            _recorded = true;
            return ScenarioCheck.Pass($"ship at {_recordedPosition}");
        }

        private ScenarioCheck CheckAtHelm()
        {
            var body = _subject.Body;
            var helm = HelmTransform();
            if (body == null || helm == null) return ScenarioCheck.Fail("no subject or no helm");

            float distance = Vector3.ProjectOnPlane(body.position - helm.position, helm.up).magnitude;
            string detail = $"{distance:0.00} m from the helm";
            return Mathf.Abs(distance - StandBehind) <= NearTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The subject is on the helm's seat on this peer (it stays there while steering: walking is off).</summary>
        private ScenarioCheck CheckOnSeat()
        {
            var body = _subject.Body;
            var seat = (Helm() as IStation)?.Seat;
            if (body == null || seat == null) return ScenarioCheck.Fail("no subject or no helm seat");

            float distance = Vector3.ProjectOnPlane(body.position - seat.position, seat.up).magnitude;
            string detail = $"{distance:0.00} m from the seat";
            return distance <= SeatTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckManned(bool expected)
        {
            var subject = _subject.Resolve();
            if (Helm() is not IStation station) return ScenarioCheck.Fail("no helm");
            ulong? owner = subject != null ? ((IPossessable)subject).OwnerId : null;

            bool manned = station.OccupantClientId != null && station.OccupantClientId == owner;
            string detail = station.OccupantClientId is { } occupant ? $"manned by client {occupant}" : "free";
            return manned == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: the ship moved at least this many metres since RecordShipPose.</summary>
        private ScenarioCheck CheckMoved(string metres)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var ship = Ship();
            if (ship == null || !_recorded) return ScenarioCheck.Fail("no ship, or no recorded pose");

            float moved = Vector3.Distance(ship.Transform.position, _recordedPosition);
            string detail = $"ship moved {moved:0.0} m";
            return moved >= Parse(metres) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: the ship turned at least this many degrees since RecordShipPose.</summary>
        private ScenarioCheck CheckTurned(string degrees)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var ship = Ship();
            if (ship == null || !_recorded) return ScenarioCheck.Fail("no ship, or no recorded pose");

            float turned = Quaternion.Angle(ship.Transform.rotation, _recordedRotation);
            string detail = $"ship turned {turned:0.0} deg";
            return turned >= Parse(degrees) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: nobody steers the ship now (its pilot input is zero).</summary>
        private ScenarioCheck CheckInputIdle()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            var input = ship.InputState;
            string detail = $"throttle {input.Throttle:0.##}, yaw {input.Yaw:0.##}, pitch {input.Pitch:0.##}";
            bool idle = input.Throttle == 0f && input.Yaw == 0f && input.Pitch == 0f;
            return idle ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IHelm? Helm() => _actors.GetActors<IHelm>().FirstOrDefault(helm => helm is Component component && component != null);

        private Transform? HelmTransform() => (Helm() as Component)?.transform;

        private IAirshipView? Ship() => Helm()?.Ship ?? _actors.GetActors<IAirshipView>().FirstOrDefault();

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
