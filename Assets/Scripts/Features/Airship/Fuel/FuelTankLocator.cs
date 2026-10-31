#nullable enable
using UnityEngine;
using TinCan.Core.Ship;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Finds the fuel tank under an airship: the tank is ship state (the pose-less FuelTankState fixture), and the
    /// receiver, the crystal rack and the gauge are separate fixtures or socket fittings parented to the same ship.
    /// </summary>
    public static class FuelTankLocator
    {
        public static IFuelTank? Find(IAirshipView airship) => FindFixture<IFuelTank>(airship);

        /// <summary>The tank of the ship <paramref name="part"/> is on; off a ship, a tank above it (tests, loose prefabs).</summary>
        public static IFuelTank? FindFor(Component part)
        {
            var airship = part.GetComponentInParent<IAirshipView>(true);
            return airship != null ? Find(airship) : part.GetComponentInParent<IFuelTank>(true);
        }

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
