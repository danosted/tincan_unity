using System;
using TinCan.Core.Infrastructure;
using TinCan.Features.Airship;
using TinCan.Features.HumanoidMovement;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Drives simulation-critical use cases from NGO's fixed network tick.
    /// </summary>
    public class NetworkSimulationScheduler : IInitializable, IDisposable
    {
        private readonly NetworkManager _networkManager;
        private readonly ProjectTimeService _timeService;
        private readonly AirshipMovementUseCase _airshipMovement;
        private readonly HumanoidMovementUseCase _humanoidMovement;
        private bool _isSubscribed;

        public NetworkSimulationScheduler(
            NetworkManager networkManager,
            ProjectTimeService timeService,
            AirshipMovementUseCase airshipMovement,
            HumanoidMovementUseCase humanoidMovement)
        {
            _networkManager = networkManager;
            _timeService = timeService;
            _airshipMovement = airshipMovement;
            _humanoidMovement = humanoidMovement;
        }

        public void Initialize()
        {
            _networkManager.OnServerStarted += SubscribeToNetworkTicks;
            _networkManager.OnClientStarted += SubscribeToNetworkTicks;
            _networkManager.OnServerStopped += UnsubscribeFromNetworkTicks;
            _networkManager.OnClientStopped += UnsubscribeFromNetworkTicks;

            if (_networkManager.IsListening)
            {
                SubscribeToNetworkTicks();
            }
        }

        public void Dispose()
        {
            UnsubscribeFromNetworkTicks();
            _networkManager.OnServerStarted -= SubscribeToNetworkTicks;
            _networkManager.OnClientStarted -= SubscribeToNetworkTicks;
            _networkManager.OnServerStopped -= UnsubscribeFromNetworkTicks;
            _networkManager.OnClientStopped -= UnsubscribeFromNetworkTicks;
        }

        private void SubscribeToNetworkTicks()
        {
            if (_isSubscribed || _networkManager.NetworkTickSystem == null) return;

            _networkManager.NetworkTickSystem.Tick += SimulateNetworkTick;
            _isSubscribed = true;
        }

        private void UnsubscribeFromNetworkTicks(bool _ = false)
        {
            if (!_isSubscribed || _networkManager.NetworkTickSystem == null) return;

            _networkManager.NetworkTickSystem.Tick -= SimulateNetworkTick;
            _isSubscribed = false;
        }

        private void SimulateNetworkTick()
        {
            var tickSystem = _networkManager.NetworkTickSystem;
            if (tickSystem == null) return;

            _timeService.BeginSimulationTick(tickSystem.TickRate);
            try
            {
                _airshipMovement.Tick();
                Physics.SyncTransforms();
                _humanoidMovement.Tick();
            }
            finally
            {
                _timeService.EndSimulationTick();
            }
        }
    }
}