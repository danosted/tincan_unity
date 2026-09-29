#nullable enable
using System;
using System.Linq;
using TinCan.Core.Gas.Cues;
using UnityEditor;
using UnityEngine;

namespace TinCan.Core.Gas.Editor
{
    /// <summary>
    /// A type picker for each <see cref="GameplayCueAction"/> in a notify's lists, then that action's own fields. Every
    /// concrete subclass appears on its own, so a new action type needs no editor change.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayCueActionPickerAttribute))]
    public sealed class GameplayCueActionPickerPropertyDrawer : PropertyDrawer
    {
        private static Type[]? _types;
        private static string[]? _names;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            foreach (var child in Children(property)) height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label.text, "Use with [SerializeReference].");
                return;
            }

            EnsureTypes();
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            int current = Array.IndexOf(_types!, property.managedReferenceValue?.GetType()) + 1;
            int selected = EditorGUI.Popup(line, label.text, current, _names!);
            if (selected != current)
            {
                property.managedReferenceValue = selected == 0 ? null : Activator.CreateInstance(_types![selected - 1]);
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.indentLevel++;
            float y = line.yMax;
            foreach (var child in Children(property))
            {
                float height = EditorGUI.GetPropertyHeight(child, true);
                y += EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                y += height;
            }
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        private static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference || property.managedReferenceValue == null) yield break;

            var child = property.Copy();
            var end = property.GetEndProperty();
            if (!child.NextVisible(true)) yield break;
            while (!SerializedProperty.EqualContents(child, end))
            {
                yield return child.Copy();
                if (!child.NextVisible(false)) yield break;
            }
        }

        private static void EnsureTypes()
        {
            if (_types != null) return;
            _types = TypeCache.GetTypesDerivedFrom<GameplayCueAction>()
                .Where(type => !type.IsAbstract && !type.IsGenericType && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToArray();
            _names = _types.Select(type => ObjectNames.NicifyVariableName(type.Name.Replace("CueAction", string.Empty))).Prepend("(None)").ToArray();
        }
    }
}
