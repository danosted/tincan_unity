#nullable enable
using System;
using TinCan.Core.Domain.Events;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Applies a <see cref="NetworkConditionPreset"/> to this peer's transport once it starts. It uses UTP's global
    /// simulator layer, which NGO already configures in the Editor and in development builds, so no extra package is
    /// needed. Release builds have no simulator layer and log a warning instead.
    /// </summary>
    public sealed class NetworkConditionsUseCase : IInitializable, IDisposable
    {
        private const string LogSource = "NetHarness";

        private readonly NetworkManager _networkManager;
        private readonly HarnessOptions _options;
        private readonly IEventPublisher _events;
        private bool _applied;

        public NetworkConditionPreset? Active { get; private set; }

        public NetworkConditionsUseCase(NetworkManager networkManager, HarnessOptions options, IEventPublisher events)
        {
            _networkManager = networkManager;
            _options = options;
            _events = events;
        }

        public void Initialize()
        {
            if (_options.NetworkPreset == null) return;

            _networkManager.OnServerStarted += Apply;
            _networkManager.OnClientStarted += Apply;
            _networkManager.OnServerStopped += ResetApplied;
            _networkManager.OnClientStopped += ResetApplied;
        }

        public void Dispose()
        {
            _networkManager.OnServerStarted -= Apply;
            _networkManager.OnClientStarted -= Apply;
            _networkManager.OnServerStopped -= ResetApplied;
            _networkManager.OnClientStopped -= ResetApplied;
        }

        private void Apply()
        {
            if (_applied) return;

            if (!NetworkConditionPresets.TryGet(_options.NetworkPreset, out var preset))
            {
                _events.LogWarning(LogSource, $"Unknown network preset '{_options.NetworkPreset}'. Known: {NetworkConditionPresets.Names}.");
                return;
            }

            if (_networkManager.NetworkConfig.NetworkTransport is not UnityTransport transport)
            {
                _events.LogWarning(LogSource, "Network conditions need UnityTransport; preset not applied.");
                return;
            }

            transport.GetNetworkDriver().ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter
            {
                SendDelayMS = preset.SendDelayMs,
                SendJitterMS = preset.SendJitterMs,
                SendPacketLossPercent = preset.SendPacketLossPercent
            });

            _applied = true;
            Active = preset;
            _events.LogInfo(LogSource, $"Network conditions applied: {preset}.");
        }

        private void ResetApplied(bool _)
        {
            _applied = false;
            Active = null;
        }
    }
}
