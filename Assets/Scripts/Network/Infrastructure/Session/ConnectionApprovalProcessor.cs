#nullable enable
using TinCan.Core.Domain.Networking;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// Decides on the server whether a connecting client may join: a readable payload, the same protocol version, and a
    /// free place. The host's own client is always let in. Refusal reasons are the lines the client's menu shows.
    /// </summary>
    public sealed class ConnectionApprovalProcessor
    {
        public const string UnreadableReason = "The host could not read your join request. Are you on the same version?";

        public static string VersionMismatchReason(string host, string client) =>
            $"Version mismatch: the host runs {Shown(host)}, you run {Shown(client)}.";

        public static string FullReason(int maxPlayers) => $"The game is full ({maxPlayers} players).";

        /// <param name="playersIn">Players already let in (connected or still connecting), the host's own included.</param>
        public (ConnectionDecision Decision, ConnectionPayload Payload) Decide(ConnectionRequest request, string hostVersion, int maxPlayers, int playersIn)
        {
            bool readable = ConnectionPayload.TryDecode(request.Payload, out var payload);
            if (!readable) payload = new ConnectionPayload(string.Empty, ConnectionPayload.DefaultName);

            if (request.IsHostClient) return (ConnectionDecision.Approve(), payload);
            if (!readable) return (ConnectionDecision.Refuse(UnreadableReason), payload);
            if (payload.Version != hostVersion) return (ConnectionDecision.Refuse(VersionMismatchReason(hostVersion, payload.Version)), payload);
            if (playersIn >= maxPlayers) return (ConnectionDecision.Refuse(FullReason(maxPlayers)), payload);

            return (ConnectionDecision.Approve(), payload);
        }

        private static string Shown(string version) => string.IsNullOrEmpty(version) ? "an unknown version" : version;
    }
}
