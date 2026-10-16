#nullable enable

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// The test-range scenes (Assets/Scenes/Test). Each is the shared TestRangeRig prefab plus one feature profile, so a
    /// scenario loads only its feature area on a bare test ship. See .docs/NETWORK_TEST_HARNESS.md, "Test range".
    /// </summary>
    public static class TestScenes
    {
        public const string Core = "Assets/Scenes/Test/Test_Core.unity";
        public const string ShipDamage = "Assets/Scenes/Test/Test_ShipDamage.unity";
        public const string NetCatch = "Assets/Scenes/Test/Test_NetCatch.unity";
        public const string Cannon = "Assets/Scenes/Test/Test_Cannon.unity";
        public const string Voyage = "Assets/Scenes/Test/Test_Voyage.unity";
        public const string Helm = "Assets/Scenes/Test/Test_Helm.unity";
        public const string Shipyard = "Assets/Scenes/Test/Test_Shipyard.unity";
        public const string GrappleHook = "Assets/Scenes/Test/Test_GrappleHook.unity";
        public const string Respawn = "Assets/Scenes/Test/Test_Respawn.unity";
        public const string Navigation = "Assets/Scenes/Test/Test_Navigation.unity";

        public static readonly string[] All = { Core, ShipDamage, NetCatch, Cannon, Voyage, Helm, Shipyard, GrappleHook, Respawn, Navigation };
    }
}
