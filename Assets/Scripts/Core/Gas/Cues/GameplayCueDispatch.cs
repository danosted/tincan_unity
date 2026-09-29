#nullable enable
namespace TinCan.Core.Gas.Cues
{
    /// <summary>Where a burst cue goes from this peer (<see cref="GameplayCueDispatchProcessor"/>).</summary>
    public readonly struct GameplayCueDispatch
    {
        public static readonly GameplayCueDispatch Drop = default;

        /// <summary>Play it on this peer now.</summary>
        public readonly bool PlayLocally;
        /// <summary>Send it to the clients (server only).</summary>
        public readonly bool SendToClients;
        /// <summary>Leave out the target's owning client, which already played its own prediction.</summary>
        public readonly bool ExcludeOwner;

        public GameplayCueDispatch(bool playLocally, bool sendToClients, bool excludeOwner)
        {
            PlayLocally = playLocally;
            SendToClients = sendToClients;
            ExcludeOwner = excludeOwner;
        }
    }
}
