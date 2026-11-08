#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>
    /// Application Layer, server only, after airship movement. Keeps the gates in the sky. When the ship's solid body
    /// comes within a gate's radius, the gate's actions run (<see cref="GateDefinition.Actions"/>) and the gate is used
    /// up: it does nothing more. Where gates go is up to the feature that lays them. Plan: .docs/plans/hazards-and-boons.md
    /// (Gates).
    /// </summary>
    public class GateUseCase : ISimulationTickable, IGates, ISessionParticipant
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Gates";

        private sealed class Live
        {
            public IGate Gate = null!;
            public GateDefinition Definition = null!;
        }

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IEventPublisher _events;
        private readonly IGateSpawner _spawner;
        private readonly IShipOverlapQuery _overlap;
        private readonly AbilitySystemUseCase _abilities;
        private readonly List<Live> _live = new();
        private readonly List<IGate> _gates = new();

        public GateUseCase(INetworkService network, IActorRegistry actors, IEventPublisher events, IGateSpawner spawner,
            IShipOverlapQuery overlap, AbilitySystemUseCase abilities)
        {
            _network = network;
            _actors = actors;
            _events = events;
            _spawner = spawner;
            _overlap = overlap;
            _abilities = abilities;
        }

        public IReadOnlyList<IGate> Gates => _gates;
        public int Passed { get; private set; }

        /// <summary>Session start: every gate leaves the sky.</summary>
        public void ResetForSession()
        {
            if (!_network.IsServer) return;
            foreach (var live in _live) _spawner.Despawn(live.Gate);
            _live.Clear();
            _gates.Clear();
        }

        public void SetSessionActive(bool active) { }

        public IGate? Spawn(GateDefinition definition, Vector3 position)
        {
            if (!_network.IsServer || definition.Prefab == null) return null;
            var gate = _spawner.Spawn(definition.Prefab, position);
            if (gate == null) return null;

            _live.Add(new Live { Gate = gate, Definition = definition });
            _gates.Add(gate);
            return gate;
        }

        public void Despawn(IGate gate)
        {
            if (!_network.IsServer) return;
            _live.RemoveAll(live => live.Gate == gate);
            _gates.Remove(gate);
            _spawner.Despawn(gate);
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            if (_live.RemoveAll(live => live.Gate.Transform == null) > 0)
            {
                _gates.Clear();
                foreach (var live in _live) _gates.Add(live.Gate);
            }

            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Transform != null);
            if (ship != null) Pass(ship);
        }

        private void Pass(IAirshipView ship)
        {
            foreach (var live in _live)
            {
                var transform = live.Gate.Transform;
                if (live.Gate.IsUsed || transform == null || !_overlap.Overlaps(ship, transform.position, live.Definition.Radius)) continue;

                live.Gate.ServerMarkUsed();
                Passed++;
                var context = new GateActionContext(ship, live.Gate, _abilities);
                foreach (var action in live.Definition.Actions) action?.Run(context);

                string name = live.Definition.name;
                _events.Publish(new GatePassedEvent(name, Passed));
                _events.LogInfo(LogSource, $"The ship passed through {name} ({Passed} so far).");
            }
        }
    }
}
