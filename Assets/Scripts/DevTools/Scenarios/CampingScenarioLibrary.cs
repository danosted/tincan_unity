#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Camping;
using TinCan.Features.SkyIslands;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for camping on an island: the server stands the subject in the middle clearing of the main island
    /// nearest the ship (open ground, kept free of trees and rocks); the subject's peer aims at the ground and faces what
    /// it placed. "The fire" and "the tent" are the ones nearest the subject on this peer. Probes read replicated state,
    /// and the placement preview on the subject's own peer.
    /// </summary>
    public sealed class CampingScenarioLibrary : IScenarioLibrary
    {
        private const float Near = 8f;

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ScenarioSubject _subject;
        private readonly IHumanoidRespawnService _respawn;
        private readonly ISkyIslands? _islands;
        private readonly SkyIslandConfig? _islandConfig;
        private readonly CampPlacementPreviewPresenter? _preview;
        private readonly SkyIslandSiteProcessor _siteProcessor = new();

        public CampingScenarioLibrary(INetworkService network, IActorRegistry actors, ScenarioSubject subject,
            IHumanoidRespawnService respawn, IObjectResolver resolver)
        {
            _network = network;
            _actors = actors;
            _subject = subject;
            _respawn = respawn;
            resolver.TryResolve(out _islands);
            resolver.TryResolve(out _islandConfig);
            resolver.TryResolve(out _preview);
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectInAClearing", _ => PlaceInClearing()),
            new ScenarioCommand("AimAtGround", AimAtGround),
            new ScenarioCommand("FaceNearest", FaceNearest)
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectInAClearing", _ => CheckInClearing()),
            new ScenarioProbe("PlacementPreview", CheckPreview),
            new ScenarioProbe("CampfirePlaced", _ => CheckCampfirePlaced()),
            new ScenarioProbe("FireLit", _ => CheckFire(expectLit: true)),
            new ScenarioProbe("FireOut", _ => CheckFire(expectLit: false)),
            new ScenarioProbe("TentPitched", _ => CheckTent(expectStanding: true)),
            new ScenarioProbe("TentGone", _ => CheckTent(expectStanding: false))
        };

        /// <summary>Server: stands the subject in the middle of the main island nearest the ship, facing the clearing's way.</summary>
        private ScenarioCheck PlaceInClearing()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(s => s.Transform != null);
            if (subject == null || body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");
            if (Clearing(ship.Transform.position) is not { } clearing) return ScenarioCheck.Fail("no main island standing");

            var stand = ScenarioPlacement.OnGround(body, clearing.position, Vector3.up, 4f);
            _respawn.ResetCharacter(subject, stand, clearing.rotation);
            return ScenarioCheck.Pass($"subject placed in a clearing at {clearing.position}");
        }

        /// <summary>
        /// Subject peer: "distance[,turn]" turns the camera <c>turn</c> degrees from where it faces and down at the ground
        /// that far ahead, as a player looks where they will set something down.
        /// </summary>
        private ScenarioCheck AimAtGround(string arguments)
        {
            var subject = _subject.Resolve();
            var movement = subject?.Movement;
            if (subject?.Look == null || movement == null) return ScenarioCheck.Fail("no subject look view");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var parts = arguments.Split(',');
            float distance = float.Parse(parts[0], CultureInfo.InvariantCulture);
            float turn = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0f;
            float yaw = subject.Look.Yaw + turn;

            var body = movement.Transform;
            var ahead = body.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance;
            if (!Physics.Raycast(ahead + Vector3.up * 3f, Vector3.down, out var ground, 8f, ~0, QueryTriggerInteraction.Ignore))
                return ScenarioCheck.Fail($"no ground {distance} m ahead");

            var eye = body.position + body.up * movement.EyeHeight;
            var direction = (ground.point - eye).normalized;
            float pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            subject.Look.ApplyLook(pitch, yaw);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward the ground {distance} m ahead");
        }

        /// <summary>Subject peer: turns the camera toward the middle of the nearest object with this name (a placed part).</summary>
        private ScenarioCheck FaceNearest(string objectName)
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            if (subject?.Look == null || body == null) return ScenarioCheck.Fail("no subject look view");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var target = Object.FindObjectsByType<Transform>()
                .Where(t => t.name == objectName)
                .OrderBy(t => (t.position - body.position).sqrMagnitude)
                .FirstOrDefault();
            if (target == null) return ScenarioCheck.Fail($"nothing named '{objectName}' on this peer");

            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, target);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward {target.name}");
        }

        private ScenarioCheck CheckInClearing()
        {
            var body = _subject.Body;
            if (body == null) return ScenarioCheck.Fail("no subject");
            if (Clearing(body.position) is not { } clearing) return ScenarioCheck.Fail("no main island standing on this peer");

            float distance = Level(body.position - clearing.position).magnitude;
            string detail = $"{distance:0.0} m from the middle of a clearing";
            return distance < 1.5f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>"Kind,Verdict": what the subject's placement preview shows on this peer (the subject's own).</summary>
        private ScenarioCheck CheckPreview(string expected)
        {
            if (_preview == null) return ScenarioCheck.Fail("Camping feature not loaded in this profile");
            string shown = _preview.Current is { } current ? $"{current.Kind},{current.Verdict}" : "nothing";
            string detail = $"the preview shows {shown}";
            return string.Equals(shown, expected, StringComparison.OrdinalIgnoreCase) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckCampfirePlaced()
        {
            var fire = Nearest<FireNetworkMediator>();
            string detail = fire == null ? "no campfire near the subject" : $"a campfire {Distance(fire):0.0} m from the subject";
            return fire != null ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckFire(bool expectLit)
        {
            var fire = Nearest<FireNetworkMediator>();
            if (fire == null) return ScenarioCheck.Fail("no campfire near the subject");

            string detail = fire.IsLit ? $"fire lit, {fire.BurnSecondsLeft:0} s left" : "fire out";
            return fire.IsLit == expectLit ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckTent(bool expectStanding)
        {
            var tent = Nearest<TentSpotNetworkMediator>();
            bool standing = tent != null && tent.IsSpawned && tent.IsPitched;
            string detail = standing ? $"a pitched tent {Distance(tent!):0.0} m from the subject" : "no tent near the subject";
            return standing == expectStanding ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The nearest spawned one within reach of the subject, off any ship (the ship's hearth is not a campfire).</summary>
        private T? Nearest<T>() where T : Unity.Netcode.NetworkBehaviour
        {
            var body = _subject.Body;
            if (body == null) return null;
            return Object.FindObjectsByType<T>()
                .Where(candidate => candidate.IsSpawned && candidate.GetComponentInParent<IAirshipView>() == null)
                .Where(candidate => (candidate.transform.position - body.position).magnitude < Near)
                .OrderBy(candidate => (candidate.transform.position - body.position).sqrMagnitude)
                .FirstOrDefault();
        }

        private float Distance(Component component) =>
            _subject.Body == null ? float.NaN : (component.transform.position - _subject.Body.position).magnitude;

        /// <summary>The middle clearing of the main island nearest the point, in world space.</summary>
        private Pose? Clearing(Vector3 near)
        {
            if (_islands == null || _islandConfig == null) return null;
            var island = _islands.Standing.Where(i => !i.IsSatellite).OrderBy(i => Level(i.Top - near).sqrMagnitude)
                .Select(i => (SkyIslandSpec?)i).FirstOrDefault();
            if (island is not { } found) return null;

            var sites = new SkyIslandSites();
            _siteProcessor.Sites(found, _islandConfig.SiteRules, sites);
            var middle = sites.Clearings[0];
            return new Pose(found.Top + middle.position, middle.rotation);
        }

        private static Vector3 Level(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
