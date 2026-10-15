#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Presentation, every peer, per frame: draws each hooked player's line from the hook in their hands (the held
    /// visual's <see cref="GrappleHookConfig.LineStartName"/> child) to where it holds. This peer's own simulation knows
    /// the local player's hook (predicted on a client) and, on the server, everyone's; other players' lines come from
    /// the replicated <see cref="IGrappleLineState"/>. Plain local objects, never networked; none on a headless server.
    /// </summary>
    public sealed class GrappleLinePresenter : ITickable, IDisposable
    {
        private readonly GrappleHookConfig _config;
        private readonly IGrappleHooks _hooks;
        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly bool _drawsVisuals = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        private readonly Dictionary<Guid, LineRenderer> _lines = new();
        private readonly Dictionary<Guid, Transform> _starts = new();
        private readonly HashSet<Guid> _drawn = new();

        public GrappleLinePresenter(GrappleHookConfig config, IGrappleHooks hooks, IActorRegistry actors, INetworkService network)
        {
            _config = config;
            _hooks = hooks;
            _actors = actors;
            _network = network;
        }

        /// <summary>The line drawn for <paramref name="playerId"/> this frame, if any (tests and scenarios).</summary>
        public LineRenderer? LineOf(Guid playerId) =>
            _lines.TryGetValue(playerId, out var line) && line != null && line.gameObject.activeSelf ? line : null;

        public void Tick()
        {
            if (!_drawsVisuals) return;

            _drawn.Clear();
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                var body = player.Movement?.Transform;
                if (body == null || !TryGetHookPoint(player, local, out var hookPoint)) continue;

                Draw(player.Id, LineStart(Start(player.Id, body), body, player.Movement!.EyeHeight), hookPoint);
                _drawn.Add(player.Id);
            }

            foreach (var pair in _lines)
            {
                if (!_drawn.Contains(pair.Key) && pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
        }

        /// <summary>Where a line starts: the hook's tip in the player's hands, else just below their eyes.</summary>
        public static Vector3 LineStart(Transform? tip, Transform body, float eyeHeight) =>
            tip != null ? tip.position : body.position + body.up * (eyeHeight * 0.7f);

        public void Dispose()
        {
            foreach (var line in _lines.Values)
            {
                if (line == null) continue;
                if (Application.isPlaying) Object.Destroy(line.gameObject);
                else Object.DestroyImmediate(line.gameObject);
            }

            _lines.Clear();
        }

        private bool TryGetHookPoint(IHumanoidCharacterView player, IHumanoidCharacterView? local, out Vector3 hookPoint)
        {
            if (_network.IsServer || ReferenceEquals(player, local)) return _hooks.TryGetHookPoint(player.Id, out hookPoint);

            foreach (var state in _actors.GetActors<IGrappleLineState>())
            {
                if (state.TryGetHookPoint(player, out hookPoint)) return true;
            }

            hookPoint = default;
            return false;
        }

        private Transform? Start(Guid playerId, Transform body)
        {
            if (_starts.TryGetValue(playerId, out var cached) && cached != null && cached.gameObject.activeInHierarchy) return cached;

            var tip = body.FindDescendant(_config.LineStartName);
            if (tip == null || !tip.gameObject.activeInHierarchy) return null;
            _starts[playerId] = tip;
            return tip;
        }

        private void Draw(Guid playerId, Vector3 from, Vector3 to)
        {
            if (!_lines.TryGetValue(playerId, out var line) || line == null)
            {
                line = CreateLine(playerId);
                _lines[playerId] = line;
            }

            line.gameObject.SetActive(true);
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private LineRenderer CreateLine(Guid playerId)
        {
            var line = new GameObject($"GrappleLine_{playerId:N}").AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = _config.LineWidth;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (_config.LineMaterial != null) line.sharedMaterial = _config.LineMaterial;
            return line;
        }
    }
}
