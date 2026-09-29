#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// The list of every <see cref="GameplayTag"/> asset in the project, so the tag registry can resolve names at
    /// runtime without scanning loaded objects. In the Editor it keeps itself complete: it refreshes when validated
    /// and whenever a tag asset is imported, moved or deleted (<see cref="GameplayTagDatabasePostprocessor"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagDatabase", menuName = "TinCan/Abilities/Tag Database")]
    public class GameplayTagDatabase : ScriptableObject
    {
        [SerializeField] private List<GameplayTag> _tags = new();

        public IReadOnlyList<GameplayTag> Tags => _tags;

#if UNITY_EDITOR
        private void OnValidate() => Refresh();

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            var found = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(GameplayTag))
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayTag>)
                .Where(tag => tag != null)
                .OrderBy(tag => tag.name, System.StringComparer.Ordinal)
                .ToList();

            if (found.SequenceEqual(_tags)) return;

            _tags = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>Keeps every <see cref="GameplayTagDatabase"/> in step with the tag assets on disk.</summary>
    public class GameplayTagDatabasePostprocessor : UnityEditor.AssetPostprocessor
    {
        // Refreshes inside the callback rather than via EditorApplication.delayCall: an unfocused Editor may not tick
        // for a long time, and the database then stayed stale after a tag was created.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touchesTags =
                imported.Concat(moved).Any(path => UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(GameplayTag)) ||
                deleted.Any(path => path.EndsWith(".asset"));
            if (!touchesTags) return;

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(GameplayTagDatabase)))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(path)?.Refresh();
            }
        }
    }
#endif
}
