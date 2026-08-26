using Unity.Netcode;
using Unity.Netcode.Components;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Project policy for NGO transform synchronization.
    /// Movement and platform support remain owned by the movement layer.
    /// </summary>
    public class NetworkTransformMediator : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => true;
    }
}
