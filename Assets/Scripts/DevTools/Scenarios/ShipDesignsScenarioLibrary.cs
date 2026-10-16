#nullable enable
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for designed ships. Commands: FlyDesign gives every ship a stored design (server, as the shipyard's
    /// Launch does); PlaceSubjectAtCell "x,y,z,yaw" stands the subject on whatever is under that ship cell, its body
    /// turned yaw degrees from the bow (server); FaceShipYaw "yaw" turns the subject's camera the same way, level
    /// (subject peer). Probes: ShipDesignBuilt: this peer has built the ship from the design its state replicates, with
    /// at least the given number of parts standing. SubjectOnShip: the ground under the subject on this peer belongs to
    /// the ship (a built part or the helm). ShipTopSpeed: the ship's replicated top speed, set from the design's parts
    /// (S4). SubjectFeetAbove "y": the subject's feet are at least that high in ship space on this peer. ShipFrameBuilt "n":
    /// at least n generated frame meshes (planks and beams per story) stand for the ship on this peer. SubjectStandsOn
    /// "prefix": the ground under the subject is a part of the ship whose name starts with the prefix ("Planks" for a frame
    /// floor). Plans: .docs/plans/modular-airship-builder.md, .docs/plans/airship-parts-pack.md, .docs/plans/ship-frames.md.
    /// </summary>
    public sealed class ShipDesignsScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly IActorRegistry _actors;
        private readonly IShipAssembly _assembly;
        private readonly INetworkService _network;
        private readonly IHumanoidRespawnService _respawn;
        private readonly IShipDesignStore _store;
        private readonly IShipDesigns _designs;

        public ShipDesignsScenarioLibrary(ScenarioSubject subject, IActorRegistry actors, IShipAssembly assembly, INetworkService network,
            IHumanoidRespawnService respawn, IShipDesignStore store, IShipDesigns designs)
        {
            _subject = subject;
            _actors = actors;
            _assembly = assembly;
            _network = network;
            _respawn = respawn;
            _store = store;
            _designs = designs;
        }

        public System.Collections.Generic.IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("FlyDesign", FlyDesign),
            new ScenarioCommand("PlaceSubjectAtCell", PlaceAtCell),
            new ScenarioCommand("FaceShipYaw", FaceShipYaw),
        };

        public System.Collections.Generic.IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("ShipDesignBuilt", CheckBuilt),
            new ScenarioProbe("SubjectOnShip", _ => CheckOnShip()),
            new ScenarioProbe("ShipTopSpeed", CheckTopSpeed),
            new ScenarioProbe("SubjectFeetAbove", CheckFeetAbove),
            new ScenarioProbe("ShipFrameBuilt", CheckFrameBuilt),
            new ScenarioProbe("SubjectStandsOn", CheckStandsOn),
        };

        private ScenarioCheck CheckBuilt(string minimumParts)
        {
            var state = _actors.GetActors<IShipDesignState>().FirstOrDefault(s => s.Ship != null);
            if (state == null) return ScenarioCheck.Fail("no ship design state on a ship");
            if (state.DesignHash == 0) return ScenarioCheck.Fail("the ship has no design yet");
            if (!_assembly.TryGetBuilt(state.Ship!.Id, out var hash, out var parts)) return ScenarioCheck.Fail("nothing built yet");

            string detail = $"built {ShipDesignHash.ToText(hash)} with {parts} parts (replicated {ShipDesignHash.ToText(state.DesignHash)})";
            bool enough = parts >= int.Parse(string.IsNullOrEmpty(minimumParts) ? "1" : minimumParts, CultureInfo.InvariantCulture);
            return hash == state.DesignHash && enough ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The ship's top speed on this peer (its replicated flight speed attribute) is the given m/s, within 0.2.</summary>
        private ScenarioCheck CheckTopSpeed(string metresPerSecond)
        {
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (ship == null) return ScenarioCheck.Fail("no ship");

            float expected = float.Parse(metresPerSecond, CultureInfo.InvariantCulture);
            string detail = $"top speed {ship.MaxForwardSpeed:0.00} m/s";
            return Mathf.Abs(ship.MaxForwardSpeed - expected) <= 0.2f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckOnShip()
        {
            var body = _subject.Body;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var from = body.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(from, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                return ScenarioCheck.Fail($"nothing under the subject at {body.position}");
            }

            string detail = $"standing on {hit.collider.name}, {hit.distance - 0.5f:0.00} m below the feet pivot";
            return hit.collider.transform.IsChildOf(ship.Transform) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckFrameBuilt(string minimumMeshes)
        {
            var state = _actors.GetActors<IShipDesignState>().FirstOrDefault(s => s.Ship != null);
            if (state == null) return ScenarioCheck.Fail("no ship design state on a ship");

            int meshes = _assembly.BuiltFrameMeshes(state.Ship!.Id);
            string detail = $"{meshes} frame meshes built";
            return meshes >= int.Parse(minimumMeshes, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckStandsOn(string namePrefix)
        {
            var body = _subject.Body;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var from = body.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(from, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                return ScenarioCheck.Fail($"nothing under the subject at {body.position}");
            }

            string detail = $"standing on {hit.collider.name}";
            bool match = hit.collider.transform.IsChildOf(ship.Transform) && hit.collider.name.StartsWith(namePrefix, System.StringComparison.Ordinal);
            return match ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server, as the shipyard's Launch: the stored design becomes the selected one and every ship flies it.</summary>
        private ScenarioCheck FlyDesign(string name)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var loaded = _store.Load(name);
            if (!loaded.Succeeded) return ScenarioCheck.Fail($"\"{name}\" does not load: {loaded.Error}");

            _designs.Select(loaded.Design!);
            var states = _actors.GetActors<IShipDesignState>().Where(s => s.Ship != null).ToList();
            foreach (var state in states)
            {
                if (!_designs.TryApply(state, loaded.Design!, out var error)) return ScenarioCheck.Fail(error ?? "refused");
            }

            return states.Count > 0
                ? ScenarioCheck.Pass($"{states.Count} ship(s) fly \"{name}\" ({loaded.Design!.Parts.Count} parts)")
                : ScenarioCheck.Fail("no ship to give it to");
        }

        /// <summary>Server: "x,y,z,yaw". Stands the subject on the ground under that ship cell, through the respawn service.</summary>
        private ScenarioCheck PlaceAtCell(string argument)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var values = argument.Split(',').Select(v => float.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (values.Length != 4) return ScenarioCheck.Fail($"'{argument}' is not x,y,z,yaw");

            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (subject == null || body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var cell = new Vector3(values[0], values[1], values[2]) * ShipGrid.CellSize;
            var stand = ScenarioPlacement.OnGround(body, ship.Transform.TransformPoint(cell), ship.Transform.up);
            _respawn.ResetCharacter(subject, stand, ship.Transform.rotation * Quaternion.Euler(0f, values[3], 0f));
            return ScenarioCheck.Pass($"subject placed at {ship.Transform.InverseTransformPoint(stand)} in ship space");
        }

        /// <summary>Subject peer: the camera looks level, turned the given degrees from the ship's bow.</summary>
        private ScenarioCheck FaceShipYaw(string degrees)
        {
            var subject = _subject.Resolve();
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (subject?.Look == null || ship == null) return ScenarioCheck.Fail("no subject look view or no ship");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var forward = ship.Transform.rotation * Quaternion.Euler(0f, float.Parse(degrees, CultureInfo.InvariantCulture), 0f) * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            subject.Look.ApplyLook(0f, yaw);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, level");
        }

        /// <summary>This peer: the subject's feet (the bottom of its capsule) are at least this high in ship space.</summary>
        private ScenarioCheck CheckFeetAbove(string height)
        {
            var body = _subject.Body;
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault();
            if (body == null || ship == null) return ScenarioCheck.Fail("no subject or no ship");

            var controller = body.GetComponent<CharacterController>();
            float rootAboveFeet = controller == null ? 1f : (controller.height * 0.5f - controller.center.y) * body.lossyScale.y;
            float feet = ship.Transform.InverseTransformPoint(body.position).y - rootAboveFeet;
            string detail = $"feet at {feet:0.00} m in ship space";
            return feet >= float.Parse(height, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
