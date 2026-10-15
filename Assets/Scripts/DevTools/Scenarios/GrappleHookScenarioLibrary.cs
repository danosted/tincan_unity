#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.GrappleHook;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the grappling hook. Commands: stand the subject at a point in ship space (server; off the ship
    /// to fall), and turn the subject's camera so the hook flies from the eye to a point in ship space (subject peer).
    /// Probes: the subject is near a ship-space point, stands on the ship, has a replicated line or none, what the aim
    /// marker shows (HookAim: None, InRange, OutOfRange; the local player only), and this peer
    /// draws it.
    /// </summary>
    public sealed class GrappleHookScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private readonly GrappleLinePresenter _lines;
        private readonly GrappleAimPresenter _aim;

        public GrappleHookScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors,
            IHumanoidRespawnService respawn, GrappleLinePresenter lines, GrappleAimPresenter aim)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _respawn = respawn;
            _lines = lines;
            _aim = aim;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtShipLocal", PlaceAtShipLocal),
            new ScenarioCommand("AimAtShipLocal", AimAtShipLocal)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectNearShipLocal", NearShipLocal),
            new ScenarioProbe("SubjectOnShip", _ => CheckOnShip()),
            new ScenarioProbe("SubjectHooked", _ => CheckHooked(expected: true)),
            new ScenarioProbe("SubjectUnhooked", _ => CheckHooked(expected: false)),
            new ScenarioProbe("HookLineDrawn", _ => CheckLineDrawn()),
            new ScenarioProbe("SubjectPose", _ => DescribePose()),
            new ScenarioProbe("HookAim", expected => _aim.Reach.ToString() == expected
                ? ScenarioCheck.Pass($"aim {_aim.Reach} at {_aim.MarkerPoint}")
                : ScenarioCheck.Fail($"aim {_aim.Reach}, expected {expected}"))
        };

        /// <summary>Always passes: where the subject's body and its drawn Visual are in ship space on this peer (for the report).</summary>
        private ScenarioCheck DescribePose()
        {
            var body = _subject.Body;
            var ship = Ship();
            if (body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var visual = body.FindDescendant("Visual");
            Vector3 local = ship.Transform.InverseTransformPoint(body.position);
            string drawn = visual != null ? ship.Transform.InverseTransformPoint(visual.position).ToString("0.00") : "none";
            return ScenarioCheck.Pass($"body {local:0.00}, visual {drawn} in ship space");
        }

        /// <summary>Server: puts the subject at "x,y,z" in ship space, facing the ship's centreline. Off the deck it falls.</summary>
        private ScenarioCheck PlaceAtShipLocal(string argument)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var ship = Ship();
            if (subject == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");
            if (!TryParsePoint(argument, out var local)) return ScenarioCheck.Fail($"'{argument}' is not x,y,z");

            Vector3 position = ship.Transform.TransformPoint(local);
            Vector3 inward = Vector3.ProjectOnPlane(ship.Transform.position - position, Vector3.up);
            var facing = inward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(inward, Vector3.up) : ship.Transform.rotation;
            _respawn.ResetCharacter(subject, position, facing);
            return ScenarioCheck.Pass($"subject placed at {local} in ship space");
        }

        /// <summary>Subject peer: turns the camera so a look from the eye (where the hook flies from) meets "x,y,z" in ship space.</summary>
        private ScenarioCheck AimAtShipLocal(string argument)
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var ship = Ship();
            if (subject?.Look == null || body == null || ship == null) return ScenarioCheck.Fail("no subject look view, or no ship");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");
            if (!TryParsePoint(argument, out var local)) return ScenarioCheck.Fail($"'{argument}' is not x,y,z");

            Vector3 eye = body.position + body.up * subject.Movement.EyeHeight;
            Vector3 direction = (ship.Transform.TransformPoint(local) - eye).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg, -subject.Look.MaxPitch, subject.Look.MaxPitch);
            subject.Look.ApplyLook(pitch, yaw);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward {local} in ship space");
        }

        /// <summary>"x,y,z,radius": the subject is within radius metres of a ship-space point on this peer.</summary>
        private ScenarioCheck NearShipLocal(string argument)
        {
            var body = _subject.Body;
            var ship = Ship();
            if (body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var parts = argument.Split(',');
            if (parts.Length != 4 || !TryParsePoint(string.Join(",", parts.Take(3)), out var local) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
            {
                return ScenarioCheck.Fail($"'{argument}' is not x,y,z,radius");
            }

            float distance = Vector3.Distance(ship.Transform.InverseTransformPoint(body.position), local);
            string detail = $"{distance:0.0} m from {local} in ship space";
            return distance <= radius ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The subject stands on the ship on this peer: grounded, on a collider that belongs to it.</summary>
        private ScenarioCheck CheckOnShip()
        {
            var movement = _subject.Resolve()?.Movement;
            var ship = Ship();
            if (movement == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var ground = movement.LastGroundHit;
            bool onShip = movement.CurrentGround.IsGrounded && ground.HasValue && ground.Value.collider.transform.IsChildOf(ship.Transform);
            Vector3 local = ship.Transform.InverseTransformPoint(movement.Transform.position);
            string detail = $"{(onShip ? "on" : "not on")} the ship, at {local} in ship space";
            return onShip ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The replicated line state has a line for the subject (or none).</summary>
        private ScenarioCheck CheckHooked(bool expected)
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");
            var state = _actors.GetActors<IGrappleLineState>().FirstOrDefault(candidate => candidate.IsSimulating);
            if (state == null) return ScenarioCheck.Fail("no GrappleHookState on this peer");

            bool hooked = state.TryGetHookPoint(subject, out var point);
            string detail = hooked ? $"hooked at {point}" : "not hooked";
            return hooked == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>This peer draws a line for the subject (the host draws a client's from the replicated state).</summary>
        private ScenarioCheck CheckLineDrawn()
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            var line = _lines.LineOf(subject.Id);
            return line != null
                ? ScenarioCheck.Pass($"line from {line.GetPosition(0)} to {line.GetPosition(1)}")
                : ScenarioCheck.Fail("no line drawn");
        }

        private IAirshipView? Ship() => _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);

        private static bool TryParsePoint(string text, out Vector3 point)
        {
            point = default;
            var parts = text.Split(',');
            if (parts.Length != 3) return false;

            var values = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
            }

            point = new Vector3(values[0], values[1], values[2]);
            return true;
        }
    }
}
