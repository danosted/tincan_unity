#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Gates;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Application Layer, server only. When a voyage begins it lays a boon gate (<see cref="IGates"/>) at each of its
    /// placements (<see cref="BoonGateMap"/>), skipping any inside an obstacle such as island rock, and removes the last
    /// voyage's. What a gate does when the ship passes is its definition's business (a boon: an effect on the ship).
    /// Plan: .docs/plans/hazards-and-boons.md (B1).
    /// </summary>
    public class BoonGateUseCase : ISimulationTickable, ISessionParticipant
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Boons";

        private readonly INetworkService _network;
        private readonly IEventPublisher _events;
        private readonly IGates _gates;
        private readonly BoonsConfig _config;
        private readonly BoonGateMap _map;
        private readonly IReadOnlyList<IWorldObstacleQuery> _obstacles;
        private readonly List<IGate> _laid = new();
        private int _laidForSeed;

        public BoonGateUseCase(INetworkService network, IEventPublisher events, IGates gates, BoonsConfig config, BoonGateMap map,
            IReadOnlyList<IWorldObstacleQuery> obstacles)
        {
            _network = network;
            _events = events;
            _gates = gates;
            _config = config;
            _map = map;
            _obstacles = obstacles;
        }

        /// <summary>Session start: the gates leave with the session (<see cref="GateUseCase"/>); the next voyage lays its own.</summary>
        public void ResetForSession()
        {
            _laid.Clear();
            _laidForSeed = 0;
        }

        public void SetSessionActive(bool active) { }

        public void Tick()
        {
            if (!_network.IsServer) return;

            var placements = _map.Gates;
            if (_map.Seed == _laidForSeed) return;

            foreach (var gate in _laid) _gates.Despawn(gate);
            _laid.Clear();
            _laidForSeed = _map.Seed;
            foreach (var placement in placements)
            {
                var definition = _config.GateAt(placement.GateIndex);
                if (definition == null || IsBlocked(placement, definition.Radius)) continue;

                var gate = _gates.Spawn(definition, placement.Position);
                if (gate != null) _laid.Add(gate);
            }
            if (_laidForSeed != 0) _events.LogInfo(LogSource, $"Laid {_laid.Count} boon gate(s) for voyage {_laidForSeed}.");
        }

        private bool IsBlocked(BoonGatePlacement placement, float radius)
        {
            foreach (var obstacle in _obstacles)
            {
                if (obstacle.IsBlocked(placement.Position, radius)) return true;
            }
            return false;
        }
    }
}
