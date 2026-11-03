#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TinCan.Core.Domain.Networking;

namespace TinCan.Tests.Fakes
{
    /// <summary>
    /// A session service that records what callers asked for and starts at once. For callers' tests that only need
    /// to know a host or join was asked for; tests of the session itself use the real one (FakeSessions in Network's
    /// tests).
    /// </summary>
    public sealed class FakeSessionService : ISessionService
    {
        private readonly List<SessionPlayer> _players = new();

        public bool IsActive { get; set; }
        public IReadOnlyList<SessionPlayer> Players => _players;
        public SessionEnd? LastEnd { get; set; }
        public int HostCalls { get; private set; }
        public int JoinCalls { get; private set; }
        public JoinTarget? LastJoin { get; private set; }
        public int LeaveCalls { get; private set; }

        public event Action<SessionPlayer>? PlayerJoined;
        public event Action<SessionPlayer>? PlayerLeft;
        public event Action<SessionEnd>? Ended;

        public UniTask<SessionStart> HostAsync(HostOptions options, CancellationToken cancellation)
        {
            HostCalls++;
            IsActive = true;
            return UniTask.FromResult(SessionStart.Started);
        }

        public UniTask<SessionStart> JoinAsync(JoinTarget target, CancellationToken cancellation)
        {
            JoinCalls++;
            LastJoin = target;
            IsActive = true;
            return UniTask.FromResult(SessionStart.Started);
        }

        public void Leave()
        {
            LeaveCalls++;
            IsActive = false;
        }

        public void RaisePlayerJoined(SessionPlayer player) => PlayerJoined?.Invoke(player);
        public void RaisePlayerLeft(SessionPlayer player) => PlayerLeft?.Invoke(player);
        public void RaiseEnded(SessionEnd end) => Ended?.Invoke(end);
    }
}
