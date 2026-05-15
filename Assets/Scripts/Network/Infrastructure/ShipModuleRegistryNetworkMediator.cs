using System.Collections.Generic;
using TinCan.Core.Domain;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Tracks all modules attached to this ship across the network.
    /// </summary>
    public class ShipModuleRegistryNetworkMediator : NetworkBehaviour, IShipModuleRegistry
    {
        private readonly List<IShipModule> _modules = new List<IShipModule>();
        public IReadOnlyList<IShipModule> Modules => _modules;

        public void RegisterModule(IShipModule module)
        {
            if (!_modules.Contains(module))
            {
                _modules.Add(module);
                Debug.Log($"[ShipModuleRegistry] Registered module: {module.ModuleName}");
            }
        }

        public void UnregisterModule(IShipModule module)
        {
            if (_modules.Remove(module))
            {
                Debug.Log($"[ShipModuleRegistry] Unregistered module: {module.ModuleName}");
            }
        }
    }
}
