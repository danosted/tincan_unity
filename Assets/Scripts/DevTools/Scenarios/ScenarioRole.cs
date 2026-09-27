#nullable enable

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Which part of a scenario this peer plays. In a host + client run the host is <see cref="Server"/> and the client
    /// is <see cref="Subject"/>. <see cref="Solo"/> is a host playing alone against its own player: it runs the Server
    /// and Subject lanes side by side, so phases interact exactly as they do across the network.
    /// </summary>
    public enum ScenarioRole
    {
        Solo,
        Server,
        Subject
    }
}
