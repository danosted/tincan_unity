using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TinCan.Features.Interaction.Editor
{
    [CustomPropertyDrawer(typeof(HandlerTypeReferenceAttribute))]
    public class HandlerTypeReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var baseType = ((HandlerTypeReferenceAttribute)attribute).BaseType;
            var candidates = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name)
                .ToArray();
            var displayNames = candidates.Select(t => t.Name).Prepend("(None)").ToArray();

            // stringValue holds the selected handler's AssemblyQualifiedName, resolved back to a Type for the dropdown selection.
            var currentIndex = Array.FindIndex(candidates, t => t.AssemblyQualifiedName == property.stringValue) + 1;

            EditorGUI.BeginProperty(position, label, property);
            var selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, displayNames);
            property.stringValue = selectedIndex <= 0 ? string.Empty : candidates[selectedIndex - 1].AssemblyQualifiedName;
            EditorGUI.EndProperty();
        }
    }
}
