#nullable enable
using TinCan.Core.Domain.Networking;
using TinCan.Network.Infrastructure.Session;
using TinCan.Tests.Fakes;

namespace TinCan.Network.Infrastructure.Tests
{
    /// <summary>
    /// The real session service over a test's network service, so callers' tests keep asserting on what reached the
    /// network (StartHost calls, the join address).
    /// </summary>
    public static class FakeSessions
    {
        public const string Version = "test-1";

        public static DirectSessionService Direct(INetworkService network, FakeNetworkConnections? connections = null, FakePlayerSettings? settings = null)
        {
            var session = new DirectSessionService(network, connections ?? new FakeNetworkConnections(), settings ?? new FakePlayerSettings(),
                new FakeEventPublisher(), Version);
            session.Initialize();
            return session;
        }
    }
}
