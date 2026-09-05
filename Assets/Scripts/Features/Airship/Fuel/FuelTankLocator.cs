#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Finds the fuel tank fixture under an airship (the tank is a child NetworkBehaviour of the ship prefab).
    /// </summary>
    public static class FuelTankLocator
    {
        public static IFuelTank? Find(IAirshipView airship) => FindFixture<IFuelTank>(airship);

        /// <summary>Finds any fixture interface under the airship (fuel tank, jerry-can supply, gauge...).</summary>
        public static T? FindFixture<T>(IAirshipView airship) where T : class
        {
            // Prefer the component's own transform: the view behind IAirshipView.Transform can already be destroyed
            // during teardown while the mediator object is still registered.
            if (airship is Component component)
            {
                return component == null ? null : component.GetComponentInChildren<T>(true);
            }

            var transform = airship.Transform;
            if (transform == null) return null;
            return transform.GetComponentInChildren<T>(true);
        }

        /// <summary>True when a cached tank reference is still usable (Unity objects can be destroyed under us).</summary>
        public static bool IsAlive(IFuelTank? tank) => tank is not Object unityObject ? tank != null : unityObject != null;
    }
}
