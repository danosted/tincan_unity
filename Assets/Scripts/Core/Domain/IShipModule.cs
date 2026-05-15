using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: Interface for any module that can be attached to a ship.
    /// </summary>
    public interface IShipModule : IActor
    {
        string ModuleName { get; }
        void OnAttachedToShip(IActor ship);
        void OnDetachedFromShip();
    }
}
