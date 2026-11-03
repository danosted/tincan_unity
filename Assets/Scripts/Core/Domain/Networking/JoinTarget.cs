#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>Where to join. An address and port for now; address codes and discovered games come in later slices.</summary>
    public readonly struct JoinTarget
    {
        public readonly string Address;
        public readonly ushort Port;

        public JoinTarget(string address, ushort port)
        {
            Address = address;
            Port = port;
        }

        public override string ToString() => $"{Address}:{Port}";
    }
}
