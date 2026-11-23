#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Gives a prefab (or one of its children) the core item components in place, so its GUID and network id stay: a
    /// <see cref="RegrowingItemSourceNetworkMediator"/> (a vein, a woodpile) or an <see cref="ItemRackNetworkMediator"/>
    /// (a rack). Missing scripts anywhere in it (a retired feature component) are removed first: Unity will not save a
    /// prefab with one. Builders call it both for new prefabs and for ones that exist, so rebuilding brings old prefabs up
    /// to date. Rule: .docs/CODE_STANDARDS.md, "Interactions: capabilities, not handlers".
    /// </summary>
    public static class ItemTargetRefit
    {
        public static void Regrowing(string prefabPath, string? child, InteractionDefinition interaction, ItemDefinition item, int capacity,
            float regrowSeconds, bool takesBack, string visualPrefix) =>
            Refit<RegrowingItemSourceNetworkMediator>(prefabPath, child, fields =>
            {
                fields.FindProperty("_interactionDefinition").objectReferenceValue = interaction;
                fields.FindProperty("_item").objectReferenceValue = item;
                fields.FindProperty("_capacity").intValue = capacity;
                fields.FindProperty("_regrowSeconds").floatValue = regrowSeconds;
                fields.FindProperty("_takesBack").boolValue = takesBack;
                fields.FindProperty("_visualPrefix").stringValue = visualPrefix;
            });

        public static void Rack(string prefabPath, string? child, InteractionDefinition interaction, ItemDefinition item) =>
            Refit<ItemRackNetworkMediator>(prefabPath, child, fields =>
            {
                fields.FindProperty("_interactionDefinition").objectReferenceValue = interaction;
                fields.FindProperty("_item").objectReferenceValue = item;
            });

        private static void Refit<T>(string prefabPath, string? child, Action<SerializedObject> configure) where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var target = child == null ? root.transform : root.transform.FindDescendant(child)
                    ?? throw new InvalidOperationException($"{prefabPath} has no child '{child}'.");
                foreach (var each in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(each.gameObject);
                var component = target.GetComponent<T>() ?? target.gameObject.AddComponent<T>();
                var fields = new SerializedObject(component);
                configure(fields);
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
