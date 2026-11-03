#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// Hosting and joining a game: the seam menus and the command line use instead of starting the network service
    /// themselves. The server checks every joiner (version, player cap) and keeps the players with their names.
    /// .docs/plans/hosting-and-joining.md.
    /// </summary>
    public interface ISessionService
    {
        bool IsActive { get; }

        /// <summary>On the server: the players let in, the host's own included. Empty on a client and offline.</summary>
        IReadOnlyList<SessionPlayer> Players { get; }

        /// <summary>
        /// How the last running session ended; null until one has (and after a new one starts). A join that never got
        /// in is not a session: its <see cref="SessionStart"/> says why.
        /// </summary>
        SessionEnd? LastEnd { get; }

        event Action<SessionPlayer>? PlayerJoined;
        event Action<SessionPlayer>? PlayerLeft;
        event Action<SessionEnd>? Ended;

        /// <summary>Hosts (with a local player) or starts a dedicated server.</summary>
        UniTask<SessionStart> HostAsync(HostOptions options, CancellationToken cancellation);

        /// <summary>
        /// Joins a game and completes once the host has let this client in, or turned it away. Cancelling stops the
        /// attempt.
        /// </summary>
        UniTask<SessionStart> JoinAsync(JoinTarget target, CancellationToken cancellation);

        void Leave();
    }
}
