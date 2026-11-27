#nullable enable
using TinCan.Core.Ship;
using TinCan.Core.Ship.Parts;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Ship Handling > Build Assets: the first handling profiles and who flies on them (plan
    /// .docs/plans/ship-handling-profiles.md). <c>HANDLING_Standard</c> is the helm core's: every designed ship flies on it
    /// (about the 10 m/s and 14 deg/s the Galleon's parts gave it). <c>HANDLING_Brisk</c> is the bare <c>Airship_Prefab</c>'s
    /// (and so the test ship's): the values it had on its prefab. <c>ModularShip_Prefab</c> (and the Galleon on it) falls
    /// back to the standard one. A profile that exists is never overwritten: designers own its values.
    /// </summary>
    public static class ShipHandlingAssetBuilder
    {
        private const string Folder = "Assets/Settings/ShipHandling/";
        public const string StandardPath = Folder + "HANDLING_Standard.asset";
        public const string BriskPath = Folder + "HANDLING_Brisk.asset";
        private const string HelmPartPath = "Assets/Settings/ShipParts/PART_CoreHelm.asset";
        private const string AirshipPrefabPath = "Assets/Prefabs/Airship/Airship_Prefab.prefab";

        [MenuItem("TinCan/Dev/Ship Handling/Build Assets")]
        public static void Build()
        {
            var standard = Standard();
            var brisk = Profile(BriskPath, Brisk());

            var helm = AssetDatabase.LoadAssetAtPath<ShipPartDefinition>(HelmPartPath);
            if (helm != null) AssignCore(helm, standard);
            AssignToPrefab(AirshipPrefabPath, brisk);
            AssignToPrefab(ShipDesignsAssetBuilder.ModularShipPath, standard);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShipHandling] Profiles {StandardPath} (helm core, modular ship) and {BriskPath} (bare airship) ready.");
        }

        /// <summary>The helm core's profile, made on first use.</summary>
        public static ShipHandlingProfile Standard() => Profile(StandardPath, StandardHandling());

        /// <summary>The core part flies on the profile, unless a designer already gave it one.</summary>
        public static void AssignCore(ShipPartDefinition core, ShipHandlingProfile profile)
        {
            var serialized = new SerializedObject(core);
            var field = serialized.FindProperty("_handling");
            if (field.objectReferenceValue != null) return;
            field.objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(core);
        }

        private static ShipHandling StandardHandling()
        {
            var handling = Brisk();
            handling.MaxForwardSpeed = 10f;
            handling.AccelerationRate = 5f;
            handling.TurnSpeed = 14f;
            return handling;
        }

        private static ShipHandling Brisk()
        {
            var handling = ShipHandling.Default;
            handling.MaxForwardSpeed = 30f;
            handling.AccelerationRate = 10f;
            handling.PitchLevelSpeed = 0.03f;
            return handling;
        }

        private static ShipHandlingProfile Profile(string path, ShipHandling handling)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipHandlingProfile>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Settings", "ShipHandling");
            var profile = ShipHandlingProfile.Create(System.IO.Path.GetFileNameWithoutExtension(path), handling);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static void AssignToPrefab(string path, ShipHandlingProfile profile)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<AirshipControllerView>();
                if (view == null) return;
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_handlingProfile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
