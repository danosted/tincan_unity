#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace TinCan.App.Views
{
    /// <summary>
    /// Throwaway presentation for <see cref="IMenuSystem"/>: renders a plain UI Toolkit tree from the current
    /// MenuSnapshot, preserving controls while values change. A modal request draws on the Modal layer, any other menu on
    /// the Menus layer; behind it a backdrop dims the scene and takes the pointer when <see cref="MenuBackdropRules"/>
    /// says so, so nothing under a menu can be clicked. The cursor is <see cref="ICursorPolicy"/>'s, not this view's.
    /// Replace this class when real UI arrives.
    /// </summary>
    public class MenuOverlayView : MonoBehaviour, IInjectedView
    {
        private static readonly Color Dim = new(0f, 0f, 0f, 0.45f);

        private IMenuSystem? _menus;
        private IUiLayers? _layers;
        private VisualElement? _backdrop;
        private VisualElement? _panel;
        private VisualElement? _rows;
        private MenuSnapshot? _renderedSnapshot;

        [Inject]
        public void Construct(IMenuSystem menus, IUiLayers layers)
        {
            _menus = menus;
            _layers = layers;
            _menus.Changed += Render;
            Render();
        }

        private void OnEnable()
        {
            Render();
        }

        private void OnDestroy()
        {
            if (_menus != null) _menus.Changed -= Render;
            _backdrop?.RemoveFromHierarchy();
            _panel?.RemoveFromHierarchy();
        }

        private void Render()
        {
            if (_layers == null || _menus == null) return;

            _backdrop ??= BuildBackdrop();
            _panel ??= BuildPanel();

            var snapshot = _menus.Current;
            if (snapshot == null)
            {
                _renderedSnapshot = null;
                _backdrop.style.display = DisplayStyle.None;
                _panel.style.display = DisplayStyle.None;
                return;
            }

            var layer = _layers.Root(snapshot.Kind == MenuKind.Modal ? UiLayer.Modal : UiLayer.Menus);
            if (_panel.parent != layer)
            {
                layer.Add(_backdrop);
                layer.Add(_panel);
            }

            bool dim = MenuBackdropRules.For(snapshot.Kind).DimScene;
            _backdrop.style.display = dim ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.style.display = DisplayStyle.Flex;

            if (HasSameLayout(snapshot))
            {
                // Keep the focused control and its caret/selection alive during text edits.
                for (int i = 0; i < snapshot.Items.Count; i++)
                {
                    var value = snapshot.Items[i].Value;
                    switch (_rows!.ElementAt(i))
                    {
                        case TextField field when field.value != value:
                            field.SetValueWithoutNotify(value);
                            break;
                        case Toggle toggle when toggle.value != (value == bool.TrueString):
                            toggle.SetValueWithoutNotify(value == bool.TrueString);
                            break;
                        case Button button when snapshot.Items[i].Kind == MenuItemKind.Binding:
                            button.text = BindingText(snapshot.Items[i]);
                            break;
                    }
                }
                _renderedSnapshot = snapshot;
                return;
            }

            _panel.Clear();
            _panel.Add(new Label(snapshot.Title) { style = { fontSize = 28, marginBottom = 12, unityFontStyleAndWeight = FontStyle.Bold } });
            // Long menus (Controls) scroll instead of running off the screen.
            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexShrink = 1 } };
            _panel.Add(scroll);
            _rows = scroll.contentContainer;
            foreach (var row in snapshot.Items)
            {
                _rows.Add(BuildRow(row));
            }
            _renderedSnapshot = snapshot;
        }

        private bool HasSameLayout(MenuSnapshot snapshot)
        {
            if (_renderedSnapshot == null || _renderedSnapshot.MenuId != snapshot.MenuId ||
                _renderedSnapshot.Title != snapshot.Title || _renderedSnapshot.Items.Count != snapshot.Items.Count) return false;

            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                var previous = _renderedSnapshot.Items[i];
                var current = snapshot.Items[i];
                if (previous.ItemId != current.ItemId || previous.Kind != current.Kind || previous.Label != current.Label) return false;
            }
            return true;
        }

        private VisualElement BuildRow(MenuItemRow row)
        {
            var menus = _menus!;
            switch (row.Kind)
            {
                case MenuItemKind.TextField:
                {
                    var field = new TextField(row.Label) { value = row.Value };
                    field.RegisterValueChangedCallback(evt => menus.SetValue(row.ItemId, evt.newValue));
                    return field;
                }
                case MenuItemKind.Toggle:
                {
                    var toggle = new Toggle(row.Label) { value = row.Value == bool.TrueString };
                    toggle.RegisterValueChangedCallback(evt => menus.SetValue(row.ItemId, evt.newValue ? bool.TrueString : bool.FalseString));
                    return toggle;
                }
                case MenuItemKind.Binding:
                {
                    var button = new Button(() => menus.Invoke(row.ItemId)) { text = BindingText(row) };
                    button.style.marginTop = 1;
                    button.style.height = 22;
                    button.style.unityTextAlign = TextAnchor.MiddleLeft;
                    return button;
                }
                case MenuItemKind.Note:
                    return new Label(row.Label) { style = { marginTop = 6, color = new Color(1f, 0.75f, 0.3f), whiteSpace = WhiteSpace.Normal } };
                default:
                {
                    var button = new Button(() => menus.Invoke(row.ItemId)) { text = row.Label };
                    button.style.marginTop = 4;
                    button.style.height = 32;
                    return button;
                }
            }
        }

        private static string BindingText(MenuItemRow row) => $"{row.Label}  -  {row.Value}";

        private static VisualElement BuildBackdrop()
        {
            // Takes the pointer (the default picking mode), so a click under a menu reaches nothing below it.
            var backdrop = new VisualElement { name = "menu-backdrop" };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = backdrop.style.top = backdrop.style.right = backdrop.style.bottom = 0;
            backdrop.style.backgroundColor = Dim;
            return backdrop;
        }

        private static VisualElement BuildPanel()
        {
            var panel = new VisualElement { name = "menu-panel" };
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(50);
            panel.style.top = Length.Percent(50);
            panel.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            panel.style.minWidth = 320;
            panel.style.maxHeight = Length.Percent(92);
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            panel.style.color = Color.white;
            return panel;
        }
    }
}
