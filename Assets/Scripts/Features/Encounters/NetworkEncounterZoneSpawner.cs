#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Infrastructure Layer: spawns an encounter zone's networked object on the server, injected first so its
    /// EntityNetworkMediator registers it (the shape of <c>NetworkIslandTreeSpawner</c>). Clients inject theirs on spawn.
    /// </summary>
    public sealed class NetworkEncounterZoneSpawner : IEncounterZoneSpawner
    {
        private readonly IObjectResolver _container;

        public NetworkEncounterZoneSpawner(IObjectResolver container) => _container = container;

        public void Spawn(GameObject prefab, Vector3 position)
        {
            var zone = Object.Instantiate(prefab, position, Quaternion.identity);
            _container.InjectGameObject(zone);
            zone.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        }
    }
}
