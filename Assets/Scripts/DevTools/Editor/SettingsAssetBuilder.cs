#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Settings;
using TinCan.Core.Settings;
using TinCan.Core.UI;
using TinCan.Features.FirstPersonCamera;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Settings > Build Assets: the core setting definitions (look sensitivity, invert Y) on the settings
    /// installer, the field of view on the first-person camera installer (its default from FirstPersonCameraConfig), the
    /// Settings menu with a Controls page (the settings, then Key bindings) and a View page, and the main menu's
    /// Controls row turned into Settings. Safe to run again. Plan: .docs/plans/player-settings.md (S1, S2).
    /// </summary>
    public static class SettingsAssetBuilder
    {
        private const string Folder = "Assets/Settings/PlayerSettings";
        private const string InstallerPath = "Assets/Resources/Installers/SettingsFeatureInstaller.asset";
        private const string CameraInstallerPath = "Assets/Resources/Installers/FirstPersonCameraFeatureInstaller.asset";
        private const string CameraConfigPath = "Assets/Settings/Camera/FirstPersonCameraConfig.asset";
        private const string MainMenuPath = "Assets/UI/Menus/Menu_Main.asset";
        private const string ControlsMenuPath = "Assets/UI/Menus/Menu_Controls.asset";
        private const string SettingsMenuPath = "Assets/UI/Menus/Menu_Settings.asset";
        private const string ControlsPagePath = "Assets/UI/Menus/Menu_Settings_Controls.asset";
        private const string ViewPagePath = "Assets/UI/Menus/Menu_Settings_View.asset";

        [MenuItem("TinCan/Dev/Settings/Build Assets")]
        public static void Build()
        {
            var sensitivity = Definition("Setting_LookSensitivity",
                SettingDefinition.Slider(LookSettings.SensitivityKey, "Look sensitivity", SettingGroup.Controls, 0.1f, 3f, 0.05f, 1f));
            var invertY = Definition("Setting_InvertY",
                SettingDefinition.Toggle(LookSettings.InvertYKey, "Invert look Y", SettingGroup.Controls, false));

            var cameraConfig = AssetDatabase.LoadAssetAtPath<FirstPersonCameraConfig>(CameraConfigPath);
            float fov = cameraConfig != null ? cameraConfig.FieldOfView : 75f;
            var fieldOfView = Definition("Setting_FieldOfView",
                SettingDefinition.Slider(FirstPersonViewRig.FieldOfViewKey, "Field of view", SettingGroup.View, 60f, 110f, 1f, fov));

            var installer = Asset<SettingsFeatureInstaller>(InstallerPath, _ => { });
            SetList(installer, "_settings", sensitivity, invertY);

            var cameraInstaller = AssetDatabase.LoadAssetAtPath<FirstPersonCameraFeatureInstaller>(CameraInstallerPath);
            if (cameraInstaller != null) SetField(cameraInstaller, "_fieldOfView", fieldOfView);
            else Debug.LogWarning($"[Settings] {CameraInstallerPath} is missing; the field of view setting is not contributed.");

            var controls = AssetDatabase.LoadAssetAtPath<MenuDefinition>(ControlsMenuPath);
            var controlsPage = Menu(ControlsPagePath, MenuDefinition.Create("settings_controls", "Controls",
                SettingsRow("settings", SettingGroup.Controls),
                new MenuItemDefinition { ItemId = "keys", Label = "Key bindings", Kind = MenuItemKind.Submenu, Submenu = controls },
                Row("back", "Back", MenuItemKind.Back)));
            var viewPage = Menu(ViewPagePath, MenuDefinition.Create("settings_view", "View",
                SettingsRow("settings", SettingGroup.View),
                Row("back", "Back", MenuItemKind.Back)));
            var settingsMenu = Menu(SettingsMenuPath, MenuDefinition.Create("settings", "Settings",
                new MenuItemDefinition { ItemId = "controls", Label = "Controls", Kind = MenuItemKind.Submenu, Submenu = controlsPage },
                new MenuItemDefinition { ItemId = "view", Label = "View", Kind = MenuItemKind.Submenu, Submenu = viewPage },
                Row("back", "Back", MenuItemKind.Back)));

            PointMainMenuAtSettings(settingsMenu);

            AssetDatabase.SaveAssets();
            Debug.Log("[Settings] Built the setting definitions, the settings installer, the field of view contribution and the Settings menu.");
        }

        /// <summary>The main menu's Controls row (or an existing Settings row) opens the Settings menu.</summary>
        private static void PointMainMenuAtSettings(MenuDefinition settingsMenu)
        {
            var main = AssetDatabase.LoadAssetAtPath<MenuDefinition>(MainMenuPath);
            if (main == null)
            {
                Debug.LogWarning($"[Settings] {MainMenuPath} is missing; the Settings menu is not reachable.");
                return;
            }

            var serialized = new SerializedObject(main);
            var items = serialized.FindProperty("_items");
            for (int i = 0; i < items.arraySize; i++)
            {
                var item = items.GetArrayElementAtIndex(i);
                string id = item.FindPropertyRelative(nameof(MenuItemDefinition.ItemId)).stringValue;
                if (id != "controls" && id != "settings") continue;

                item.FindPropertyRelative(nameof(MenuItemDefinition.ItemId)).stringValue = "settings";
                item.FindPropertyRelative(nameof(MenuItemDefinition.Label)).stringValue = "Settings";
                item.FindPropertyRelative(nameof(MenuItemDefinition.Kind)).enumValueIndex = (int)MenuItemKind.Submenu;
                item.FindPropertyRelative(nameof(MenuItemDefinition.Submenu)).objectReferenceValue = settingsMenu;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(main);
                return;
            }

            Debug.LogWarning($"[Settings] {MainMenuPath} has no Controls or Settings row; add a Submenu row to Menu_Settings by hand.");
        }

        private static SettingDefinition Definition(string assetName, SettingDefinition built)
        {
            string path = $"{Folder}/{assetName}.asset";
            built.name = assetName;
            var existing = AssetDatabase.LoadAssetAtPath<SettingDefinition>(path);
            if (existing == null)
            {
                EnsureFolder(path);
                AssetDatabase.CreateAsset(built, path);
                return built;
            }

            EditorUtility.CopySerialized(built, existing);
            existing.name = assetName;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static MenuDefinition Menu(string path, MenuDefinition built)
        {
            built.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<MenuDefinition>(path);
            if (existing == null)
            {
                EnsureFolder(path);
                AssetDatabase.CreateAsset(built, path);
                return built;
            }

            EditorUtility.CopySerialized(built, existing);
            existing.name = built.name;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static MenuItemDefinition Row(string id, string label, MenuItemKind kind) => new() { ItemId = id, Label = label, Kind = kind };

        private static MenuItemDefinition SettingsRow(string id, SettingGroup group) =>
            new() { ItemId = id, Label = group.ToString(), Kind = MenuItemKind.Settings, SettingsGroup = group };

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                EnsureFolder(path);
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetList(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string assetPath) => EnsureDirectory(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));

        private static void EnsureDirectory(string? folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
