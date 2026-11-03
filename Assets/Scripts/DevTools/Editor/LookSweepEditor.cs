#nullable enable
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// A look sweep's inspector: its fields, a button that renders its sheet, and one button per variant that writes that
    /// variant into the base look (the pick; undoable). The buttons follow the variant list as it changes.
    /// </summary>
    [CustomEditor(typeof(LookSweep))]
    public sealed class LookSweepEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var sweep = (LookSweep)target;
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            root.Add(new Button(() => EditorUtility.RevealInFinder(LookSweepSheet.Render(sweep))) { text = "Render sheet" });

            var picks = new VisualElement { style = { marginTop = 8 } };
            root.Add(new Label("Pick a variant (writes it into the base look):") { style = { marginTop = 8 } });
            root.Add(picks);

            void Rebuild()
            {
                picks.Clear();
                for (int i = 0; i < sweep.Variants.Count; i++)
                {
                    int index = i;
                    picks.Add(new Button(() => LookSweepSheet.Apply(sweep, index))
                    {
                        text = $"Apply {LookSweepVariant.Letter(index)}: {sweep.Variants[index].Describe()}",
                    });
                }
            }

            Rebuild();
            root.TrackSerializedObjectValue(serializedObject, _ => Rebuild());
            return root;
        }
    }
}
