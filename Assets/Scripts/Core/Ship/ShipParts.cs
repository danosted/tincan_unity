#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Which ship a part is on: the one place fixtures and fittings (the helm, the anchor, a socket, a leash point) ask.
    /// Parts are parented to the ship after they spawn, so it is looked up when asked.
    /// </summary>
    public static class ShipParts
    {
        /// <summary>The ship above <paramref name="part"/> (never the part itself); null while unparented or destroyed.</summary>
        public static IAirshipView? ShipOf(Component part) =>
            part != null && part.transform.parent != null ? part.transform.parent.GetComponentInParent<IAirshipView>() : null;
    }
}
