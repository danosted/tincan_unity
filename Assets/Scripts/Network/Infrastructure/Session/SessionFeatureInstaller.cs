#nullable enable
using System;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// Hosting and joining (core: loads in every scene). Registers <see cref="ISessionService"/> as
    /// <see cref="DirectSessionService"/>, which checks every joiner against this build's version
    /// (<see cref="Application.version"/>), and <see cref="ILanDiscovery"/> as <see cref="LanDiscoveryService"/>. See
    /// .docs/plans/hosting-and-joining.md.
    /// </summary>
    [CreateAssetMenu(fileName = "SessionFeatureInstaller", menuName = "TinCan/Features/Session Feature Installer")]
    public class SessionFeatureInstaller : FeatureInstaller
    {
        public override int Order => -15;

        public override void Install(IContainerBuilder builder)
        {
            // The composition root registers the network service; its connection side is the same object.
            builder.Register(resolver => resolver.Resolve<INetworkService>() as INetworkConnections
                    ?? throw new InvalidOperationException($"The network service does not implement {nameof(INetworkConnections)}."),
                Lifetime.Singleton);

            builder.Register<DirectSessionService>(Lifetime.Singleton)
                .As<ISessionService>().As<IInitializable>()
                .WithParameter("protocolVersion", Application.version);

            // LAN discovery: every host and dedicated server answers; the Join menu browses while it is open.
            builder.Register<UdpDatagramSockets>(Lifetime.Singleton).As<IDatagramSockets>();
            builder.Register<LanDiscoveryService>(Lifetime.Singleton)
                .As<ILanDiscovery>().As<ITickable>()
                .WithParameter("protocolVersion", Application.version)
                .WithParameter("discoveryPort", (int)LanDiscoveryProcessor.DiscoveryPort);
        }
    }
}
