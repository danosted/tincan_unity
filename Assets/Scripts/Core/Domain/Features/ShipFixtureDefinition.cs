#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Features
{
    /// <summary>
    /// A networked prefab to spawn as a child of every airship, at a pose in ship-local space. Features contribute
    /// these through their FeatureInstaller instead of editing the airship prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipFixture", menuName = "TinCan/Features/Ship Fixture")]
    public class ShipFixtureDefinition : ScriptableObject
    {
        [Tooltip("Prefab with a NetworkObject (AutoObjectParentSync on) and optionally an IShipModule on its root.")]
        public GameObject? Prefab;
        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
    }

    public interface IShipFixtureCatalog
    {
        IReadOnlyList<ShipFixtureDefinition> Fixtures { get; }
    }
}
