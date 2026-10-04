#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Core.UI;
using TinCan.Features.Shipyard;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Shipyard > Build Assets: the shipyard's config, its ghost and floor materials (created only when
    /// missing), Menu_Shipyard (name, save, load, new, launch, exit, resume), the installer, and its place in
    /// Profile_Test_Shipyard. Then it rebuilds the input assets, which give the installer its context. Run
    /// TinCan > Dev > Ship Designs > Build Assets first. Plan: .docs/plans/modular-airship-builder.md (S3).
    /// </summary>
    public static class ShipyardAssetBuilder
    {
        private const string ConfigPath = "Assets/Settings/Shipyard/ShipyardConfig.asset";
        private const string GhostPath = "Assets/Materials/Shipyard/M_ShipyardGhost.mat";
        private const string FloorPath = "Assets/Materials/Shipyard/M_ShipyardFloor.mat";
        private const string MenuPath = "Assets/UI/Menus/Menu_Shipyard.asset";
        private const string InstallerPath = "Assets/Resources/Installers/ShipyardFeatureInstaller.asset";

        [MenuItem("TinCan/Dev/Shipyard/Build Assets")]
        public static void Build()
        {
            var ghost = Material(GhostPath, new Color(1f, 1f, 1f, 0.45f), transparent: true);
            var floor = Material(FloorPath, new Color(0.16f, 0.22f, 0.3f, 1f), transparent: false);
            var menu = Menu();

            var config = Asset<ShipyardConfig>(ConfigPath, c =>
            {
                c.GhostMaterial = ghost;
                c.FloorMaterial = floor;
                c.Menu = menu;
            });
            var installer = Asset<ShipyardFeatureInstaller>(InstallerPath, i =>
            {
                var fields = new SerializedObject(i);
                fields.FindProperty("_config").objectReferenceValue = config;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var profile = AssetDatabase.LoadAssetAtPath<FeatureProfile>(ShipDesignsAssetBuilder.ProfilePath)
                          ?? throw new InvalidOperationException("Run TinCan > Dev > Ship Designs > Build Assets first.");
            AddUnique(profile, "_installers", installer);

            AssetDatabase.SaveAssets();
            InputAssetBuilder.Build();
            Debug.Log("[ShipyardAssetBuilder] Shipyard assets built.");
        }

        private static MenuDefinition Menu()
        {
            var built = MenuDefinition.Create("Shipyard", "Shipyard",
                Row(ShipyardUseCase.NameField, "Name", MenuItemKind.TextField),
                Row("save", "Save", MenuItemKind.Command, ShipyardSaveMenuCommand.Id),
                Row("load", "Load by name", MenuItemKind.Command, ShipyardLoadMenuCommand.Id),
                Row("new", "New ship", MenuItemKind.Command, ShipyardNewMenuCommand.Id),
                Row("launch", "Launch", MenuItemKind.Command, ShipyardLaunchMenuCommand.Id),
                Row("exit", "Leave shipyard", MenuItemKind.Command, ShipyardExitMenuCommand.Id),
                Row("back", "Resume building", MenuItemKind.Back));
            built.name = Path.GetFileNameWithoutExtension(MenuPath);

            var existing = AssetDatabase.LoadAssetAtPath<MenuDefinition>(MenuPath);
            if (existing == null)
            {
                EnsureFolder(MenuPath);
                AssetDatabase.CreateAsset(built, MenuPath);
                return built;
            }

            EditorUtility.CopySerialized(built, existing);
            existing.name = built.name;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static MenuItemDefinition Row(string id, string label, MenuItemKind kind, string command = "") =>
            new() { ItemId = id, Label = label, Kind = kind, CommandId = command, DefaultValue = string.Empty };

        private static Material Material(string path, Color colour, bool transparent)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("URP Unlit shader not found.");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", colour);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            EnsureFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

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

        private static void AddUnique(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            }

            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
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
