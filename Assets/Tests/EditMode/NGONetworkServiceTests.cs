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
                var serviceType = Type.GetType("TinCan.Network.Infrastructure.NGONetworkService, TinCan.Network", true)!;
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

        [Test]
        public void ServerFrameRate_DefaultsToTheTickRate_AndTakesAHigherOverride()
        {
            var serviceType = Type.GetType("TinCan.Network.Infrastructure.NGONetworkService, TinCan.Network", true)!;
            var method = serviceType.GetMethod("ServerFrameRate")!;
            int Rate(string[] args) => (int)method.Invoke(null, new object[] { args, 30 })!;

            Assert.That(Rate(new[] { "game.x86_64", "-server" }), Is.EqualTo(30));
            Assert.That(Rate(new[] { "-server", "-serverfps", "120" }), Is.EqualTo(120));
            Assert.That(Rate(new[] { "-serverfps", "10" }), Is.EqualTo(30), "never below the tick rate");
            Assert.That(Rate(new[] { "-serverfps", "fast" }), Is.EqualTo(30));
        }

        [Test]
        public void SetListenEndpoint_SetsTheBindAddressAndPort_AndKeepsTheConnectAddress()
        {
            var gameObject = new GameObject("NetworkServiceTest");
            try
            {
                var transport = gameObject.AddComponent<UnityTransport>();
                var manager = gameObject.AddComponent<NetworkManager>();
                transport.ConnectionData.Address = "127.0.0.1";
                transport.ConnectionData.ServerListenAddress = "127.0.0.1";

                var serviceType = Type.GetType("TinCan.Network.Infrastructure.NGONetworkService, TinCan.Network", true)!;
                var service = (INetworkService)Activator.CreateInstance(serviceType, new object?[] { manager, null })!;

                service.SetListenEndpoint("0.0.0.0", 9000);

                Assert.That(transport.ConnectionData.ServerListenAddress, Is.EqualTo("0.0.0.0"));
                Assert.That(transport.ConnectionData.Port, Is.EqualTo(9000));
                Assert.That(transport.ConnectionData.Address, Is.EqualTo("127.0.0.1"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
