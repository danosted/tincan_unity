#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Anchor;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the anchor (plan: peaceful-mode.md, P1). Commands: stand the subject inboard of the anchor
    /// (server), turn the subject's camera to it (subject peer), remember where the ship is (server). Probes: the subject
    /// stands by the anchor, the anchor is down or up (replicated), and the ship has stayed within some metres of the
    /// recorded spot (server).
    /// </summary>
    public sealed class AnchorScenarioLibrary : IScenarioLibrary
    {
        // Where PlaceSubjectAtAnchor stands the subject: this far inboard of the winch, within Interact reach.
        private const float StandInboard = 1.2f;
        private const float NearTolerance = 0.8f;

        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private Vector3 _recorded;
        private bool _hasRecord;

        public AnchorScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, IHumanoidRespawnService respawn)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _respawn = respawn;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtAnchor", _ => PlaceAtAnchor()),
            new ScenarioCommand("FaceAnchor", _ => FaceAnchor()),
            new ScenarioCommand("RecordAnchoredShip", _ => RecordShip())
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectAtAnchor", _ => CheckAtAnchor()),
            new ScenarioProbe("AnchorDown", _ => CheckDown(expected: true)),
            new ScenarioProbe("AnchorUp", _ => CheckDown(expected: false)),
            new ScenarioProbe("ShipHeldWithin", CheckHeld)
        };

        /// <summary>Server: stands the subject on the deck inboard of the anchor, through the respawn service.</summary>
        private ScenarioCheck PlaceAtAnchor()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var anchor = AnchorTransform();
            if (subject == null || body == null || anchor == null) return ScenarioCheck.Fail("no subject or no anchor");

            // Inboard is toward the ship's middle, whatever way the plate faces.
            var ship = Anchor()?.Ship;
            Vector3 toMiddle = ship != null ? Vector3.ProjectOnPlane(ship.position - anchor.position, anchor.up) : -anchor.forward;
            if (toMiddle.sqrMagnitude < 1e-4f) toMiddle = -anchor.forward;
            toMiddle.Normalize();

            Vector3 stand = ScenarioPlacement.OnGround(body, anchor.position + toMiddle * StandInboard, anchor.up);
            _respawn.ResetCharacter(subject, stand, Quaternion.LookRotation(-toMiddle, anchor.up));
            return ScenarioCheck.Pass($"subject placed {StandInboard} m inboard of the anchor, at {stand}");
        }

        /// <summary>Subject peer: turns the subject's camera toward the anchor.</summary>
        private ScenarioCheck FaceAnchor()
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var anchor = AnchorTransform();
            if (subject?.Look == null || body == null || anchor == null) return ScenarioCheck.Fail("no subject look view, or no anchor");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            // Aim at the Interact volume, not the winch's foot on the deck.
            var volume = anchor.FindDescendant("InteractVolume") ?? anchor;
            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, volume);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward {anchor.name}'s {volume.name}");
        }

        /// <summary>Server: remembers where the anchor's ship is now, for ShipHeldWithin.</summary>
        private ScenarioCheck RecordShip()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Anchor()?.Ship;
            if (ship == null) return ScenarioCheck.Fail("no anchor on a ship");

            _recorded = ship.position;
            _hasRecord = true;
            return ScenarioCheck.Pass($"ship at {_recorded}");
        }

        private ScenarioCheck CheckAtAnchor()
        {
            var body = _subject.Body;
            var anchor = AnchorTransform();
            if (body == null || anchor == null) return ScenarioCheck.Fail("no subject or no anchor");

            float distance = Vector3.ProjectOnPlane(body.position - anchor.position, anchor.up).magnitude;
            string detail = $"{distance:0.00} m from the anchor";
            return Mathf.Abs(distance - StandInboard) <= NearTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckDown(bool expected)
        {
            var anchor = Anchor();
            if (anchor == null) return ScenarioCheck.Fail("no anchor");
            string detail = anchor.IsDown ? "the anchor is down" : "the anchor is up";
            return anchor.IsDown == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: the anchor's ship is still within this many metres of where RecordAnchoredShip saw it.</summary>
        private ScenarioCheck CheckHeld(string metres)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var ship = Anchor()?.Ship;
            if (ship == null || !_hasRecord) return ScenarioCheck.Fail("no ship, or no recorded spot");

            float moved = Vector3.Distance(ship.position, _recorded);
            string detail = $"ship moved {moved:0.00} m";
            return moved <= float.Parse(metres, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IAnchor? Anchor() => _actors.GetActors<IAnchor>().FirstOrDefault(anchor => anchor is Component component && component != null);

        private Transform? AnchorTransform() => (Anchor() as Component)?.transform;
    }
}
