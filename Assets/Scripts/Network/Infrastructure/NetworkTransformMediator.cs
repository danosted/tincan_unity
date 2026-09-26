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
        /// <summary>
        /// Set on objects whose owner predicts its own motion (the player). The owning client then keeps its predicted
        /// transform instead of being overwritten by the interpolated server state; everyone else still follows the
        /// server. Server teleports still apply.
        /// </summary>
        public bool OwnerPredicted { get; set; }

        protected override bool OnIsServerAuthoritative() => true;

        public override void OnUpdate()
        {
            if (OwnerPredicted && IsOwner && !IsServer) return;

            base.OnUpdate();
        }
    }
}
