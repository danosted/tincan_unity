#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Infrastructure Layer: the players' hook lines on the GrappleHookState ship fixture (with an EntityNetworkMediator,
    /// which registers it as an actor). A server-written list, so every peer and every late joiner sees who is hooked
    /// where. A point on a networked object (the ship) is kept in that object's local space, so each peer draws it on its
    /// own view of the ship.
    /// </summary>
    public class GrappleHookStateNetworkMediator : NetworkBehaviour, IGrappleLineState
    {
        private NetworkList<GrappleLine> _lines = null!;
        private ActorIdentity? _identity;

        private void Awake() => _lines = new NetworkList<GrappleLine>();

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public void ServerSetLines(IReadOnlyList<GrappleLineSnapshot> lines)
        {
            if (!IsServer) return;

            _lines.Clear();
            foreach (var snapshot in lines)
            {
                if (TryReplicate(snapshot, out var line)) _lines.Add(line);
            }
        }

        public bool TryGetHookPoint(IActor player, out Vector3 worldPoint)
        {
            worldPoint = default;
            var playerObject = SpawnedObjectOf(player as Component);
            if (playerObject == null) return false;

            foreach (var line in _lines)
            {
                if (line.Player.NetworkObjectId != playerObject.NetworkObjectId) continue;
                if (!line.HasFrame)
                {
                    worldPoint = line.Point;
                    return true;
                }

                if (!line.Frame.TryGet(out var frame) || frame == null) return false;
                worldPoint = frame.transform.TransformPoint(line.Point);
                return true;
            }

            return false;
        }

        private static bool TryReplicate(GrappleLineSnapshot snapshot, out GrappleLine line)
        {
            line = default;
            var playerObject = SpawnedObjectOf(snapshot.Player as Component);
            if (playerObject == null) return false;

            line.Player = playerObject;
            var frameObject = SpawnedObjectOf(snapshot.Frame);
            if (frameObject == null)
            {
                line.Point = snapshot.WorldPoint;
                return true;
            }

            line.HasFrame = true;
            line.Frame = frameObject;
            line.Point = frameObject.transform.InverseTransformPoint(snapshot.WorldPoint);
            return true;
        }

        private static NetworkObject? SpawnedObjectOf(Component? component)
        {
            if (component == null) return null;
            var networkObject = component.GetComponentInParent<NetworkObject>();
            return networkObject != null && networkObject.IsSpawned ? networkObject : null;
        }

        public override void OnDestroy()
        {
            _lines?.Dispose();
            base.OnDestroy();
        }
    }
}
