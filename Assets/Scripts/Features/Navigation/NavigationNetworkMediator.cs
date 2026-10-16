#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Ship;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Infrastructure Layer: a ship's route, on the NavigationState ship fixture (with an EntityNetworkMediator, which
    /// registers it as an actor). Server-written lists and variables carry the waypoints, the active one, the first leg's
    /// start and the discovered points of interest to every peer, late joiners included. A server RPC carries a
    /// navigator's new route to the server, which checks it (<see cref="NavigationUseCase"/>).
    /// </summary>
    public class NavigationNetworkMediator : NetworkBehaviour, IRouteState
    {
        private NetworkList<RouteWaypoint> _waypoints = null!;
        private NetworkList<int> _discovered = null!;

        private readonly NetworkVariable<int> _activeIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _origin = new(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly Queue<RouteRequest> _requests = new();
        private ActorIdentity? _identity;

        private void Awake()
        {
            _waypoints = new NetworkList<RouteWaypoint>();
            _discovered = new NetworkList<int>();
        }

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        // Fixtures are parented to the ship root after they spawn, so look the ship up when asked.
        public IAirshipView? Ship => this != null ? GetComponentInParent<IAirshipView>() : null;

        public int Count => _waypoints.Count;
        public RouteWaypoint Waypoint(int index) => _waypoints[index];
        public int ActiveIndex => _waypoints.Count == 0 ? -1 : Mathf.Clamp(_activeIndex.Value, 0, _waypoints.Count - 1);
        public Vector3 Origin => _origin.Value;
        public int DiscoveredCount => _discovered.Count;

        public bool IsDiscovered(int poiId) => _discovered.Contains(poiId);

        public void RequestRoute(IReadOnlyList<RouteWaypoint> waypoints, int activeIndex)
        {
            if (!IsSpawned) return;
            var copy = new RouteWaypoint[waypoints.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = waypoints[i];
            RequestRouteRpc(copy, activeIndex);
        }

        public bool TryConsumeRouteRequest(out RouteRequest request) => _requests.TryDequeue(out request);

        public void ServerSetRoute(IReadOnlyList<RouteWaypoint> waypoints, int activeIndex, Vector3 origin)
        {
            if (!IsServer) return;
            _waypoints.Clear();
            foreach (var waypoint in waypoints) _waypoints.Add(waypoint);
            _origin.Value = origin;
            _activeIndex.Value = waypoints.Count == 0 ? -1 : Mathf.Clamp(activeIndex, 0, waypoints.Count - 1);
        }

        public void ServerSetWaypoint(int index, RouteWaypoint waypoint)
        {
            if (!IsServer || index < 0 || index >= _waypoints.Count || _waypoints[index].Equals(waypoint)) return;
            _waypoints[index] = waypoint;
        }

        public void ServerSetActiveIndex(int index)
        {
            if (IsServer && _activeIndex.Value != index) _activeIndex.Value = index;
        }

        public void ServerDiscover(int poiId)
        {
            if (IsServer && !_discovered.Contains(poiId)) _discovered.Add(poiId);
        }

        public void ServerClearDiscovered()
        {
            if (IsServer && _discovered.Count > 0) _discovered.Clear();
        }

        [Rpc(SendTo.Server)]
        private void RequestRouteRpc(RouteWaypoint[] waypoints, int activeIndex, RpcParams rpcParams = default) =>
            _requests.Enqueue(new RouteRequest(rpcParams.Receive.SenderClientId, waypoints, activeIndex));
    }
}
