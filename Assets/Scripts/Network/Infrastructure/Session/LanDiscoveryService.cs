#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// LAN discovery over UDP broadcast, polled each frame. While this peer serves a game (host or dedicated server) it
    /// answers queries on <see cref="LanDiscoveryProcessor.DiscoveryPort"/> with the game's name, players and version.
    /// While browsing it asks every <see cref="QueryInterval"/> seconds (broadcast, and loopback for a game on this
    /// machine) and lists who answered, forgetting a game not heard from for <see cref="StaleAfter"/> seconds.
    /// .docs/plans/hosting-and-joining.md (S2).
    /// </summary>
    public sealed class LanDiscoveryService : ILanDiscovery, ITickable, IDisposable
    {
        public const float QueryInterval = 1f;
        public const float StaleAfter = 3.5f;
        public const string BroadcastAddress = "255.255.255.255";
        public const string LoopbackAddress = "127.0.0.1";

        private readonly ISessionService _session;
        private readonly INetworkConnections _connections;
        private readonly IDatagramSockets _sockets;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly string _protocolVersion;
        private readonly int _discoveryPort;
        private readonly Random _random = new();

        private readonly List<DiscoveredGame> _games = new();
        private readonly List<(uint HostId, float LastSeen)> _seen = new();

        private IDatagramSocket? _responder;
        private bool _responderFailed;
        private uint _hostId;
        private IDatagramSocket? _browser;
        private float _nextQuery;

        public LanDiscoveryService(ISessionService session, INetworkConnections connections, IDatagramSockets sockets, ITimeService time,
            IEventPublisher events, string protocolVersion, int discoveryPort = LanDiscoveryProcessor.DiscoveryPort)
        {
            _session = session;
            _connections = connections;
            _sockets = sockets;
            _time = time;
            _events = events;
            _protocolVersion = protocolVersion;
            _discoveryPort = discoveryPort;
        }

        public IReadOnlyList<DiscoveredGame> Games => _games;
        public bool Browsing { get; private set; }
        public event Action? Changed;

        public void Browse(bool browsing)
        {
            if (browsing == Browsing) return;
            Browsing = browsing;
            if (browsing)
            {
                _nextQuery = _time.Time;
                return;
            }

            _browser?.Dispose();
            _browser = null;
            _seen.Clear();
            if (_games.Count == 0) return;
            _games.Clear();
            Changed?.Invoke();
        }

        public void Tick()
        {
            Respond();
            Ask();
        }

        public void Dispose()
        {
            _responder?.Dispose();
            _responder = null;
            _browser?.Dispose();
            _browser = null;
        }

        private void Respond()
        {
            if (_session.Hosted is not { } hosted)
            {
                CloseResponder();
                return;
            }

            if (_responder == null && !_responderFailed) OpenResponder();
            if (_responder == null) return;

            byte[]? reply = null;
            while (_responder.TryReceive(out var bytes, out var address, out var port))
            {
                if (!LanDiscoveryProcessor.IsQuery(bytes)) continue;
                reply ??= LanDiscoveryProcessor.Reply(new LanGameInfo(_hostId, hosted.Name, _protocolVersion, _connections.ListenPort,
                    _session.Players.Count, hosted.MaxPlayers, needsPassword: false));
                _responder.Send(reply, address, port);
            }
        }

        private void OpenResponder()
        {
            _responder = _sockets.Listen(_discoveryPort);
            _hostId = (uint)_random.Next(1, int.MaxValue);
            if (_responder != null) return;

            _responderFailed = true;
            _events.LogWarning("Session", $"LAN discovery could not open UDP {_discoveryPort}; this game will not show in other players' lists.");
        }

        private void CloseResponder()
        {
            _responderFailed = false;
            _responder?.Dispose();
            _responder = null;
        }

        private void Ask()
        {
            if (!Browsing) return;

            _browser ??= _sockets.Broadcaster();
            if (_browser == null) return;

            float now = _time.Time;
            if (now >= _nextQuery)
            {
                _nextQuery = now + QueryInterval;
                var query = LanDiscoveryProcessor.Query();
                _browser.Send(query, BroadcastAddress, _discoveryPort);
                _browser.Send(query, LoopbackAddress, _discoveryPort);
            }

            bool changed = false;
            while (_browser.TryReceive(out var bytes, out var address, out _))
            {
                if (LanDiscoveryProcessor.TryReadReply(bytes, out var info)) changed |= Heard(info, address, now);
            }

            for (int i = _games.Count - 1; i >= 0; i--)
            {
                if (!LanDiscoveryProcessor.IsStale(_seen[i].LastSeen, now, StaleAfter)) continue;
                _games.RemoveAt(i);
                _seen.RemoveAt(i);
                changed = true;
            }

            if (changed) Changed?.Invoke();
        }

        /// <summary>Adds or refreshes a game; true when the list shows something new. A host heard on loopback keeps that address.</summary>
        private bool Heard(LanGameInfo info, string address, float now)
        {
            int index = _seen.FindIndex(s => s.HostId == info.HostId);
            if (index >= 0)
            {
                var known = _games[index];
                if (known.Address == LoopbackAddress) address = LoopbackAddress;
            }

            var game = new DiscoveredGame(info.Name, address, info.GamePort, info.Players, info.MaxPlayers, info.Version,
                info.Version == _protocolVersion, info.NeedsPassword);
            if (index < 0)
            {
                _games.Add(game);
                _seen.Add((info.HostId, now));
                return true;
            }

            _seen[index] = (info.HostId, now);
            if (Same(_games[index], game)) return false;
            _games[index] = game;
            return true;
        }

        private static bool Same(DiscoveredGame a, DiscoveredGame b) =>
            a.Name == b.Name && a.Address == b.Address && a.Port == b.Port && a.Players == b.Players
            && a.MaxPlayers == b.MaxPlayers && a.Version == b.Version && a.NeedsPassword == b.NeedsPassword;
    }
}
