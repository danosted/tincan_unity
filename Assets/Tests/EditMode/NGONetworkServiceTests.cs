#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class NGONetworkServiceTests
    {
        [TestCase("127.0.0.1")]
        [TestCase("0.0.0.0")]
        [TestCase("")]
        [TestCase(null)]
        public void SetConnection_PreservesHostListenAddressAcrossJoinAttempts(string? listenAddress)
        {
            var gameObject = new GameObject("NetworkServiceTest");
            try
            {
                var transport = gameObject.AddComponent<UnityTransport>();
                var manager = gameObject.AddComponent<NetworkManager>();
                transport.ConnectionData.ServerListenAddress = listenAddress!;

                // Named test assemblies cannot reference the predefined Assembly-CSharp assembly.
                var serviceType = Type.GetType("TinCan.Network.Infrastructure.NGONetworkService, Assembly-CSharp", true)!;
                var service = (INetworkService)Activator.CreateInstance(serviceType, new object?[] { manager, null })!;

                service.SetConnection("192.0.2.10", 8000);
                service.SetConnection("192.0.2.20", 9000);

                Assert.That(transport.ConnectionData.Address, Is.EqualTo("192.0.2.20"));
                Assert.That(transport.ConnectionData.Port, Is.EqualTo(9000));
                Assert.That(transport.ConnectionData.ServerListenAddress, Is.EqualTo(listenAddress ?? string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
