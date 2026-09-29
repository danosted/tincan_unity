#nullable enable
namespace TinCan.Core.UI
{
    public readonly struct SessionRequest
    {
        public readonly SessionRequestKind Kind;
        public readonly string Address;
        public readonly ushort Port;
        /// <summary>Seconds to wait before joining (<c>-joindelay</c>); 0 joins at once. Hosting ignores it.</summary>
        public readonly float DelaySeconds;

        public SessionRequest(SessionRequestKind kind, string address, ushort port, float delaySeconds = 0f)
        {
            Kind = kind;
            Address = address;
            Port = port;
            DelaySeconds = delaySeconds;
        }
    }
}
