#nullable enable
using System;
using System.IO;
using TinCan.Core.Ship;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Frame pacing on screen during a scripted run (.docs/NETWORK_TEST_HARNESS.md, "Frame pacing"). Each frame it
    /// samples the nearest ship's simulated pose (in Update, before the host draws it between ticks), its drawn pose and
    /// the camera (after LateUpdate), and logs a <c>[FramePacing]</c> line every 10 s and when play ends.
    /// <para>
    /// A number in <c>Logs/dev-frame-cap.txt</c> caps the frame rate (VSync off) to stand in for a display of that
    /// rate; the Game view's own VSync toggle wins over it, so untick it for a sweep. Delete the file afterwards.
    /// </para>
    /// </summary>
    public sealed class FramePacingProbe : IInitializable, ITickable, IPostLateTickable, IDisposable
    {
        public const string FrameCapFile = "Logs/dev-frame-cap.txt";
        private const float ReportEvery = 10f;

        private readonly NetworkManager _network;
        private readonly FramePacingMeter _shipSimulated = new();
        private readonly FramePacingMeter _shipDrawn = new();
        private readonly FramePacingMeter _camera = new();
        private AirshipControllerView[] _ships = Array.Empty<AirshipControllerView>();
        private AirshipControllerView? _ship;
        private float _nextReport = ReportEvery;
        private float _nextShipSearch;
        private float _frameSeconds;
        private int _frames;
        private int _cap;
        private string _role = "offline";
        private bool _reported;

        public FramePacingProbe(NetworkManager network) => _network = network;

        public void Initialize()
        {
            if (!File.Exists(FrameCapFile) || !int.TryParse(File.ReadAllText(FrameCapFile).Trim(), out _cap) || _cap <= 0) return;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _cap;
            Debug.LogWarning($"[FramePacing] Frame rate capped at {_cap} fps by {FrameCapFile} (dev sweep). Delete the file to remove the cap.");
        }

        public void Tick()
        {
            FindShip();
            if (_ship != null) _shipSimulated.Sample(_ship.transform.position, Time.deltaTime);
        }

        public void PostLateTick()
        {
            if (_ship != null) _shipDrawn.Sample(_ship.transform.position, Time.deltaTime);

            var camera = ViewCamera();
            if (camera != null) _camera.Sample(camera.transform.position, Time.deltaTime);

            if (_network.IsListening) _role = _network.IsHost ? "host" : _network.IsServer ? "server" : "client";
            _reported = false; // a frame since the last report: the next one says something new
            _frames++;
            _frameSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime < _nextReport) return;
            _nextReport = Time.unscaledTime + ReportEvery;
            Report();
        }

        public void Dispose()
        {
            if (_reported) return;
            Report();
        }

        private void Report()
        {
            _reported = true;
            float fps = _frameSeconds > 0f ? _frames / _frameSeconds : 0f;
            Debug.Log($"[FramePacing] {_role} {fps:F0} fps{(_cap > 0 ? $" (cap {_cap})" : string.Empty)}: " +
                      $"ship simulated {_shipSimulated.Describe()}; ship drawn {_shipDrawn.Describe()}; camera {_camera.Describe()}");
        }

        /// <summary>The camera drawing the screen: the deepest enabled one that renders to it (the player's is not tagged MainCamera).</summary>
        private static Camera? ViewCamera()
        {
            Camera? best = null;
            foreach (var camera in Camera.allCameras)
            {
                if (camera.targetTexture != null) continue;
                if (best == null || camera.depth > best.depth) best = camera;
            }

            return best;
        }

        private void FindShip()
        {
            if (Time.unscaledTime < _nextShipSearch && _ship != null) return;
            _nextShipSearch = Time.unscaledTime + 1f;
            _ships = UnityEngine.Object.FindObjectsByType<AirshipControllerView>(FindObjectsSortMode.None);

            var camera = ViewCamera();
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            float nearest = float.MaxValue;
            _ship = null;
            foreach (var ship in _ships)
            {
                float distance = (ship.transform.position - eye).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                _ship = ship;
            }
        }
    }
}
