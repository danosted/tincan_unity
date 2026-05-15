using System.Collections.Generic;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: Registry for tracking modules attached to a ship.
    /// </summary>
    public interface IShipModuleRegistry
    {
        IReadOnlyList<IShipModule> Modules { get; }
        void RegisterModule(IShipModule module);
        void UnregisterModule(IShipModule module);
    }
}
