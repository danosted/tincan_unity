#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.SkyIslands;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for island impact.
    /// <list type="bullet">
    /// <item><c>PlaceIslandIntoBow</c> (server) builds an island just ahead of the ship, then moves it back until its rock
    /// overlaps the hull by a few metres, as if the ship had flown into it.</item>
    /// <item><c>ShipHitIsland</c> and <c>ShipOutOfRock</c> read the server's impact and contact query.</item>
    /// <item><c>ShipHurt</c> reads the ship's replicated health, so it answers on any peer.</item>
    /// </list>
    /// </summary>
    public sealed class SkyIslandImpactScenarioLibrary : IScenarioLibrary
    {
        private const float Overlap = 3f;
        private static readonly SkyIslandSpec Rock = new(new SkyIslandId(-1, 0, 0, 0), Vector3.zero, 45f, 70f, 4242u);

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ISkyIslandBuilder? _builder;
        private readonly ISkyIslandContactQuery? _contact;
        private readonly SkyIslandImpactUseCase? _impact;

        public SkyIslandImpactScenarioLibrary(INetworkService network, IActorRegistry actors, IObjectResolver resolver)
        {
            _network = network;
            _actors = actors;
            resolver.TryResolve(out _builder);
            resolver.TryResolve(out _contact);
            resolver.TryResolve(out _impact);
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceIslandIntoBow", _ => PlaceIntoBow())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipHitIsland", _ => CheckHit()),
            new ScenarioProbe("ShipOutOfRock", _ => CheckOutOfRock()),
            new ScenarioProbe("ShipHurt", _ => CheckHurt())
        };

        private ScenarioCheck PlaceIntoBow()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            if (_builder == null) return Missing();
            var ship = Ship();
            var root = (ship as Component)?.transform ?? ship?.Transform;
            if (ship == null || root == null || !SolidBounds(root, out var hull)) return ScenarioCheck.Fail("no ship with solid colliders");

            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            Vector3 middle = hull.center;
            float bow = hull.extents.magnitude;

            // Build it clear ahead, with the ship's middle height a few metres under its top.
            var top = middle + forward * (bow + Rock.Radius + 20f) + Vector3.up * 8f;
            var rock = new SkyIslandSpec(Rock.Id, top, Rock.Radius, Rock.Depth, Rock.ShapeSeed);
            _builder.Build(rock);
            var built = GameObject.Find(SkyIslandBuilder.NameOf(rock.Id));
            if (built == null) return ScenarioCheck.Fail("the island was not built");
            Physics.SyncTransforms();

            // Measure the gap along the ship's middle line: hull surface forward to the rock.
            if (!FirstHit(middle, forward, 400f, hit => hit.collider.GetComponent<SkyIslandBody>() != null, out var rockHit))
                return ScenarioCheck.Fail("no rock ahead of the ship's middle");
            // The middle line can pass over the deck; then the hull's bounds give the bow.
            float gap = FirstHit(rockHit.point, -forward, 400f, hit => hit.collider.transform.IsChildOf(root), out var hullHit)
                ? hullHit.distance
                : rockHit.distance - Reach(hull, forward);
            built.transform.position -= forward * (gap + Overlap);
            Physics.SyncTransforms();
            return ScenarioCheck.Pass($"island placed {Overlap} m into the bow (it was {gap:0.0} m ahead)");
        }

        private ScenarioCheck CheckHit()
        {
            if (_impact == null) return Missing();
            string detail = $"{_impact.Hits} island hit(s) on the server";
            return _impact.Hits > 0 ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckOutOfRock()
        {
            if (_contact == null) return Missing();
            var ship = Ship();
            if (ship == null) return ScenarioCheck.Fail("no ship");
            return _contact.TryPushOut(ship, out var push, out var island)
                ? ScenarioCheck.Fail($"still {push.magnitude:0.00} m inside island {island}")
                : ScenarioCheck.Pass("the ship is clear of the rock");
        }

        private ScenarioCheck CheckHurt()
        {
            var controller = (Ship() as IShipState)?.Controller;
            if (!controller.TryGetHealth(out var health)) return ScenarioCheck.Fail("no ship health");
            string detail = $"ship health {health.Health:0} / {health.MaxHealth:0} on this peer";
            return health.IsDamaged ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private static bool SolidBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (any) bounds.Encapsulate(collider.bounds);
                else bounds = collider.bounds;
                any = true;
            }
            return any;
        }

        /// <summary>How far the bounds reach from their centre along a direction.</summary>
        private static float Reach(Bounds bounds, Vector3 direction) =>
            Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.y) * bounds.extents.y + Mathf.Abs(direction.z) * bounds.extents.z;

        private static bool FirstHit(Vector3 from, Vector3 direction, float distance, System.Func<RaycastHit, bool> wanted, out RaycastHit first)
        {
            var hits = Physics.RaycastAll(from, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            first = hits.Where(wanted).OrderBy(hit => hit.distance).FirstOrDefault();
            return first.collider != null;
        }

        private IAirshipView? Ship() =>
            _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);

        private static ScenarioCheck Missing() => ScenarioCheck.Fail("SkyIslands feature not loaded in this profile");
    }
}
