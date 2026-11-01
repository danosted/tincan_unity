#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// A menu's card in the middle of the screen: its title, then its rows in a scroll view so long menus fit
    /// (MenuPanel.uss). Rows are any elements; fields inside get the menu's spacing.
    /// </summary>
    [UxmlElement]
    public partial class MenuPanel : VisualElement
    {
        public static readonly string UssClassName = "tc-menu-panel";
        public static readonly string TitleUssClassName = UssClassName + "__title";
        public static readonly string RowsUssClassName = UssClassName + "__rows";

        private readonly Label _title = new();
        private readonly ScrollView _scroll = new(ScrollViewMode.Vertical);

        public MenuPanel()
        {
            AddToClassList(UssClassName);
            _title.AddToClassList(TitleUssClassName);
            _scroll.AddToClassList(RowsUssClassName);
            hierarchy.Add(_title);
            hierarchy.Add(_scroll);
        }

        [UxmlAttribute]
        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        /// <summary>Where the rows go.</summary>
        public VisualElement Rows => _scroll.contentContainer;

        public override VisualElement contentContainer => _scroll.contentContainer;
    }
}
