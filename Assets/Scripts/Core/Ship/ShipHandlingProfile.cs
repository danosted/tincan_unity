#nullable enable
using System;
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// A way of flying, as an asset a designer names and tunes (<c>Assets/Settings/ShipHandling/HANDLING_*</c>). A ship's
    /// core part picks one (<see cref="Parts.ShipPartDefinition.Handling"/>); a ship with no design flies on the one its
    /// prefab names (<see cref="AirshipControllerView"/>). Plan: .docs/plans/ship-handling-profiles.md.
    /// </summary>
    [CreateAssetMenu(fileName = "HANDLING_New", menuName = "TinCan/Ship/Handling Profile")]
    public class ShipHandlingProfile : ScriptableObject
    {
        [SerializeField] private ShipHandling _handling = ShipHandling.Default;

        public ShipHandling Handling => _handling;

        /// <summary>
        /// The values changed: an edit in the Inspector (also during Play, so a designer tunes a flying ship live) or
        /// <see cref="SetHandling"/>. Ships flying on this profile follow at once.
        /// </summary>
        public event Action? Changed;

        /// <summary>Tooling and test seam; designers edit the asset in the Inspector.</summary>
        public void SetHandling(ShipHandling handling)
        {
            _handling = handling;
            Changed?.Invoke();
        }

        private void OnValidate() => Changed?.Invoke();

        /// <summary>Test and tooling seam; assets set these in the Inspector.</summary>
        public static ShipHandlingProfile Create(string name, ShipHandling handling)
        {
            var profile = CreateInstance<ShipHandlingProfile>();
            profile.name = name;
            profile._handling = handling;
            return profile;
        }
    }
}
