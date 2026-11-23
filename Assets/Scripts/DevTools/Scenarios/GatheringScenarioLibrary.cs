#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Items;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for fuel gathering (plan: fuel-gathering.md). Commands: stand the subject in front of the crystal
    /// vein nearest the ship (server), turn the subject's camera to it (subject peer). Probes: how many veins have spawned,
    /// the subject stands by that vein, how many crystals it holds, and how many the ship's crystal bin holds (replicated).
    /// </summary>
    public sealed class GatheringScenarioLibrary : IScenarioLibrary
    {
        // Where PlaceSubjectAtVein stands the subject: this far in front of the vein, within Interact reach.
        private const float StandInFront = 1.6f;
        private const float NearTolerance = 0.8f;
        // A vein is the core regrowing item source that gives fuel crystals.
        private const string CrystalItemName = "ITEM_FuelCrystal";

        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private Component? _chosen;

        public GatheringScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, IHumanoidRespawnService respawn)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _respawn = respawn;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlaceSubjectAtVein", _ => PlaceAtVein()),
            new ScenarioCommand("FaceVein", _ => FaceVein())
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("VeinsSpawned", CheckSpawned),
            new ScenarioProbe("SubjectAtVein", _ => CheckAtVein()),
            new ScenarioProbe("VeinCrystals", CheckCrystals),
            new ScenarioProbe("BinCrystals", CheckBin)
        };

        /// <summary>Server: stands the subject in front of the vein nearest the ship, facing it.</summary>
        private ScenarioCheck PlaceAtVein()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var vein = Vein();
            if (subject == null || body == null || vein == null) return ScenarioCheck.Fail("no subject or no vein");

            var front = vein.transform.forward;
            Vector3 stand = ScenarioPlacement.OnGround(body, vein.transform.position + front * StandInFront, Vector3.up);
            _respawn.ResetCharacter(subject, stand, Quaternion.LookRotation(-front, Vector3.up));
            return ScenarioCheck.Pass($"subject placed in front of {vein.name} at {stand}");
        }

        /// <summary>Subject peer: turns the subject's camera toward the vein's Interact volume.</summary>
        private ScenarioCheck FaceVein()
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var vein = Vein();
            if (subject?.Look == null || body == null || vein == null) return ScenarioCheck.Fail("no subject look view, or no vein");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var volume = vein.transform.FindDescendant("InteractVolume") ?? vein.transform;
            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, volume);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward {vein.name}");
        }

        private ScenarioCheck CheckSpawned(string atLeast)
        {
            int veins = Veins().Count();
            string detail = $"{veins} vein(s) spawned";
            return veins >= int.Parse(atLeast, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckAtVein()
        {
            var body = _subject.Body;
            var vein = Vein();
            if (body == null || vein == null) return ScenarioCheck.Fail("no subject or no vein");

            float distance = Vector3.ProjectOnPlane(body.position - vein.transform.position, Vector3.up).magnitude;
            string detail = $"{distance:0.00} m from the vein";
            return Mathf.Abs(distance - StandInFront) <= NearTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The chosen vein holds exactly this many crystals on this peer.</summary>
        private ScenarioCheck CheckCrystals(string expected)
        {
            if (Vein() is not IRegrowingItemSource vein) return ScenarioCheck.Fail("no vein");
            string detail = $"{vein.Available} crystal(s)";
            return vein.Available == int.Parse(expected, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The ship's crystal bin holds exactly this many crystals on this peer.</summary>
        private ScenarioCheck CheckBin(string expected)
        {
            var bin = Object.FindObjectsByType<ItemStockpileNetworkMediator>(FindObjectsSortMode.None).FirstOrDefault(store => store.IsSpawned);
            if (bin == null) return ScenarioCheck.Fail("no crystal bin spawned");
            string detail = $"{bin.Count}/{bin.Capacity} crystal(s) in the bin";
            return bin.Count == int.Parse(expected, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private System.Collections.Generic.IEnumerable<Component> Veins() =>
            Object.FindObjectsByType<RegrowingItemSourceNetworkMediator>(FindObjectsSortMode.None)
                .Where(source => source.IsSpawned && source.YieldItem != null && source.YieldItem.name == CrystalItemName);

        /// <summary>
        /// The vein the steps mean: once the subject has stood by one, that vein, kept while it stands (the same on every
        /// peer, whatever has spawned there yet, and still that one after the subject is sent aboard); before that, the
        /// vein nearest the ship, where PlaceSubjectAtVein sends them.
        /// </summary>
        private Component? Vein()
        {
            if (_chosen is RegrowingItemSourceNetworkMediator kept && kept != null && kept.IsSpawned) return kept;

            if (NearestTo(_subject.Body?.position) is { } nearSubject &&
                (nearSubject.transform.position - _subject.Body!.position).sqrMagnitude < 25f)
            {
                return _chosen = nearSubject;
            }

            return NearestTo(_actors.GetActors<TinCan.Core.Ship.IAirshipView>().FirstOrDefault()?.Transform?.position);
        }

        private Component? NearestTo(Vector3? point) => point is { } at
            ? Veins().OrderBy(vein => (vein.transform.position - at).sqrMagnitude).FirstOrDefault()
            : null;
    }
}
