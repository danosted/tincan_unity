#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The holes in the cloud sea (<see cref="ICloudHoles"/>), every peer. Reads the replicated list on the CloudHolesState
    /// fixture (<see cref="ICloudHoleState"/>) and works out each hole's shape at the shared time; on the server it opens
    /// holes and drops the ones that have shut, each tick. Plan: .docs/plans/cloud-sea.md.
    /// </summary>
    public class CloudHolesUseCase : ICloudHoles, ISimulationTickable
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly CloudHoleProcessor _processor;
        private readonly CloudBoundaryConfig _config;
        private readonly List<CloudHoleShape> _shapes = new();
        private float _shapesAt = float.NaN;
        private int _shapesFrom = -1;

        public CloudHolesUseCase(INetworkService network, IActorRegistry actors, ITimeService time,
            CloudHoleProcessor processor, CloudBoundaryConfig config)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _processor = processor;
            _config = config;
        }

        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        public IReadOnlyList<CloudHole> Holes => State?.Holes ?? Array.Empty<CloudHole>();

        public IReadOnlyList<CloudHoleShape> ShapesNow
        {
            get
            {
                IReadOnlyList<CloudHole> holes = Holes;
                float now = _time.Time;
                // Asked many times a tick (every ship and player), so worked out once per moment and list.
                if (now.Equals(_shapesAt) && holes.Count == _shapesFrom) return _shapes;

                _shapes.Clear();
                foreach (CloudHole hole in holes) _shapes.Add(_processor.Shape(hole, now, _config.HoleRim));
                _shapesAt = now;
                _shapesFrom = holes.Count;
                return _shapes;
            }
        }

        public bool TryOpen(CloudHole hole)
        {
            ICloudHoleState? state = State;
            if (!_network.IsServer || state == null || state.Holes.Count >= ICloudHoles.MaxHoles) return false;

            state.ServerAdd(hole);
            _shapesAt = float.NaN;
            return true;
        }

        public void Tick()
        {
            ICloudHoleState? state = State;
            if (!_network.IsServer || state == null) return;

            for (int i = state.Holes.Count - 1; i >= 0; i--)
            {
                if (_processor.HasClosed(state.Holes[i], _time.Time)) state.ServerRemoveAt(i);
            }
        }

        private ICloudHoleState? State => _actors.GetActors<ICloudHoleState>().FirstOrDefault(state => state.IsSimulating);
    }
}
