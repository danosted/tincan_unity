#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.SkyHazards;
using TinCan.Features.Stations;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the cannon and sky hazards. Commands: put a hazard on the deck (server), point the barrel along
    /// the cannon's rest direction (subject peer, through the gunner's aim as the mouse would), and put a target on the
    /// arc the barrel points along now (server), so a shot with no further aiming hits it. Probes read replicated state
    /// on this peer: who mans the cannon, how many hazards exist, and how many balls this peer drew; the server also
    /// counts hazards shot down. The subject peer can also point the barrel at an exact aim, check that aiming turns the
    /// barrel past the back (a turret wraps, and the replicated barrel follows) and leaves the body facing where it was (the
    /// Gunner input context).
    /// </summary>
    public sealed class CannonScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ISkyHazards _hazards;
        private readonly CannonShotPresenter _presenter;
        private readonly CannonConfig _config;
        private readonly CannonAimProcessor _aim;
        private readonly IHumanoidRespawnService _respawn;
        private readonly GunnerAimUseCase _gunner;
        private float? _recordedFacing;

        // Where PlaceSubjectAtCannon stands the subject: this far behind the cannon, on its seat side; within Interact reach.
        private const float StandBehind = 2.2f;
        private const float NearTolerance = 0.8f;

        private int _hitsBeforeSpawn;

        public CannonScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, ISkyHazards hazards,
            CannonShotPresenter presenter, CannonConfig config, CannonAimProcessor aim, IHumanoidRespawnService respawn, GunnerAimUseCase gunner)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _hazards = hazards;
            _presenter = presenter;
            _config = config;
            _aim = aim;
            _respawn = respawn;
            _gunner = gunner;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtCannon", PlaceAtCannon),
            new ScenarioCommand("FaceCannon", FaceCannon),
            new ScenarioCommand("AimCannon", Aim),
            new ScenarioCommand("AimCannonAt", AimAt),
            new ScenarioCommand("RecordSubjectFacing", _ => RecordFacing()),
            new ScenarioCommand("SpawnTargetOnArc", SpawnOnArc),
            new ScenarioCommand("SpawnHazardOnDeck", _ => SpawnOnDeck())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectAtCannon", CheckAtCannon),
            new ScenarioProbe("CannonManned", which => CheckManned(expected: true, which)),
            new ScenarioProbe("CannonFree", which => CheckManned(expected: false, which)),
            new ScenarioProbe("HazardsVisible", CheckVisible),
            new ScenarioProbe("HazardsDestroyed", CheckDestroyed),
            new ScenarioProbe("BallsShown", CheckBalls),
            new ScenarioProbe("HazardHits", CheckHits),
            new ScenarioProbe("ShipHealthBelow", CheckShipHealthBelow),
            new ScenarioProbe("GunnerAimYaw", CheckGunnerYaw),
            new ScenarioProbe("CannonYaw", CheckCannonYaw),
            new ScenarioProbe("SubjectFacingHeld", _ => CheckFacingHeld())
        };

        /// <summary>Server: stands the subject behind the cannon ("aft": the aftmost) on the deck, through the respawn service (the owner snaps).</summary>
        private ScenarioCheck PlaceAtCannon(string which)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var cannon = Cannon(which)?.Base;
            if (subject == null || body == null || cannon == null) return ScenarioCheck.Fail("no subject or no cannon");

            Vector3 stand = ScenarioPlacement.OnGround(body, cannon.position - cannon.forward * StandBehind, cannon.up);
            _respawn.ResetCharacter(subject, stand, body.rotation);
            return ScenarioCheck.Pass($"subject placed {StandBehind} m behind the cannon");
        }

        /// <summary>Subject peer: turns the subject's camera toward the scenario's cannon (FaceObject would take the first by name).</summary>
        private ScenarioCheck FaceCannon(string which)
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var cannon = Cannon(which)?.Base;
            if (subject?.Look == null || body == null || cannon == null) return ScenarioCheck.Fail("no subject look view, or no cannon");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, cannon);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward {cannon.name}");
        }

        /// <summary>The subject stands behind the cannon on this peer, within reach (a lagged client sees its ship a little behind).</summary>
        private ScenarioCheck CheckAtCannon(string which)
        {
            var body = _subject.Body;
            var cannon = Cannon(which)?.Base;
            if (body == null || cannon == null) return ScenarioCheck.Fail("no subject or no cannon");

            float distance = Vector3.ProjectOnPlane(body.position - cannon.position, cannon.up).magnitude;
            string detail = $"{distance:0.00} m from the cannon";
            return Mathf.Abs(distance - StandBehind) <= NearTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Subject peer: points the barrel along the cannon's rest direction at this elevation, as the mouse would.</summary>
        private ScenarioCheck Aim(string elevation)
        {
            if (!_gunner.TrySetAim(new Vector2(0f, Parse(elevation)))) return ScenarioCheck.Fail("subject-peer command: this peer does not man a cannon");
            return ScenarioCheck.Pass($"barrel yaw 0 deg, elevation {elevation} deg");
        }

        /// <summary>Subject peer: points the barrel at "yaw elevation" (degrees, the cannon's frame), as the mouse would.</summary>
        private ScenarioCheck AimAt(string yawAndElevation)
        {
            var parts = yawAndElevation.Split(' ');
            if (parts.Length != 2) return ScenarioCheck.Fail("expected \"<yaw> <elevation>\"");
            if (!_gunner.TrySetAim(new Vector2(Parse(parts[0]), Parse(parts[1])))) return ScenarioCheck.Fail("subject-peer command: this peer does not man a cannon");
            return ScenarioCheck.Pass($"barrel yaw {parts[0]} deg, elevation {parts[1]} deg");
        }

        /// <summary>Subject peer: remembers which way the body faces in the cannon's frame, for SubjectFacingHeld.</summary>
        private ScenarioCheck RecordFacing()
        {
            if (FacingInCannonFrame() is not { } facing) return ScenarioCheck.Fail("no subject or no cannon");
            _recordedFacing = facing;
            return ScenarioCheck.Pass($"body faces {facing:0.#} deg from the barrel's rest");
        }

        /// <summary>Subject peer: the gunner's aim yaw is at this many degrees.</summary>
        private ScenarioCheck CheckGunnerYaw(string degrees)
        {
            if (_gunner.LocalAim is not { } aim) return ScenarioCheck.Fail("this peer does not man a cannon");
            string detail = $"aim yaw {aim.Aim.x:0.#} deg (limit {_config.YawLimit:0.#})";
            return Mathf.Abs(aim.Aim.x - Parse(degrees)) <= 0.5f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The scenario cannon's replicated barrel yaw on this peer is at this many degrees (the server's aim).</summary>
        private ScenarioCheck CheckCannonYaw(string degrees)
        {
            if (Cannon() is not { } cannon) return ScenarioCheck.Fail("no cannon");
            string detail = $"barrel yaw {cannon.Yaw:0.#} deg on this peer";
            return Mathf.Abs(Mathf.DeltaAngle(cannon.Yaw, Parse(degrees))) <= 1f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Subject peer: the body still faces where RecordSubjectFacing saw it; aiming turned the barrel, not the body.</summary>
        private ScenarioCheck CheckFacingHeld()
        {
            if (_recordedFacing is not { } recorded || FacingInCannonFrame() is not { } facing) return ScenarioCheck.Fail("no recorded facing, subject or cannon");
            float drift = Mathf.Abs(Mathf.DeltaAngle(recorded, facing));
            string detail = $"body turned {drift:0.#} deg while aiming";
            return drift <= 3f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private float? FacingInCannonFrame()
        {
            var body = _subject.Body;
            var cannon = Cannon()?.Base;
            if (body == null || cannon == null) return null;
            return Vector3.SignedAngle(Vector3.ProjectOnPlane(cannon.forward, cannon.up), Vector3.ProjectOnPlane(body.forward, cannon.up), cannon.up);
        }

        /// <summary>Server: a target this far along the arc the barrel points along now.</summary>
        private ScenarioCheck SpawnOnArc(string metres)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var cannon = Cannon();
            if (cannon?.Base == null || cannon.Muzzle == null) return ScenarioCheck.Fail("no cannon");

            float distance = Parse(metres);
            var arc = BallisticArc.FromMuzzle(cannon.Muzzle.position, _aim.MuzzleDirection(cannon.Base.rotation, cannon.Yaw, cannon.Elevation),
                _config.MuzzleSpeed, Vector3.zero, _config.GravityVector, 0);
            float t = 0f;
            while (t < _config.MaxLifetime && Vector3.Distance(arc.PositionAt(t), arc.Origin) < distance) t += 0.01f;

            var hazard = _hazards.SpawnAt(arc.PositionAt(t));
            return hazard != null
                ? ScenarioCheck.Pass($"target {distance} m down the arc (yaw {cannon.Yaw:0.#}, elevation {cannon.Elevation:0.#})")
                : ScenarioCheck.Fail("hazard spawn failed (no SkyHazardConfig prefab?)");
        }

        /// <summary>
        /// Server: a hazard resting on the top of the ship's solid body (found by a ray down onto its own colliders), so
        /// the next tick counts it as contact. Hazards no longer drift (H0), so this is how a test brings one to the ship.
        /// Hits from before this no longer count toward <c>HazardHits</c>.
        /// </summary>
        private ScenarioCheck SpawnOnDeck()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
            if (ship == null) return ScenarioCheck.Fail("no ship");

            var root = (ship as Component)?.transform ?? ship.Transform;
            Bounds? body = null;
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (body is { } bounds)
                {
                    bounds.Encapsulate(collider.bounds);
                    body = bounds;
                }
                else body = collider.bounds;
            }
            if (body == null) return ScenarioCheck.Fail("the ship has no solid collider");

            var from = new Vector3(body.Value.center.x, body.Value.max.y + 5f, body.Value.center.z);
            var deck = Physics.RaycastAll(from, Vector3.down, body.Value.size.y + 10f, ~0, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider.transform.IsChildOf(root))
                .OrderBy(hit => hit.distance)
                .Select(hit => (RaycastHit?)hit)
                .FirstOrDefault();
            if (deck == null) return ScenarioCheck.Fail("no deck under the ship's middle");

            _hitsBeforeSpawn = _hazards.Hits;
            var hazard = _hazards.SpawnAt(deck.Value.point);
            return hazard != null
                ? ScenarioCheck.Pass($"hazard on the deck at {deck.Value.point}")
                : ScenarioCheck.Fail("hazard spawn failed (no SkyHazardConfig prefab?)");
        }

        /// <summary>Server: hits on the ship since the last SpawnHazardOnDeck.</summary>
        private ScenarioCheck CheckHits(string count)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            int hits = _hazards.Hits - _hitsBeforeSpawn;
            string detail = $"{hits} hit(s) on the ship since the spawn ({_hazards.Hits} in all)";
            return hits >= int.Parse(count, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The ship's replicated health on this peer is below this value.</summary>
        private ScenarioCheck CheckShipHealthBelow(string value)
        {
            var controller = _actors.GetActors<IAirshipView>().OfType<IShipState>().FirstOrDefault()?.Controller;
            if (controller == null || !controller.TryGetAttributeSet<HealthAttributeSet>(out var health)) return ScenarioCheck.Fail("no ship health");

            string detail = $"ship health {health.Health:0} / {health.MaxHealth:0} on this peer";
            return health.Health < Parse(value) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckManned(bool expected, string which)
        {
            var subject = _subject.Resolve();
            if (Cannon(which) is not IStation station) return ScenarioCheck.Fail("no cannon");
            ulong? owner = subject != null ? ((IPossessable)subject).OwnerId : null;

            bool manned = station.OccupantClientId != null && station.OccupantClientId == owner;
            string detail = station.OccupantClientId is { } occupant ? $"manned by client {occupant}" : "free";
            return manned == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckVisible(string count)
        {
            // Every spawned hazard on this peer, found in the scene: hazards are not actors, and this is dev tooling.
            int visible = Object.FindObjectsByType<SkyHazardNetworkMediator>(FindObjectsSortMode.None).Count(hazard => hazard.IsSpawned);
            string detail = $"{visible} hazard(s) on this peer";
            return visible == int.Parse(count, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckDestroyed(string count)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            string detail = $"{_hazards.Destroyed} shot down";
            return _hazards.Destroyed >= int.Parse(count, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckBalls(string count)
        {
            string detail = $"{_presenter.BallsShown} ball(s) drawn on this peer";
            return _presenter.BallsShown >= int.Parse(count, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        // The ship carries a cannon on each side; scenarios use the starboard one. Picked by where it sits on the ship, so
        // every peer names the same cannon whatever order they spawned in. "aft" picks the aftmost (then starboard) one,
        // for ships with cannons fore and aft (the Galleon).
        private ICannon? Cannon(string which = "")
        {
            var cannons = _actors.GetActors<ICannon>().Where(cannon => cannon.Base != null);
            return which == "aft"
                ? cannons.OrderBy(cannon => ShipLocal(cannon).z).ThenByDescending(cannon => ShipLocal(cannon).x).FirstOrDefault()
                : cannons.OrderByDescending(cannon => ShipLocal(cannon).x).FirstOrDefault();
        }

        private static Vector3 ShipLocal(ICannon cannon) =>
            cannon.ShipRoot != null ? cannon.ShipRoot.InverseTransformPoint(cannon.Base!.position) : cannon.Base!.position;

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
