#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// The player a scenario is about. On the subject peer and in a solo run it is the local player; on the server of
    /// a host + client run it is the first player owned by a remote client, so server-side arrange steps act on the
    /// client's player and the client verifies what it sees.
    /// </summary>
    public sealed class ScenarioSubject
    {
        private readonly HarnessOptions _options;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;

        public ScenarioSubject(HarnessOptions options, IActorRegistry registry, INetworkService network)
        {
            _options = options;
            _registry = registry;
            _network = network;
        }

        public IHumanoidCharacterView? Resolve()
        {
            if (_options.ScenarioSolo || !_network.IsServer) return _registry.GetLocalPlayerActor<IHumanoidCharacterView>();

            return _registry.GetActors<IHumanoidCharacterView>()
                .FirstOrDefault(player => ((IPossessable)player).OwnerId is { } owner && owner != _network.LocalClientId);
        }

        /// <summary>The subject's simulated body. Null until the player has spawned.</summary>
        public Transform? Body => Resolve()?.Movement?.Transform;
    }
}
