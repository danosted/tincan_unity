#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.DevTools.Perf
{
    /// <summary>
    /// Measures this instance for <c>-perf</c> runs: once the session is up it waits out the warm-up, then records a
    /// window of per-frame costs (frame and main-thread time, GC allocation, physics, network tick, draw calls) and
    /// per-second rates (bytes and messages on the wire, memory, live network objects), and writes a
    /// <see cref="PerfReport"/>. Counter names are the ones Unity 6000.4 registers (checked against
    /// ProfilerRecorderHandle.GetAvailable); most need a development build and are listed as unavailable otherwise.
    /// Recording does not allocate while the series have capacity.
    /// </summary>
    public sealed class PerfSampler : IPostLateTickable, IDisposable
    {
        private const string LogSource = "Perf";
        private const string SentPrefix = "NetworkMessageManager.SerializeAndEnqueue.";
        private const string ReceivedPrefix = "NetworkMessageManager.DeserializeAndHandle.";
        private const float NanosecondsPerMillisecond = 1_000_000f;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float MaxPlausibleFrameMs = 1000f;

        // Unity 6000.4 has no single draw-call counter; it splits them by path. The total is their sum.
        private static readonly string[] DrawCallCounters =
        {
            "Standard Draw Calls Count", "Standard Indirect Draw Calls Count", "Standard Instanced Draw Calls Count",
            "SRP Batcher Draw Calls Count", "BRG Draw Calls Count", "BRG Indirect Draw Calls Count"
        };

        private enum Phase { WaitingForSession, WarmingUp, Measuring, Done }

        private readonly PerfOptions _options;
        private readonly INetworkService _network;
        private readonly NetworkManager _networkManager;
        private readonly IEventPublisher _events;
        private readonly IRandomSource _random;
        private readonly IActorRegistry _actors;

        private readonly List<Probe> _probes = new();
        private readonly Probe _mainThread;
        private readonly Probe _gpu;
        private readonly Probe _gcAllocated;
        private readonly Probe _gcAllocations;
        private readonly Probe _gcCollect;
        private readonly Probe _physics;
        private readonly Probe _physicsQueries;
        private readonly Probe _networkTick;
        private readonly Probe _setPass;
        private readonly Probe _triangles;
        private readonly Probe _gcUsed;
        private readonly Probe _totalUsed;
        private readonly List<Probe> _drawCalls = new();
        private readonly List<MessageProbe> _messages = new();
        private readonly HashSet<string> _messageNames = new();
        private readonly List<ProfilerRecorderHandle> _handles = new();

        private readonly List<PerfSeries> _series = new();
        private readonly PerfSeries _frameMs;
        private readonly PerfSeries _mainThreadMs;
        private readonly PerfSeries _gpuMs;
        private readonly PerfSeries _gcAllocBytes;
        private readonly PerfSeries _gcAllocCount;
        private readonly PerfSeries _physicsMs;
        private readonly PerfSeries _physicsQueryCount;
        private readonly PerfSeries _tickMs;
        private readonly PerfSeries _drawCallCount;
        private readonly PerfSeries _setPassCount;
        private readonly PerfSeries _triangleCount;
        private readonly PerfSeries _txBytesPerSecond;
        private readonly PerfSeries _rxBytesPerSecond;
        private readonly PerfSeries _txPacketsPerSecond;
        private readonly PerfSeries _rxPacketsPerSecond;
        private readonly PerfSeries _sentMessagesPerSecond;
        private readonly PerfSeries _receivedMessagesPerSecond;
        private readonly PerfSeries _gcUsedMb;
        private readonly PerfSeries _totalUsedMb;
        private readonly PerfSeries _networkObjects;
        private readonly PerfSeries _ngoRttMs;
        private readonly PerfSeries _timeLeadMs;
        private readonly PerfSeries _localBufferMs;

        private Phase _phase = Phase.WaitingForSession;
        private float _phaseStart;
        private float _lastSecond;
        private float _lastScan;
        private int _frames;
        private long _gcCollections;
        private TrafficTotals _lastTraffic;
        private long _lastSent;
        private long _lastReceived;
        private long _sentTotal;
        private long _receivedTotal;
        private int _profiledFrames;
        private bool _profiling;

        public PerfSampler(PerfOptions options, INetworkService network, NetworkManager networkManager, IEventPublisher events,
            IRandomSource random, IActorRegistry actors)
        {
            _options = options;
            _network = network;
            _networkManager = networkManager;
            _events = events;
            _random = random;
            _actors = actors;

            _mainThread = Track("CPU Main Thread Frame Time");
            _gpu = Track("GPU Frame Time");
            _gcAllocated = Track("GC Allocated In Frame");
            _gcAllocations = Track("GC Allocation In Frame Count");
            _gcCollect = Track("GC.Collect");
            _physics = Track("FixedUpdate.PhysicsFixedUpdate");
            _physicsQueries = Track("Physics Queries");
            _networkTick = Track("NetworkTickSystem.Tick");
            _setPass = Track("SetPass Calls Count");
            _triangles = Track("Triangles Count");
            _gcUsed = Track("GC Used Memory");
            _totalUsed = Track("Total Used Memory");
            foreach (var counter in DrawCallCounters) _drawCalls.Add(Track(counter));

            int frames = (int)(_options.DurationSeconds * 240f) + 64;
            int seconds = (int)_options.DurationSeconds + 8;
            _frameMs = Series("frame_ms", "ms", frames);
            _mainThreadMs = Series("main_thread_ms", "ms", frames);
            _gpuMs = Series("gpu_ms", "ms", frames);
            _gcAllocBytes = Series("gc_alloc_bytes", "B/frame", frames);
            _gcAllocCount = Series("gc_alloc_count", "allocs/frame", frames);
            _physicsMs = Series("physics_ms", "ms/frame", frames);
            _physicsQueryCount = Series("physics_queries", "queries/frame", frames);
            _tickMs = Series("tick_ms", "ms/tick", frames);
            _drawCallCount = Series("draw_calls", "calls/frame", frames);
            _setPassCount = Series("setpass_calls", "calls/frame", frames);
            _triangleCount = Series("triangles", "tris/frame", frames);
            _txBytesPerSecond = Series("tx_bytes_s", "B/s", seconds);
            _rxBytesPerSecond = Series("rx_bytes_s", "B/s", seconds);
            _txPacketsPerSecond = Series("tx_packets_s", "packets/s", seconds);
            _rxPacketsPerSecond = Series("rx_packets_s", "packets/s", seconds);
            _sentMessagesPerSecond = Series("messages_sent_s", "messages/s", seconds);
            _receivedMessagesPerSecond = Series("messages_received_s", "messages/s", seconds);
            _gcUsedMb = Series("gc_used_mb", "MB", seconds);
            _totalUsedMb = Series("total_used_mb", "MB", seconds);
            _networkObjects = Series("network_objects", "objects", seconds);
            _ngoRttMs = Series("ngo_rtt_ms", "ms", seconds);
            _timeLeadMs = Series("time_lead_ms", "ms", seconds);
            _localBufferMs = Series("local_buffer_ms", "ms", seconds);
        }

        public void PostLateTick()
        {
            float now = Time.realtimeSinceStartup;
            switch (_phase)
            {
                case Phase.WaitingForSession:
                    if (!_network.IsActive) return;
                    Enter(Phase.WarmingUp, now);
                    _events.LogInfo(LogSource, $"Session up; warming up {_options.WarmupSeconds:0.#} s, then measuring {_options.DurationSeconds:0.#} s ({_options.Label}).");
                    return;

                case Phase.WarmingUp:
                    // NGO registers a message type's marker the first time it is used, so look for new ones until the window opens.
                    if (now - _lastScan >= 1f) ScanMessageMarkers(now);
                    if (now - _phaseStart < _options.WarmupSeconds) return;
                    ScanMessageMarkers(now);
                    _lastTraffic = ReadTraffic();
                    _lastSecond = now;
                    Enter(Phase.Measuring, now);
                    StartProfile();
                    return;

                case Phase.Measuring:
                    RecordFrame();
                    if (now - _lastSecond >= 1f) RecordSecond(now);
                    if (now - _phaseStart >= _options.DurationSeconds) Finish(now, "window complete");
                    return;
            }
        }

        public void Dispose()
        {
            if (_phase == Phase.Measuring) Finish(Time.realtimeSinceStartup, "shut down before the window ended");
            foreach (var probe in _probes) probe.Dispose();
            foreach (var probe in _messages) probe.Recorder.Dispose();
        }

        private void Enter(Phase phase, float now)
        {
            _phase = phase;
            _phaseStart = now;
        }

        private void RecordFrame()
        {
            _frames++;
            if (_profiling && ++_profiledFrames >= _options.ProfileFrames) StopProfile();
            _frameMs.Add(Time.unscaledDeltaTime * 1000f);
            AddIfValid(_mainThreadMs, _mainThread, _mainThread.Last / NanosecondsPerMillisecond);
            // GPU timing arrives late and sometimes as a sentinel (negative, or absurdly large); keep plausible frames only.
            float gpu = _gpu.Last / NanosecondsPerMillisecond;
            if (_gpu.Valid && gpu > 0f && gpu < MaxPlausibleFrameMs) _gpuMs.Add(gpu);
            AddIfValid(_gcAllocBytes, _gcAllocated, _gcAllocated.Last);
            AddIfValid(_gcAllocCount, _gcAllocations, _gcAllocations.Last);
            AddIfValid(_physicsMs, _physics, _physics.Last / NanosecondsPerMillisecond);
            AddIfValid(_physicsQueryCount, _physicsQueries, _physicsQueries.Last);
            // Render counters read 0 on frames with no finished render reading (common at hundreds of fps); a rendering
            // client always draws something, so a 0 is "no reading", not "drew nothing". Headless instances record none.
            AddIfPositive(_setPassCount, _setPass);
            AddIfPositive(_triangleCount, _triangles);
            _gcCollections += _gcCollect.LastCount;

            // Per tick, not per frame: a frame runs zero, one or several network ticks.
            int ticks = _networkTick.LastCount;
            if (ticks > 0) _tickMs.Add(_networkTick.Last / NanosecondsPerMillisecond / ticks);

            long drawCalls = 0;
            bool anyDrawCounter = false;
            foreach (var probe in _drawCalls)
            {
                if (!probe.Valid) continue;
                anyDrawCounter = true;
                drawCalls += probe.Last;
            }
            if (anyDrawCounter && drawCalls > 0) _drawCallCount.Add(drawCalls);

            foreach (var message in _messages)
            {
                int count = message.Recorder.LastCount;
                if (count == 0) continue;
                message.Total += count;
                if (message.Sent) _sentTotal += count;
                else _receivedTotal += count;
            }
        }

        private void RecordSecond(float now)
        {
            float elapsed = now - _lastSecond;
            _lastSecond = now;

            var traffic = ReadTraffic();
            if (traffic.Valid && _lastTraffic.Valid)
            {
                _txBytesPerSecond.Add((traffic.TxBytes - _lastTraffic.TxBytes) / elapsed);
                _rxBytesPerSecond.Add((traffic.RxBytes - _lastTraffic.RxBytes) / elapsed);
                _txPacketsPerSecond.Add((traffic.TxPackets - _lastTraffic.TxPackets) / elapsed);
                _rxPacketsPerSecond.Add((traffic.RxPackets - _lastTraffic.RxPackets) / elapsed);
            }
            _lastTraffic = traffic;

            if (_messages.Count > 0)
            {
                _sentMessagesPerSecond.Add((_sentTotal - _lastSent) / elapsed);
                _receivedMessagesPerSecond.Add((_receivedTotal - _lastReceived) / elapsed);
                _lastSent = _sentTotal;
                _lastReceived = _receivedTotal;
            }

            AddIfValid(_gcUsedMb, _gcUsed, _gcUsed.Current / BytesPerMegabyte);
            AddIfValid(_totalUsedMb, _totalUsed, _totalUsed.Current / BytesPerMegabyte);
            var spawned = _networkManager.SpawnManager?.SpawnedObjectsList;
            if (spawned != null) _networkObjects.Add(spawned.Count);

            // Clients: the RTT NGO's time system uses, and how far the client clock runs ahead of the server's. The client
            // leads by about half the RTT plus one tick, so an inflated RTT means inputs wait in the server queue
            // (.docs/plans/input-queue-lead.md, Phase 0).
            if (!_network.IsServer && _network.IsClient)
            {
                _ngoRttMs.Add(_networkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId));
                _timeLeadMs.Add((float)((_networkManager.LocalTime.Time - _networkManager.ServerTime.Time) * 1000d));
                if (_networkManager.NetworkTimeSystem != null) _localBufferMs.Add((float)(_networkManager.NetworkTimeSystem.LocalBufferSec * 1000d));
            }
        }

        private void Finish(float now, string reason)
        {
            _phase = Phase.Done;
            StopProfile();
            var report = BuildReport(now - _phaseStart);
            string path = Write(report);
            _events.LogInfo(LogSource, $"Report ({reason}) written to {path}");
            _events.LogInfo(LogSource, report.ToSummaryLine());

            if (_options.QuitWhenDone && !Application.isEditor) Application.Quit();
        }

        private PerfReport BuildReport(float windowSeconds)
        {
            var report = new PerfReport(RoleName()) { WindowSeconds = windowSeconds, Frames = _frames };
            report.AddInfo("label", _options.Label);
            report.AddInfo("seed", _random.Seed?.ToString() ?? "none");
            report.AddInfo("startedUtc", DateTime.UtcNow.AddSeconds(-windowSeconds).ToString("o"));
            report.AddInfo("unity", Application.unityVersion);
            report.AddInfo("platform", Application.platform.ToString());
            report.AddInfo("developmentBuild", Debug.isDebugBuild ? "true" : "false");
            report.AddInfo("editor", Application.isEditor ? "true" : "false");
            report.AddInfo("batchMode", Application.isBatchMode ? "true" : "false");
            report.AddInfo("targetFrameRate", Application.targetFrameRate.ToString());
            report.AddInfo("tickRate", _networkManager.NetworkConfig.TickRate.ToString());
            report.AddInfo("warmupS", PerfReport.Number(_options.WarmupSeconds));
            report.AddInfo("cpu", SystemInfo.processorType);
            report.AddInfo("cpuCount", SystemInfo.processorCount.ToString());
            report.AddInfo("memoryMB", SystemInfo.systemMemorySize.ToString());
            report.AddInfo("gpu", SystemInfo.graphicsDeviceName);
            report.AddInfo("resolution", Application.isBatchMode ? "none" : $"{Screen.width}x{Screen.height}");
            if (_network.IsServer) report.AddInfo("connectedClients", _networkManager.ConnectedClientsIds.Count.ToString());
            report.AddInfo("gcCollections", _gcCollections.ToString());
            AddInputQueues(report);
            if (_options.ProfileFrames > 0) report.AddInfo("profiledFrames", _profiledFrames.ToString());

            float frameBudget = Application.targetFrameRate > 0 ? 1000f / Application.targetFrameRate : 1000f / 60f;
            report.AddInfo("hitchThresholdMs", PerfReport.Number(frameBudget * 2f));
            report.AddInfo("memorySlopeMbPerMin", PerfReport.Number(PerfSeries.Slope(_totalUsedMb.Samples) * 60f));
            foreach (var series in _series)
            {
                if (series.Count == 0) continue;
                report.AddMetric(series.Summarise(series == _frameMs || series == _mainThreadMs ? frameBudget * 2f : null));
            }

            foreach (var probe in _probes)
            {
                if (!probe.Valid) report.AddUnavailable(probe.Name);
            }

            foreach (var message in _messages)
            {
                string type = message.Name.Substring(message.Sent ? SentPrefix.Length : ReceivedPrefix.Length);
                report.AddMessages(type, message.Sent ? message.Total : 0, message.Sent ? 0 : message.Total);
            }

            return report;
        }

        private string Write(PerfReport report)
        {
            try
            {
                string directory = ReportDirectory();
                Directory.CreateDirectory(directory);
                string json = report.ToJson();

                // A runner passes its own folder per run; a local run keeps a timestamped copy and a latest-<role>.
                if (_options.OutputDirectory != null)
                {
                    string path = Path.Combine(directory, report.Role + ".json");
                    File.WriteAllText(path, json);
                    return path;
                }

                string stamped = Path.Combine(directory, $"{report.Role}-{_options.Label}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(stamped, json);
                File.WriteAllText(Path.Combine(directory, $"latest-{report.Role}.json"), json);
                return stamped;
            }
            catch (Exception exception)
            {
                _events.LogWarning(LogSource, $"Could not write the report: {exception.Message}");
                return "(not written)";
            }
        }

        /// <summary>
        /// Server only: each client's input queue on the server since it spawned. The server consumes one input per
        /// tick, so a standing queue depth of n delays that client's input by n ticks (perf plan, input-ack latency).
        /// </summary>
        private void AddInputQueues(PerfReport report)
        {
            if (!_network.IsServer) return;
            foreach (var actor in _actors.GetActors<IHumanoidCharacterView>())
            {
                if (actor is not IBufferedInputSource buffered || buffered.InputBufferStats.Ticks == 0) continue;
                var stats = buffered.InputBufferStats;
                ulong? owner = actor is IPossessable possessable ? possessable.OwnerId : null;
                report.AddInfo($"inputQueue.{(owner.HasValue ? "client" + owner.Value : actor.Id.ToString())}",
                    $"mean depth {stats.MeanDepth:0.00}, max {stats.MaxDepth}, starved {stats.Starved}, skipped {stats.Skipped}, ticks {stats.Ticks}");
            }
        }

        private string ReportDirectory() =>
            _options.OutputDirectory ?? Path.Combine(HarnessPaths.ProjectRoot(Application.dataPath), "Logs", "perf");

        /// <summary><c>-perfprofile</c>: a binary profiler log of the window's first frames, opened in the Editor's Profiler (Load).</summary>
        private void StartProfile()
        {
            if (_options.ProfileFrames <= 0) return;
            try
            {
                Directory.CreateDirectory(ReportDirectory());
                string path = Path.Combine(ReportDirectory(), RoleName() + ".raw");
                UnityEngine.Profiling.Profiler.logFile = path;
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                // Call stacks on GC.Alloc samples name the allocating method, not just the nearest marker.
                UnityEngine.Profiling.Profiler.enableAllocationCallstacks = true;
                UnityEngine.Profiling.Profiler.enabled = true;
                _profiling = true;
                _events.LogInfo(LogSource, $"Profiling {_options.ProfileFrames} frames to {path}.");
            }
            catch (Exception exception)
            {
                _events.LogWarning(LogSource, $"Could not start the profiler capture: {exception.Message}");
            }
        }

        private void StopProfile()
        {
            if (!_profiling) return;
            _profiling = false;
            UnityEngine.Profiling.Profiler.enableAllocationCallstacks = false;
            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = string.Empty;
            _events.LogInfo(LogSource, $"Profiler capture done ({_profiledFrames} frames).");
        }

        private string RoleName()
        {
            if (!string.IsNullOrEmpty(_options.Role)) return _options.Role!;
            if (_network.IsHost) return "host";
            if (_network.IsServer) return "server";
            return $"client{_network.LocalClientId}";
        }

        private TrafficTotals ReadTraffic()
        {
            if (_networkManager.NetworkConfig.NetworkTransport is not UnityTransport transport) return default;
            try
            {
                ref var driver = ref transport.GetNetworkDriver();
                if (!driver.IsCreated) return default;
                var stats = driver.GetStatistics();
                return new TrafficTotals(true, stats.TxTotalBytes, stats.RxTotalBytes, stats.TxTotalPackets, stats.RxTotalPackets);
            }
            catch (Exception)
            {
                return default;
            }
        }

        private void ScanMessageMarkers(float now)
        {
            _lastScan = now;
            _handles.Clear();
            ProfilerRecorderHandle.GetAvailable(_handles);
            foreach (var handle in _handles)
            {
                string name = ProfilerRecorderHandle.GetDescription(handle).Name;
                bool sent = name.StartsWith(SentPrefix, StringComparison.Ordinal);
                if (!sent && !name.StartsWith(ReceivedPrefix, StringComparison.Ordinal)) continue;
                if (!_messageNames.Add(name)) continue;
                _messages.Add(new MessageProbe(name, sent, new Probe(name)));
            }
        }

        private static void AddIfValid(PerfSeries series, Probe probe, float value)
        {
            if (probe.Valid) series.Add(value);
        }

        private static void AddIfPositive(PerfSeries series, Probe probe)
        {
            if (probe.Valid && probe.Last > 0) series.Add(probe.Last);
        }

        private Probe Track(string stat)
        {
            var probe = new Probe(stat);
            _probes.Add(probe);
            return probe;
        }

        private PerfSeries Series(string name, string unit, int capacity)
        {
            var series = new PerfSeries(name, unit, capacity);
            _series.Add(series);
            return series;
        }

        private readonly struct TrafficTotals
        {
            public readonly bool Valid;
            public readonly ulong TxBytes;
            public readonly ulong RxBytes;
            public readonly ulong TxPackets;
            public readonly ulong RxPackets;

            public TrafficTotals(bool valid, ulong txBytes, ulong rxBytes, ulong txPackets, ulong rxPackets)
            {
                Valid = valid;
                TxBytes = txBytes;
                RxBytes = rxBytes;
                TxPackets = txPackets;
                RxPackets = rxPackets;
            }
        }

        private sealed class MessageProbe
        {
            public readonly string Name;
            public readonly bool Sent;
            public readonly Probe Recorder;
            public long Total;

            public MessageProbe(string name, bool sent, Probe recorder)
            {
                Name = name;
                Sent = sent;
                Recorder = recorder;
            }
        }

        /// <summary>A profiler counter or marker by name: the last finished frame's value, and how often a marker ran.</summary>
        private sealed class Probe : IDisposable
        {
            private ProfilerRecorder _recorder;

            public string Name { get; }
            public bool Valid => _recorder.Valid;
            public long Last => _recorder.Valid ? _recorder.LastValue : 0;
            public long Current => _recorder.Valid ? _recorder.CurrentValue : 0;
            public int LastCount => _recorder.Valid && _recorder.Count > 0 ? (int)_recorder.GetSample(0).Count : 0;

            public Probe(string name)
            {
                Name = name;
                _recorder = new ProfilerRecorder(name, 1,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                    ProfilerRecorderOptions.SumAllSamplesInFrame);
            }

            public void Dispose() => _recorder.Dispose();
        }
    }
}
