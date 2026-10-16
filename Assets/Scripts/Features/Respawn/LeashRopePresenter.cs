#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Presentation, every peer, per frame: a rope from the ship's leash point to each player carrying the leashed tag
    /// (replicated, so late joiners see it too). Which point is the nearest one; a ship has one today. Plain local objects,
    /// none on a headless server.
    /// </summary>
    public sealed class LeashRopePresenter : ITickable, IDisposable
    {
        private const float RopeHeight = 0.5f; // where the rope meets the body, above its centre

        private readonly RespawnRules _rules;
        private readonly IActorRegistry _actors;
        private readonly bool _drawsVisuals = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        private readonly Dictionary<Guid, LineRenderer> _ropes = new();
        private readonly HashSet<Guid> _drawn = new();

        public LeashRopePresenter(RespawnRules rules, IActorRegistry actors)
        {
            _rules = rules;
            _actors = actors;
        }

        /// <summary>The rope drawn for <paramref name="playerId"/> this frame, if any (tests and scenarios).</summary>
        public LineRenderer? RopeOf(Guid playerId) =>
            _ropes.TryGetValue(playerId, out var rope) && rope != null && rope.gameObject.activeSelf ? rope : null;

        public void Tick()
        {
            if (!_drawsVisuals || _rules.LeashedTag == null) return;

            _drawn.Clear();
            var points = _actors.GetActors<ILeashPoint>().Where(point => point.IsSimulating).ToList();
            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                var body = player.Movement?.Transform;
                if (body == null || points.Count == 0 || !player.HasTag(_rules.LeashedTag)) continue;

                var point = points.OrderBy(p => (p.Anchor.position - body.position).sqrMagnitude).First();
                Draw(player.Id, point.Anchor.position, body.position + body.up * RopeHeight);
                _drawn.Add(player.Id);
            }

            foreach (var pair in _ropes)
            {
                if (!_drawn.Contains(pair.Key) && pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            foreach (var rope in _ropes.Values)
            {
                if (rope == null) continue;
                if (Application.isPlaying) Object.Destroy(rope.gameObject);
                else Object.DestroyImmediate(rope.gameObject);
            }

            _ropes.Clear();
        }

        private void Draw(Guid playerId, Vector3 from, Vector3 to)
        {
            if (!_ropes.TryGetValue(playerId, out var rope) || rope == null)
            {
                rope = new GameObject($"LeashRope_{playerId:N}").AddComponent<LineRenderer>();
                rope.useWorldSpace = true;
                rope.positionCount = 2;
                rope.widthMultiplier = _rules.RopeWidth;
                rope.shadowCastingMode = ShadowCastingMode.Off;
                rope.receiveShadows = false;
                if (_rules.RopeMaterial != null) rope.sharedMaterial = _rules.RopeMaterial;
                _ropes[playerId] = rope;
            }

            rope.gameObject.SetActive(true);
            rope.SetPosition(0, from);
            rope.SetPosition(1, to);
        }
    }
}
