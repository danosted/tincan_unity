#nullable enable
using System;
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// A menu row that does something when clicked (MenuButton.uss). <see cref="Compact"/> is the dense left-aligned
    /// variant the key binding rows use.
    /// </summary>
    [UxmlElement]
    public partial class MenuButton : Button
    {
        public static readonly string UssClassName = "tc-menu-button";
        public static readonly string CompactUssClassName = UssClassName + "--compact";

        public MenuButton() : this(string.Empty, null) { }

        public MenuButton(string label, Action? clicked) : base(clicked)
        {
            text = label;
            AddToClassList(UssClassName);
        }

        [UxmlAttribute]
        public bool Compact
        {
            get => ClassListContains(CompactUssClassName);
            set => EnableInClassList(CompactUssClassName, value);
        }
    }
}
