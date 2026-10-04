#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using TinCan.Features.SkyIslands;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Probes for sky islands, on the peer that runs them. Every peer builds its own islands from the replicated session
    /// layout, so each checks its own: built from the voyage's seed, exactly the islands the layout wants around the ship's cell,
    /// clear of the ship, and solid (a ray down onto one hits its collider). <c>RecordIslandSeed</c> and
    /// <c>IslandSeedChanged</c> see a new layout however fast it arrives.
    /// </summary>
    public sealed class SkyIslandsScenarioLibrary : IScenarioLibrary
    {
        private const float ShipClearance = 50f;

        private readonly IActorRegistry _actors;
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly ISkyIslands? _islands;
        private readonly SkyIslandConfig? _config;
        private readonly SkyIslandLayoutProcessor _layout = new();
        private int _recordedSeed;

        public SkyIslandsScenarioLibrary(IActorRegistry actors, ScenarioSubject subject, INetworkService network, IObjectResolver resolver)
        {
            _actors = actors;
            _subject = subject;
            _network = network;
            resolver.TryResolve(out _islands);
            resolver.TryResolve(out _config);
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("RecordIslandSeed", _ => Record()),
            new ScenarioCommand("LookAtNearestIsland", _ => LookAtNearest())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("IslandsFromVoyage", _ => CheckFromVoyage()),
            new ScenarioProbe("IslandSeedChanged", _ => CheckSeedChanged()),
            new ScenarioProbe("IslandsMatchLayout", _ => CheckMatchLayout()),
            new ScenarioProbe("IslandsClearOfShip", _ => CheckClearOfShip()),
            new ScenarioProbe("IslandSolid", _ => CheckSolid())
        };

        private ScenarioCheck Record()
        {
            if (_islands == null) return Missing();
            _recordedSeed = _islands.Seed;
            return ScenarioCheck.Pass($"island seed {_recordedSeed} recorded");
        }

        /// <summary>Subject peer: turns the subject's camera toward the nearest main island, for a checkpoint that shows it.</summary>
        private ScenarioCheck LookAtNearest()
        {
            if (_islands == null) return Missing();
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            if (subject?.Look == null || body == null) return ScenarioCheck.Fail("no subject look view");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var nearest = _islands.Standing
                .OrderBy(island => island.IsSatellite)
                .ThenBy(island => (island.Top - body.position).sqrMagnitude)
                .Select(island => (Spec: island, Built: GameObject.Find(SkyIslandBuilder.NameOf(island.Id))))
                .FirstOrDefault(found => found.Built != null);
            if (nearest.Built == null) return ScenarioCheck.Fail("no island object to look at");

            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, nearest.Built.transform);
            float distance = (nearest.Spec.Top - body.position).magnitude;
            return ScenarioCheck.Pass($"looking at {nearest.Spec.Id}, {distance:0} m away (pitch {pitch:0}, yaw {yaw:0})");
        }

        private ScenarioCheck CheckFromVoyage()
        {
            if (_islands == null) return Missing();
            var session = _actors.GetActors<ISessionLayout>().FirstOrDefault(layout => layout.LayoutSeed != 0);
            if (session == null) return ScenarioCheck.Fail("no voyage layout on this peer yet");

            string detail = $"islands seed {_islands.Seed}, voyage seed {session.LayoutSeed}, {_islands.StandingCount} standing, " +
                            $"{_islands.Pending} pending on this peer";
            bool built = _islands.Seed == session.LayoutSeed && _islands.Pending == 0 && _islands.StandingCount > 0;
            return built ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckSeedChanged()
        {
            if (_islands == null) return Missing();
            string detail = $"island seed {_islands.Seed} ({_recordedSeed} recorded) on this peer";
            return _islands.Seed != 0 && _islands.Seed != _recordedSeed ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The islands standing are exactly the ones the layout wants around the ship (as the streaming last saw it).</summary>
        private ScenarioCheck CheckMatchLayout()
        {
            if (_islands == null || _config == null) return Missing();
            var ship = Ship();
            var session = _actors.GetActors<ISessionLayout>().FirstOrDefault(layout => layout.LayoutSeed == _islands.Seed);
            if (ship == null || session == null) return ScenarioCheck.Fail("no ship or no voyage layout matching the islands");

            var wanted = new List<SkyIslandSpec>();
            var centre = _layout.CellCentre(_layout.CellOf(ship.Transform.position, _config.CellSize), _config.CellSize);
            _layout.Around(session.LayoutSeed, centre, _config.StreamRadius, _config.LayoutRules,
                new[] { session.Origin, session.Destination }, wanted);
            var standing = new HashSet<SkyIslandId>(_islands.Standing.Select(island => island.Id));
            int missing = wanted.Count(island => !standing.Contains(island.Id));
            int extra = standing.Count(id => wanted.All(island => !island.Id.Equals(id)));

            string detail = $"{standing.Count} standing, {wanted.Count} wanted, {missing} missing, {extra} extra on this peer";
            return missing == 0 && extra == 0 ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckClearOfShip()
        {
            if (_islands == null) return Missing();
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            Vector3 at = ship.Transform.position;
            float closest = float.MaxValue;
            foreach (var island in _islands.Standing)
            {
                float gap = new Vector2(island.Top.x - at.x, island.Top.z - at.z).magnitude - island.Radius;
                closest = Mathf.Min(closest, gap);
            }

            string detail = $"nearest island edge {closest:0} m from the ship (level) on this peer";
            return closest >= ShipClearance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>A ray straight down onto the nearest island's top hits that island's collider.</summary>
        private ScenarioCheck CheckSolid()
        {
            if (_islands == null) return Missing();
            var ship = Ship();
            if (ship == null || _islands.StandingCount == 0) return ScenarioCheck.Fail("no ship or no islands");

            Vector3 at = ship.Transform.position;
            var nearest = _islands.Standing.OrderBy(island => (island.Top - at).sqrMagnitude).First();
            var origin = nearest.Top + Vector3.up * 200f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 200f + nearest.Depth, ~0, QueryTriggerInteraction.Ignore))
                return ScenarioCheck.Fail($"a ray down onto {nearest.Id} hit nothing; {Describe(nearest)}");

            string detail = $"a ray down onto {nearest.Id} hit {hit.collider.name} at y {hit.point.y:0.0} (top {nearest.Top.y:0.0})";
            return hit.collider.name == SkyIslandBuilder.NameOf(nearest.Id) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>What the scene holds for an island, for a failure report.</summary>
        private static string Describe(SkyIslandSpec island)
        {
            var root = GameObject.Find("SkyIslands");
            if (root == null) return "no SkyIslands root in the scene";
            var built = root.transform.Find(SkyIslandBuilder.NameOf(island.Id));
            if (built == null) return $"root has {root.transform.childCount} children, none for {island.Id}";
            var collider = built.GetComponent<MeshCollider>();
            string mesh = collider != null && collider.sharedMesh != null ? $"{collider.sharedMesh.vertexCount} vertices" : "no mesh";
            return $"object at {built.position} active {built.gameObject.activeInHierarchy}, collider " +
                   $"{(collider != null && collider.enabled ? "on" : "off")} ({mesh}, bounds {collider?.bounds}), scene {built.gameObject.scene.name}";
        }

        private IAirshipView? Ship() =>
            _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);

        private static ScenarioCheck Missing() => ScenarioCheck.Fail("SkyIslands feature not loaded in this profile");
    }
}
