#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Items;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Application Layer, server: tying a bundle and setting camp things down. A press of <see cref="CampingConfig.PlaceInput"/>
    /// (an ability input bit, read from the predicted input stream like the grappling hook's, so no RPC) does what the
    /// player's carrying tag says: a log is tied into a campfire bundle; a bundle or tent roll is set down where the
    /// player aims, if it fits there (<see cref="CampPlacementFinder"/>, the same answer the preview shows), as a campfire
    /// (unlit) or a pitched tent. Presses are read during humanoid movement and carried out after it, on the equipment.
    /// </summary>
    public sealed class CampPlacementUseCase : IHumanoidMotionContributor, ISimulationTickable
    {
        private const string LogSource = "Camping";

        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly CampPlacementFinder _finder;
        private readonly ICampSpawner _spawner;
        private readonly CampingConfig _config;
        private readonly IEventPublisher _events;

        private readonly HashSet<Guid> _pressHeld = new();
        private readonly Dictionary<Guid, uint> _lastSequence = new();
        private readonly List<(Guid Player, CampPlacement? Place)> _pending = new();

        public CampPlacementUseCase(INetworkService network, IActorRegistry actors, CampPlacementFinder finder, ICampSpawner spawner,
            CampingConfig config, IEventPublisher events)
        {
            _network = network;
            _actors = actors;
            _finder = finder;
            _spawner = spawner;
            _config = config;
            _events = events;
        }

        public void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step)
        {
            if (!_network.IsServer || _config.PlaceInput == null) return;
            int bit = _config.PlaceInput.BitIndex;
            if (bit < 0) return;

            // Each input once, in order: a release the host sees only as a replay still counts, and an input seen
            // again (a replay of one already read) never presses twice.
            Guid id = character.Id;
            if (_lastSequence.TryGetValue(id, out uint last) && input.Sequence <= last) return;
            _lastSequence[id] = input.Sequence;

            bool held = (input.ActiveInputMask & (1UL << bit)) != 0;
            bool pressed = held && _pressHeld.Add(id);
            if (!held) _pressHeld.Remove(id);
            if (!pressed) return;

            if (Holds(character, _config.CarryingFirewoodTag))
            {
                _pending.Add((character.Id, null));
                return;
            }

            var kind = CampPlacementProcessor.Placing(Holds(character, _config.CarryingBundleTag), Holds(character, _config.CarryingTentTag));
            if (kind is not { } placing) return;

            var movement = character.Movement;
            var body = movement.Transform;
            var eye = body.position + body.up * movement.EyeHeight;
            var facing = HumanoidTargeter.Facing(input.LookRotation, movement.CurrentGround.MovingGroundTransform, body.rotation);
            var placement = _finder.Find(placing, eye, CampPlacementProcessor.AimDirection(facing, input.LookPitch), body);
            if (placement.Fits) _pending.Add((character.Id, placement));
            else _events.LogInfo(LogSource, $"No room for a {placing.ToString().ToLowerInvariant()} there ({placement.Verdict}).");
        }

        public void Tick()
        {
            if (!_network.IsServer)
            {
                _pending.Clear();
                return;
            }

            foreach (var (player, place) in _pending)
            {
                if (!_actors.TryGetActor(player, out var actor) || EquipmentLocator.Resolve(actor) is not { } equipment) continue;
                if (place is { } placement) Place(player, equipment, placement);
                else Bundle(player, equipment);
            }
            _pending.Clear();
        }

        private void Bundle(Guid player, IEquipment equipment)
        {
            var firewood = _config.FirewoodItem;
            var bundle = _config.BundleItem;
            if (firewood == null || bundle == null || !equipment.IsHolding(firewood) || !equipment.TryUnequip()) return;
            if (!equipment.TryEquip(bundle))
            {
                equipment.TryEquip(firewood);
                return;
            }
            _events.Publish(new CampfireBundledEvent(player));
        }

        private void Place(Guid player, IEquipment equipment, CampPlacement placement)
        {
            var item = placement.Kind == CampPlaceable.Tent ? _config.TentItem : _config.BundleItem;
            if (item == null || !equipment.IsHolding(item)) return;
            if (_spawner.Spawn(placement.Kind, placement.Point, placement.Rotation) == null) return;

            equipment.TryUnequip();
            _events.Publish(new CampPlacedEvent(player, placement.Kind, placement.Point));
        }

        private static bool Holds(IHumanoidCharacterView character, Core.Domain.Abilities.Tags.GameplayTag? tag) =>
            tag != null && character.HasTag(tag);
    }
}
