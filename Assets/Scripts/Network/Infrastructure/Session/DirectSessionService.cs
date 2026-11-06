#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain.Settings;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// Hosting and joining straight over the network service, with no one else's servers: joiners connect by address and
    /// the server checks each one (<see cref="ConnectionApprovalProcessor"/>). Keeps the players with their names on the
    /// server, and how the last session ended for the menu. .docs/plans/hosting-and-joining.md.
    /// </summary>
    public sealed class DirectSessionService : ISessionService, IInitializable, IDisposable
    {
        /// <summary>The player-name setting (.docs/plans/player-settings.md, S3); "Player" until it exists.</summary>
        public const string PlayerNameSettingKey = "player.name";
        public const string DefaultListenAddress = "0.0.0.0";
        public const string HostFailedMessage = "Could not start the game. Is another game using the port?";
        public const string DefaultServerName = "TinCan server";

        public static string UnreachableMessage(JoinTarget target) => $"Could not reach {target}.";

        private readonly INetworkService _network;
        private readonly INetworkConnections _connections;
        private readonly IPlayerSettings _settings;
        private readonly IEventPublisher _events;
        private readonly string _protocolVersion;
        private readonly ConnectionApprovalProcessor _approval = new();

        // Let in (connected or still connecting), by client id; Players lists the connected ones in join order.
        private readonly Dictionary<ulong, string> _approved = new();
        private readonly List<SessionPlayer> _players = new();
        private int _maxPlayers = HostOptions.DefaultMaxPlayers;
        private string _gameName = string.Empty;
        private bool _running;

        // A join waiting for the host's answer.
        private UniTaskCompletionSource<SessionStart>? _joining;
        private JoinTarget _joinTarget;

        public DirectSessionService(INetworkService network, INetworkConnections connections, IPlayerSettings settings,
            IEventPublisher events, string protocolVersion)
        {
            _network = network;
            _connections = connections;
            _settings = settings;
            _events = events;
            _protocolVersion = protocolVersion;
        }

        public bool IsActive => _network.IsActive;
        public IReadOnlyList<SessionPlayer> Players => _players;
        public SessionEnd? LastEnd { get; private set; }
        public HostedGame? Hosted => _network.IsServer && _network.IsActive ? new HostedGame(_gameName, _maxPlayers) : null;

        public event Action<SessionPlayer>? PlayerJoined;
        public event Action<SessionPlayer>? PlayerLeft;
        public event Action<SessionEnd>? Ended;

        public void Initialize()
        {
            _connections.Approval = Approve;
            _connections.ClientConnected += OnClientConnected;
            _connections.ClientDisconnected += OnClientDisconnected;
        }

        public void Dispose()
        {
            if (_connections.Approval == Approve) _connections.Approval = null;
            _connections.ClientConnected -= OnClientConnected;
            _connections.ClientDisconnected -= OnClientDisconnected;
            _joining?.TrySetCanceled();
        }

        public UniTask<SessionStart> HostAsync(HostOptions options, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_network.IsActive) return UniTask.FromResult(SessionStart.AlreadyActive);

            Begin();
            _maxPlayers = options.MaxPlayers;
            _gameName = GameName(options);
            if (options.Dedicated)
            {
                if (options.Port != 0)
                {
                    _network.SetListenEndpoint(string.IsNullOrWhiteSpace(options.ListenAddress) ? DefaultListenAddress : options.ListenAddress, options.Port);
                }
                _network.StartServer();
            }
            else
            {
                _network.StartHost();
            }

            // Starting is immediate (the host's own client is let in during StartHost); there is nothing to wait for yet.
            if (_network.IsActive) return UniTask.FromResult(SessionStart.Started);

            _running = false;
            return UniTask.FromResult(SessionStart.Failed(HostFailedMessage));
        }

        public async UniTask<SessionStart> JoinAsync(JoinTarget target, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_network.IsActive || _joining != null) return SessionStart.AlreadyActive;

            Begin();
            var joining = new UniTaskCompletionSource<SessionStart>();
            _joining = joining;
            _joinTarget = target;
            _network.SetConnection(target.Address, target.Port);
            _network.StartClient();

            if (!_network.IsActive)
            {
                StopJoining();
                return SessionStart.Failed(UnreachableMessage(target));
            }

            try
            {
                using (cancellation.Register(() => joining.TrySetCanceled(cancellation)))
                {
                    return await joining.Task;
                }
            }
            catch (OperationCanceledException)
            {
                if (_joining == joining && _network.IsActive)
                {
                    StopJoining();
                    _network.Shutdown();
                }
                throw;
            }
            finally
            {
                if (_joining == joining) _joining = null;
            }
        }

        public void Leave()
        {
            if (!_network.IsActive) return;

            // Our own disconnect comes back through ClientDisconnected during Shutdown; this player left, nothing was lost.
            // Leaving while a join waits cancels the join; it was never a session.
            var joining = _joining;
            bool wasRunning = _running && joining == null;
            StopJoining();
            _network.Shutdown();
            joining?.TrySetCanceled();
            if (wasRunning) End(SessionEnd.Left);
        }

        private void Begin()
        {
            Clear();
            LastEnd = null;
            _running = true;
            _connections.SetConnectionPayload(new ConnectionPayload(_protocolVersion, LocalName()).Encode());
        }

        /// <summary>The name others see in their game list: the one asked for, a server's default, or "Ada's game".</summary>
        public string GameName(HostOptions options) =>
            !string.IsNullOrWhiteSpace(options.Name) ? options.Name.Trim()
            : options.Dedicated ? DefaultServerName
            : $"{ConnectionPayload.CleanName(LocalName())}'s game";

        private string LocalName() =>
            _settings.Has(PlayerNameSettingKey) ? _settings.GetString(PlayerNameSettingKey) : ConnectionPayload.DefaultName;

        private ConnectionDecision Approve(ConnectionRequest request)
        {
            var (decision, payload) = _approval.Decide(request, _protocolVersion, _maxPlayers, _approved.Count);
            if (!decision.Approved)
            {
                _events.LogInfo("Session", $"Refused client {request.ClientId} ({payload.Name}): {decision.Reason}");
                return decision;
            }

            _approved[request.ClientId] = payload.Name;
            return decision;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!_network.IsServer)
            {
                _joining?.TrySetResult(SessionStart.Started);
                return;
            }

            if (!_approved.TryGetValue(clientId, out var name)) return;

            var player = new SessionPlayer(clientId, name);
            _players.Add(player);
            _events.LogInfo("Session", $"{name} joined (client {clientId}, {_players.Count}/{_maxPlayers}).");
            PlayerJoined?.Invoke(player);
        }

        private void OnClientDisconnected(ulong clientId, string? reason)
        {
            if (_network.IsServer && clientId != _network.LocalClientId)
            {
                _approved.Remove(clientId);
                int index = _players.FindIndex(p => p.ClientId == clientId);
                if (index < 0) return;

                var player = _players[index];
                _players.RemoveAt(index);
                _events.LogInfo("Session", $"{player.Name} left (client {clientId}).");
                PlayerLeft?.Invoke(player);
                return;
            }

            // Turned away (or never reached) while joining: the join's answer, not the end of a session.
            if (_joining is { } joining)
            {
                StopJoining();
                var start = string.IsNullOrWhiteSpace(reason) ? SessionStart.Failed(UnreachableMessage(_joinTarget)) : SessionStart.Refused(reason!);
                _events.LogInfo("Session", $"Join {_joinTarget} failed: {start.Message}");
                joining.TrySetResult(start);
                return;
            }

            if (!_running) return;
            _running = false;
            Clear();
            End(SessionEnd.FromDisconnect(reason));
        }

        private void StopJoining()
        {
            _joining = null;
            _running = false;
            Clear();
        }

        private void End(SessionEnd end)
        {
            LastEnd = end;
            if (end.Message.Length > 0) _events.LogInfo("Session", $"Session ended: {end.Message}");
            Ended?.Invoke(end);
        }

        private void Clear()
        {
            _approved.Clear();
            _players.Clear();
        }
    }
}
