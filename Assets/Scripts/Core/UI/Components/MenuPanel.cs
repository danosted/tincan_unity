#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// A menu's card in the middle of the screen: its title, then its rows in a scroll view so long menus fit
    /// (MenuPanel.uss). Rows are any elements; fields inside get the menu's spacing. <see cref="ShowFocus"/> shows the row
    /// the keyboard or gamepad is on. Menu keys come through the Menu input context; UI Toolkit's own navigation is
    /// stopped for the whole panel (<see cref="UiLayers.StopBuiltInNavigation"/>), so there is one focus.
    /// </summary>
    [UxmlElement]
    public partial class MenuPanel : VisualElement
    {
        public static readonly string UssClassName = "tc-menu-panel";
        public static readonly string TitleUssClassName = UssClassName + "__title";
        public static readonly string RowsUssClassName = UssClassName + "__rows";
        public static readonly string FocusedRowUssClassName = UssClassName + "__row--focused";

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

        /// <summary>
        /// Shows row <paramref name="index"/> (-1 for none) as the focused one: it gets the focus class and UI Toolkit's
        /// own focus too, so the theme's :focus look and the class never point at different rows, and a text field there
        /// takes the typing. A row that cannot hold focus (a choice) takes it away from the others. Scrolls it into view.
        /// </summary>
        public void ShowFocus(int index)
        {
            VisualElement? focused = null;
            for (int i = 0; i < Rows.childCount; i++)
            {
                var row = Rows[i];
                bool on = i == index;
                row.EnableInClassList(FocusedRowUssClassName, on);
                if (on) focused = row;
            }

            if (focused == null || panel == null) return;
            _scroll.ScrollTo(focused);

            var current = focusController?.focusedElement as VisualElement;
            if (current != null && (current == focused || focused.Contains(current))) return;
            if (focused.canGrabFocus) focused.Focus();
            else current?.Blur();
        }
    }
}
