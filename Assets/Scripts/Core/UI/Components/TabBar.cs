#nullable enable
using System;
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>A row of tabs, one of them active (TabBar.uss). <see cref="AddTab"/> adds one.</summary>
    [UxmlElement]
    public partial class TabBar : VisualElement
    {
        public static readonly string UssClassName = "tc-tab-bar";
        public static readonly string TabUssClassName = UssClassName + "__tab";
        public static readonly string ActiveUssClassName = TabUssClassName + "--active";

        public TabBar()
        {
            AddToClassList(UssClassName);
        }

        public Button AddTab(string text, bool active, Action clicked)
        {
            var tab = new Button(clicked) { text = text };
            tab.AddToClassList(TabUssClassName);
            tab.EnableInClassList(ActiveUssClassName, active);
            Add(tab);
            return tab;
        }
    }
}
