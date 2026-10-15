#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Application Layer: the grappling hook, inside the predicted movement simulation (server and predicting owner).
    /// While a player holds the hook (its carrying tag), pressing the fire input casts a ray along their look; where it
    /// catches becomes their anchor. Holding the input reels them toward it (<see cref="GrappleHookProcessor"/>);
    /// releasing it, arriving, the line snapping or putting the hook away lets go. Nothing here is an RPC: the input bit
    /// and the tag are already in the predicted stream, so owner and server reach the same anchor from the same input.
    /// A replay pulls for the inputs the anchor covered and changes nothing. After humanoid movement the server
    /// publishes the lines to <see cref="IGrappleLineState"/> so every peer can draw them.
    /// </summary>
    public sealed class GrappleHookUseCase : IHumanoidMotionContributor, ISimulationTickable, IGrappleHooks
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private readonly GrappleHookConfig _config;
        private readonly GrappleHookProcessor _processor;
        private readonly IGrappleHookQuery _query;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IEventPublisher _events;

        // The latest anchor per player, kept after it lets go so a replay of older inputs still pulls.
        private readonly Dictionary<Guid, GrappleAnchor> _anchors = new();
        private readonly HashSet<Guid> _fireHeld = new();
        private readonly List<GrappleLineSnapshot> _lines = new();
        private IGrappleLineState? _publishedTo;
        private bool _linesChanged = true;

        public GrappleHookUseCase(GrappleHookConfig config, GrappleHookProcessor processor, IGrappleHookQuery query,
            INetworkService network, IActorRegistry actors, IEventPublisher events)
        {
            _config = config;
            _processor = processor;
            _query = query;
            _network = network;
            _actors = actors;
            _events = events;
        }

        public bool TryGetHookPoint(Guid playerId, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (!_anchors.TryGetValue(playerId, out var anchor) || !anchor.IsHolding || !anchor.IsValid) return false;
            worldPoint = anchor.WorldPoint;
            return true;
        }

        public void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step)
        {
            var tag = _config.CarryingTag;
            int bit = _config.FireInput != null ? _config.FireInput.BitIndex : -1;
            if (tag == null || bit < 0) return;

            bool fireHeld = (input.ActiveInputMask & (1UL << bit)) != 0;
            if (step.IsReplay)
            {
                Replay(character, input, fireHeld, ref step);
                return;
            }

            Guid id = character.Id;
            bool firePressed = fireHeld && _fireHeld.Add(id);
            if (!fireHeld) _fireHeld.Remove(id);

            if (!character.HasTag(tag))
            {
                Release(id, input.Sequence, "hook put away");
                return;
            }

            if (!fireHeld)
            {
                Release(id, input.Sequence, "released");
                return;
            }

            if (firePressed) TryAttach(character, input);
            if (!_anchors.TryGetValue(id, out var anchor) || !anchor.IsHolding) return;
            if (!anchor.IsValid)
            {
                Release(id, input.Sequence, "lost its hold");
                return;
            }

            var (velocity, outcome) = _processor.Reel(step.Momentum, character.Movement.Transform.position, anchor.WorldPoint, _config, step.DeltaTime);
            step.Velocity = velocity;
            switch (outcome)
            {
                case GrappleReelOutcome.Arrived:
                    Release(id, input.Sequence + 1, "arrived");
                    break;
                case GrappleReelOutcome.Snapped:
                    Release(id, input.Sequence, "line snapped");
                    break;
            }
        }

        /// <summary>Server, after humanoid movement: publishes the lines when they changed (or the state fixture did).</summary>
        public void Tick()
        {
            if (!_network.IsServer) return;

            var state = _actors.GetActors<IGrappleLineState>().FirstOrDefault(candidate => candidate.IsSimulating);
            if (state == null) return;
            if (!_linesChanged && ReferenceEquals(state, _publishedTo)) return;

            _lines.Clear();
            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                if (_anchors.TryGetValue(player.Id, out var anchor) && anchor.IsHolding && anchor.IsValid)
                {
                    _lines.Add(new GrappleLineSnapshot(player, anchor.Frame, anchor.WorldPoint));
                }
            }

            state.ServerSetLines(_lines);
            _publishedTo = state;
            _linesChanged = false;
        }

        // Owner re-simulating an input after a correction: pull exactly as the live step did for it, change nothing.
        private void Replay(IHumanoidCharacterView character, HumanoidInputState input, bool fireHeld, ref HumanoidMotionStep step)
        {
            if (!fireHeld || !_anchors.TryGetValue(character.Id, out var anchor) || !anchor.IsValid || !anchor.CoversInput(input.Sequence)) return;
            step.Velocity = _processor.Reel(step.Momentum, character.Movement.Transform.position, anchor.WorldPoint, _config, step.DeltaTime).Velocity;
        }

        private void TryAttach(IHumanoidCharacterView character, HumanoidInputState input)
        {
            var movement = character.Movement;
            var body = movement.Transform;
            Vector3 eye = body.position + body.up * movement.EyeHeight;
            Quaternion facing = HumanoidTargeter.Facing(input.LookRotation, movement.CurrentGround.MovingGroundTransform, body.rotation);
            Vector3 direction = GrappleHookProcessor.AimDirection(facing, input.LookPitch);

            if (!_query.TryCatch(eye, direction, _config.MaxRange, _config.HookableLayers, body, out var frame, out var point)) return;

            _anchors[character.Id] = new GrappleAnchor(frame, frame.InverseTransformPoint(point), input.Sequence);
            _linesChanged = true;
            if (_network.IsServer) _events.Publish(new GrappleHookAttachedEvent(character.Id, frame.name, Vector3.Distance(eye, point)));
        }

        private void Release(Guid id, uint sequence, string reason)
        {
            if (!_anchors.TryGetValue(id, out var anchor) || !anchor.IsHolding) return;

            _anchors[id] = anchor.ReleasedAt(sequence);
            _linesChanged = true;
            if (_network.IsServer) _events.Publish(new GrappleHookReleasedEvent(id, reason));
        }
    }
}
