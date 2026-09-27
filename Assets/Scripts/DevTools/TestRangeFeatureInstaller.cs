#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools
{
    /// <summary>
    /// The test range's own wiring. The test scenes' lifetime scope spawns TestShip_Prefab instead of the real airship,
    /// and the real one is registered with NGO only through DefaultNetworkPrefabs. So this registers the test ship at
    /// runtime, the way features register their prefabs, without editing that list. Only the test profiles include it.
    /// See .docs/NETWORK_TEST_HARNESS.md, "Test range".
    /// </summary>
    [CreateAssetMenu(fileName = "TestRangeFeatureInstaller", menuName = "TinCan/Features/Test Range Installer")]
    public class TestRangeFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private GameObject? _testShip;

        public override IEnumerable<GameObject> NetworkedPrefabs =>
            _testShip != null ? new[] { _testShip } : Enumerable.Empty<GameObject>();

        public override void Install(IContainerBuilder builder)
        {
            if (_testShip == null) Debug.LogWarning("[TestRange] TestRangeFeatureInstaller has no test ship assigned.");
        }
    }
}
