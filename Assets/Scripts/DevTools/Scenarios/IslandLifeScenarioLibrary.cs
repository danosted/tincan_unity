#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Items;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Camping;
using TinCan.Features.IslandDetails;
using TinCan.Features.SkyIslands;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for life on the islands (plan .docs/plans/island-life.md). Trees are the networked deadwood piles on
    /// the islands (objects named IslandTree_*); a step about "the tree" means the one nearest the subject on this peer, so
    /// the server and the subject's peer pick the same one. Caves come from the islands' sites; "the cave" is the one
    /// nearest the subject (or, to place the subject, the ship). Every probe reads what this peer sees.
    /// </summary>
    public sealed class IslandLifeScenarioLibrary : IScenarioLibrary
    {
        private const float StandOff = 1.3f;
        private const string TreePrefix = "IslandTree";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ScenarioSubject _subject;
        private readonly IHumanoidRespawnService _respawn;
        private readonly ISkyIslands? _islands;
        private readonly SkyIslandConfig? _islandConfig;
        private readonly IslandSceneryUseCase? _scenery;
        private readonly SkyIslandSiteProcessor _siteProcessor = new();
        private RegrowingItemSourceNetworkMediator? _chosen;

        public IslandLifeScenarioLibrary(INetworkService network, IActorRegistry actors, ScenarioSubject subject,
            IHumanoidRespawnService respawn, IObjectResolver resolver)
        {
            _network = network;
            _actors = actors;
            _subject = subject;
            _respawn = respawn;
            resolver.TryResolve(out _islands);
            resolver.TryResolve(out _islandConfig);
            resolver.TryResolve(out _scenery);
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtIslandTree", _ => PlaceAtTree()),
            new ScenarioCommand("FaceIslandTree", _ => FaceTree()),
            new ScenarioCommand("PlaceSubjectInCave", _ => PlaceInCave())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("IslandTreesStand", _ => CheckTreesStand()),
            new ScenarioProbe("IslandSceneryStands", _ => CheckScenery()),
            new ScenarioProbe("SubjectNearIslandTree", _ => CheckNearTree()),
            new ScenarioProbe("IslandTreeLogs", CheckTreeLogs),
            new ScenarioProbe("SubjectInCave", _ => CheckInCave())
        };

        /// <summary>Server: stands the subject by the full tree nearest the ship, on its island's inner side.</summary>
        private ScenarioCheck PlaceAtTree()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var ship = Ship();
            if (subject == null || body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var tree = Trees().Where(t => t.Pile.Available == t.Pile.Capacity)
                .OrderBy(t => (t.Root.position - ship.Transform.position).sqrMagnitude).FirstOrDefault();
            if (tree.Root == null) return ScenarioCheck.Fail($"no full island tree ({Trees().Count()} trees)");

            var inward = InwardOf(tree.Root.position);
            var stand = ScenarioPlacement.OnGround(body, tree.Root.position + inward * StandOff, Vector3.up, 6f);
            _respawn.ResetCharacter(subject, stand, Quaternion.LookRotation(-inward, Vector3.up));
            _chosen = tree.Pile;
            return ScenarioCheck.Pass($"subject placed {StandOff} m from {tree.Root.name} at {tree.Root.position}");
        }

        /// <summary>Subject peer: turns the camera toward the trunk of the tree nearest the subject, at chest height.</summary>
        private ScenarioCheck FaceTree()
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            if (subject?.Look == null || body == null) return ScenarioCheck.Fail("no subject look view");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var tree = NearestTree(body.position);
            var volume = tree?.Pile.transform.Find("InteractVolume");
            if (volume == null) return ScenarioCheck.Fail("no island tree with an interact volume");

            _chosen = tree!.Value.Pile;
            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, volume);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward {tree.Value.Root.name}");
        }

        /// <summary>Server: stands the subject in the chamber of the cave nearest the ship, facing in from its mouth.</summary>
        private ScenarioCheck PlaceInCave()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var ship = Ship();
            if (subject == null || body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");
            if (NearestCave(ship.Transform.position) is not { } found) return ScenarioCheck.Fail($"no cave on the {_islands?.StandingCount ?? 0} islands standing");

            var (island, cave) = found;
            var mouth = cave.MouthDirection;
            var chamber = island.Top + cave.Chamber;
            // Probe from a little over the floor: the roof is higher still.
            var stand = ScenarioPlacement.OnGround(body, chamber + mouth * (cave.Radius * 0.45f), Vector3.up, 3.5f);
            _respawn.ResetCharacter(subject, stand, Quaternion.LookRotation(-mouth, Vector3.up));
            return ScenarioCheck.Pass($"subject placed in the {cave} of {island.Id}, facing in");
        }

        private ScenarioCheck CheckTreesStand()
        {
            int trees = Trees().Count();
            string detail = $"{trees} island trees on this peer";
            return trees > 0 ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckScenery()
        {
            if (_scenery == null) return ScenarioCheck.Fail("IslandDetails feature not loaded in this profile");
            int rocks = Object.FindObjectsByType<Transform>().Count(t => t.name.StartsWith("Boulder_"));
            string detail = $"scenery on {_scenery.Built.Count} islands, {rocks} boulders on this peer";
            return _scenery.Built.Count > 0 && rocks > 0 ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckNearTree()
        {
            var body = _subject.Body;
            if (body == null) return ScenarioCheck.Fail("no subject");
            if (NearestTree(body.position) is not { } tree) return ScenarioCheck.Fail("no island tree");

            float distance = Level(body.position - tree.Root.position).magnitude;
            string detail = $"{distance:0.00} m from {tree.Root.name}";
            return distance <= StandOff + 0.4f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckTreeLogs(string count)
        {
            var body = _subject.Body;
            if (body == null) return ScenarioCheck.Fail("no subject");
            if (NearestTree(body.position) is not { } tree) return ScenarioCheck.Fail("no island tree");

            int expected = int.Parse(count, CultureInfo.InvariantCulture);
            string detail = $"{tree.Pile.Available} logs on {tree.Root.name} on this peer";
            return tree.Pile.Available == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The subject stands in a cave's chamber, under island rock.</summary>
        private ScenarioCheck CheckInCave()
        {
            var body = _subject.Body;
            if (body == null) return ScenarioCheck.Fail("no subject");
            if (NearestCave(body.position) is not { } found) return ScenarioCheck.Fail("no cave standing on this peer");

            var (island, cave) = found;
            float fromMiddle = Level(body.position - (island.Top + cave.Chamber)).magnitude;
            bool roofed = Physics.Raycast(body.position, Vector3.up, out var hit, cave.Height + cave.Wall + 4f, ~0, QueryTriggerInteraction.Ignore)
                          && hit.collider.GetComponentInParent<SkyIslandBody>() != null;
            string detail = $"{fromMiddle:0.0} m from the middle of a {cave}, {(roofed ? $"rock {hit.distance:0.0} m overhead" : "no rock overhead")}";
            return fromMiddle < cave.Radius && roofed ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IEnumerable<(Transform Root, RegrowingItemSourceNetworkMediator Pile)> Trees() =>
            Object.FindObjectsByType<RegrowingItemSourceNetworkMediator>()
                .Where(pile => pile.IsSpawned && pile.transform.root.name.StartsWith(TreePrefix))
                .Select(pile => (pile.transform.root, pile));

        /// <summary>
        /// The tree this peer chose (placed the subject at, or faced), while it stands; otherwise the nearest. Two trees
        /// can stand about as close, so a later step must not switch to the other.
        /// </summary>
        private (Transform Root, RegrowingItemSourceNetworkMediator Pile)? NearestTree(Vector3 from)
        {
            if (_chosen != null && _chosen.IsSpawned) return (_chosen.transform.root, _chosen);
            var nearest = Trees().OrderBy(t => (t.Root.position - from).sqrMagnitude).FirstOrDefault();
            return nearest.Root == null ? null : nearest;
        }

        private (SkyIslandSpec Island, SkyIslandCave Cave)? NearestCave(Vector3 from)
        {
            if (_islands == null || _islandConfig == null) return null;
            (SkyIslandSpec, SkyIslandCave)? best = null;
            float bestDistance = float.PositiveInfinity;
            var sites = new SkyIslandSites();
            foreach (var island in _islands.Standing)
            {
                _siteProcessor.Sites(island, _islandConfig.SiteRules, sites);
                foreach (var cave in sites.Caves)
                {
                    float distance = (island.Top + cave.Chamber - from).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = (island, cave);
                }
            }
            return best;
        }

        /// <summary>From a point on an island toward that island's middle, level.</summary>
        private Vector3 InwardOf(Vector3 point)
        {
            var island = _islands?.Standing.OrderBy(i => Level(i.Top - point).sqrMagnitude).Select(i => (SkyIslandSpec?)i).FirstOrDefault();
            var inward = island is { } found ? Level(found.Top - point) : Vector3.forward;
            return inward.sqrMagnitude < 0.01f ? Vector3.forward : inward.normalized;
        }

        private IAirshipView? Ship() => _actors.GetActors<IAirshipView>().FirstOrDefault(ship => ship.Transform != null);

        private static Vector3 Level(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
