#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// Keys on the left, what they do on the right (KeyHintTable.uss). Pass key names from
    /// <c>IInputBindings.DescribeAction</c>, never typed text, so a rebinding shows.
    /// </summary>
    [UxmlElement]
    public partial class KeyHintTable : VisualElement
    {
        public static readonly string UssClassName = "tc-key-hints";
        public static readonly string RowUssClassName = UssClassName + "__row";
        public static readonly string KeyUssClassName = UssClassName + "__key";
        public static readonly string ActionUssClassName = UssClassName + "__action";

        public KeyHintTable()
        {
            AddToClassList(UssClassName);
        }

        public void AddRow(string keys, string action)
        {
            var row = new VisualElement();
            row.AddToClassList(RowUssClassName);
            var key = new Label(keys);
            key.AddToClassList(KeyUssClassName);
            var what = new Label(action);
            what.AddToClassList(ActionUssClassName);
            row.Add(key);
            row.Add(what);
            Add(row);
        }
    }
}
