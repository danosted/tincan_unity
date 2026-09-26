#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.HumanoidMovement;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Samples the locally controlled humanoid every frame into a <see cref="MovementResponseTracker"/>. It logs a
    /// summary line periodically and writes a JSON report when the bot route completes, or on shutdown if no route
    /// ran. Positions are taken in the local space of the platform the movement layer says the player is on.
    /// </summary>
    public sealed class MovementTelemetryUseCase : IInitializable, ILateTickable, IDisposable
    {
        private const string LogSource = "NetTelemetry";
        private const float MaxLegalSpeed = 20f;
        private const float LogInterval = 10f;
        private const float MovingSpeed = 0.3f;
        private const float MovingTurnRate = 2f;

        private readonly HarnessOptions _options;
        private readonly HarnessSession _session;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;
        private readonly NetworkManager _networkManager;
        private readonly IEventPublisher _events;

        private readonly MovementResponseTracker _tracker = new(MaxLegalSpeed);
        private Transform? _platform;
        private Vector3 _platformPosition;
        private Quaternion _platformRotation;
        private float _startTime = -1f;
        private float _lastLog;
        private float _rttSum;
        private float _rttMax;
        private int _rttSamples;
        private bool _written;
        private NetHarnessOverlayView? _overlay;

        public MovementTelemetryUseCase(
            HarnessOptions options,
            HarnessSession session,
            IActorRegistry registry,
            INetworkService network,
            NetworkManager networkManager,
            IEventPublisher events)
        {
            _options = options;
            _session = session;
            _registry = registry;
            _network = network;
            _networkManager = networkManager;
            _events = events;
        }

        public void Initialize()
        {
            if (!_options.IsActive) return;

            _session.RouteCompleted += WriteReport;
            _overlay = NetHarnessOverlayView.Create(DescribeLive);
        }

        public void LateTick()
        {
            if (!_options.TelemetryEnabled || _written || !_network.IsActive) return;

            var player = _registry.GetLocalPlayerActor<IHumanoidCharacterView>();
            if (player == null) return;

            float now = Time.unscaledTime;
            if (_startTime < 0f)
            {
                _startTime = now;
                _lastLog = now;
            }

            _tracker.Add(Sample(player, now));
            SampleRtt();

            if (now - _lastLog < LogInterval) return;

            _lastLog = now;
            _events.LogInfo(LogSource, DescribeLive().Replace("\n", " | "));
        }

        public void Dispose()
        {
            _session.RouteCompleted -= WriteReport;
            if (_options.TelemetryEnabled && _tracker.Frames > 0) WriteReport();
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay.gameObject);
        }

        /// <summary>A platform counts as moving above a small linear speed or turn rate, so a hovering ship reads as still.</summary>
        public static bool IsPlatformMoving(Vector3 previousPosition, Quaternion previousRotation, Vector3 position, Quaternion rotation, float deltaTime)
        {
            if (deltaTime <= 0f) return false;

            float speed = Vector3.Distance(previousPosition, position) / deltaTime;
            float turnRate = Quaternion.Angle(previousRotation, rotation) / deltaTime;
            return speed > MovingSpeed || turnRate > MovingTurnRate;
        }

        private MovementSample Sample(IHumanoidCharacterView player, float now)
        {
            var movement = player.Movement;
            Vector3 world = movement.Transform.position;
            Transform? platform = movement.CurrentGround.MovingGroundTransform;

            bool frameChanged = platform != _platform;
            bool platformMoving = false;
            Vector3 local = world;

            if (platform != null)
            {
                local = Quaternion.Inverse(platform.rotation) * (world - platform.position);
                platformMoving = !frameChanged &&
                    IsPlatformMoving(_platformPosition, _platformRotation, platform.position, platform.rotation, Time.unscaledDeltaTime);
                _platformPosition = platform.position;
                _platformRotation = platform.rotation;
            }

            _platform = platform;

            var input = player.InputState;
            return new MovementSample(now, input.MovementDirection.sqrMagnitude > 0.01f, input.IsJumping, local, frameChanged, platformMoving);
        }

        private void SampleRtt()
        {
            if (_network.IsServer || _networkManager.NetworkConfig.NetworkTransport == null) return;

            float rtt = _networkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            _rttSum += rtt;
            _rttMax = Math.Max(_rttMax, rtt);
            _rttSamples++;
        }

        private string Role => _network.IsHost ? "host" : _network.IsServer ? "server" : $"client{_network.LocalClientId}";

        private string DescribeLive()
        {
            var still = _tracker.Summarise(false);
            var moving = _tracker.Summarise(true);
            float rtt = _rttSamples > 0 ? _rttSum / _rttSamples : 0f;
            return $"NetHarness {Role} | route {_session.RouteName ?? "-"} [{_session.CurrentStep}] | netsim {_options.NetworkPreset ?? "none"} | rtt {rtt:0} ms\n" +
                   $"ship still  start {still.start}\n            stop {still.stop}  jump {still.jump}\n" +
                   $"            snaps {still.snaps} (max {still.maxSnapM:0.00} m)  reversals {still.reversals}\n" +
                   $"ship moving start {moving.start}\n            stop {moving.stop}  jump {moving.jump}\n" +
                   $"            snaps {moving.snaps} (max {moving.maxSnapM:0.00} m)  reversals {moving.reversals}";
        }

        private void WriteReport()
        {
            if (_written || _tracker.Frames == 0) return;
            _written = true;

            float duration = Time.unscaledTime - _startTime;
            var report = new MovementTelemetryReport
            {
                role = Role,
                route = _session.RouteName ?? "none",
                preset = _options.NetworkPreset ?? "none",
                startedUtc = DateTime.UtcNow.AddSeconds(-duration).ToString("o"),
                routeCompleted = _session.IsRouteComplete,
                durationS = (float)Math.Round(duration, 1),
                frames = _tracker.Frames,
                avgFps = duration > 0f ? (float)Math.Round(_tracker.Frames / duration, 1) : 0f,
                avgRttMs = _rttSamples > 0 ? (float)Math.Round(_rttSum / _rttSamples, 1) : 0f,
                maxRttMs = _rttMax,
                shipStill = _tracker.Summarise(false),
                shipMoving = _tracker.Summarise(true)
            };

            string json = JsonUtility.ToJson(report, true);
            try
            {
                string directory = HarnessPaths.TelemetryDirectory();
                Directory.CreateDirectory(directory);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string path = Path.Combine(directory, $"{report.role}-{report.route}-{report.preset}-{stamp}.json");
                File.WriteAllText(path, json);
                File.WriteAllText(Path.Combine(directory, $"latest-{report.role}.json"), json);
                _events.LogInfo(LogSource, $"Report written to {path}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _events.LogWarning(LogSource, $"Could not write report: {exception.Message}");
            }

            _events.LogInfo(LogSource, "Report " + JsonUtility.ToJson(report, false));
        }
    }
}
