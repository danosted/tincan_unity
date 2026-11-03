#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>The server's answer to a <see cref="ConnectionRequest"/>; a refusal carries the reason the client shows.</summary>
    public readonly struct ConnectionDecision
    {
        public readonly bool Approved;
        public readonly string Reason;

        private ConnectionDecision(bool approved, string reason)
        {
            Approved = approved;
            Reason = reason;
        }

        public static ConnectionDecision Approve() => new(true, string.Empty);
        public static ConnectionDecision Refuse(string reason) => new(false, reason);
    }
}
