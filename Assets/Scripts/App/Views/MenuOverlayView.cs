#nullable enable
using System.Collections.Generic;
using System.Globalization;
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
        private const string ValueLabel = "row-value";

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
                        case Slider slider when Mathf.Abs(slider.value - ParseFloat(value)) > 0.0001f:
                            slider.SetValueWithoutNotify(ParseFloat(value));
                            break;
                        case VisualElement row when snapshot.Items[i].Kind is MenuItemKind.Slider or MenuItemKind.Choice:
                            var shown = row.Q<Label>(ValueLabel);
                            if (shown != null) shown.text = value;
                            break;
                    }
                }
                _renderedSnapshot = snapshot;
                return;
            }

            _panel.Clear();
            _panel.Add(Classed(new Label(snapshot.Title), "tc-menu__title"));
            // Long menus (Controls) scroll instead of running off the screen.
            var scroll = Classed(new ScrollView(ScrollViewMode.Vertical), "tc-menu__rows");
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
                    var field = Classed(new TextField(row.Label) { value = row.Value }, "tc-menu__field");
                    field.RegisterValueChangedCallback(evt => menus.SetValue(row.ItemId, evt.newValue));
                    return field;
                }
                case MenuItemKind.Toggle:
                {
                    var toggle = Classed(new Toggle(row.Label) { value = row.Value == bool.TrueString }, "tc-menu__field");
                    toggle.RegisterValueChangedCallback(evt => menus.SetValue(row.ItemId, evt.newValue ? bool.TrueString : bool.FalseString));
                    return toggle;
                }
                case MenuItemKind.Binding:
                    return Classed(new Button(() => menus.Invoke(row.ItemId)) { text = BindingText(row) }, "tc-menu__binding");
                case MenuItemKind.Slider:
                    return SliderRow(row, menus);
                case MenuItemKind.Choice:
                    return ChoiceRow(row, menus);
                case MenuItemKind.Note:
                    return Classed(new Label(row.Label), "tc-menu__note");
                default:
                    return Classed(new Button(() => menus.Invoke(row.ItemId)) { text = row.Label }, "tc-menu__button");
            }
        }

        /// <summary>Styling is the theme's (Assets/UI/Theme/TinCanTheme.uss): elements get classes, not colors or sizes.</summary>
        private static T Classed<T>(T element, params string[] classes) where T : VisualElement
        {
            foreach (var name in classes) element.AddToClassList(name);
            return element;
        }

        private static string BindingText(MenuItemRow row) => $"{row.Label}  -  {row.Value}";

        /// <summary>A slider with its value in a field beside it; the model snaps the value to the row's step.</summary>
        private static VisualElement SliderRow(MenuItemRow row, IMenuSystem menus)
        {
            var slider = Classed(new Slider(row.Label, row.Min, row.Max) { value = ParseFloat(row.Value), showInputField = true }, "tc-menu__field");
            slider.RegisterValueChangedCallback(evt => menus.SetValue(row.ItemId, evt.newValue.ToString("0.###", CultureInfo.InvariantCulture)));
            // Keyboard arrows move it by one step.
            if (row.Step > 0f) slider.pageSize = row.Step;
            return slider;
        }

        /// <summary>The label, then the option between a previous and a next button.</summary>
        private static VisualElement ChoiceRow(MenuItemRow row, IMenuSystem menus)
        {
            var line = Classed(new VisualElement(), "tc-menu__choice");
            line.Add(Classed(new Label(row.Label), "tc-menu__choice-label"));
            // The buttons outlive value changes (the layout is kept), so they step from the model's value, not the row's.
            line.Add(Classed(new Button(() => menus.SetValue(row.ItemId, Step(row.Options, menus.GetValue(row.ItemId), -1))) { text = "<" }, "tc-menu__choice-arrow"));
            line.Add(Classed(new Label(row.Value) { name = ValueLabel }, "tc-menu__choice-value"));
            line.Add(Classed(new Button(() => menus.SetValue(row.ItemId, Step(row.Options, menus.GetValue(row.ItemId), 1))) { text = ">" }, "tc-menu__choice-arrow"));
            return line;
        }

        /// <summary>The option <paramref name="direction"/> places from the current one, wrapping around.</summary>
        public static string Step(IReadOnlyList<string> options, string current, int direction)
        {
            if (options.Count == 0) return current;
            int index = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == current) index = i;
            }
            return options[((index + direction) % options.Count + options.Count) % options.Count];
        }

        private static float ParseFloat(string value) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ? number : 0f;

        private static VisualElement BuildBackdrop()
        {
            // Takes the pointer (the default picking mode), so a click under a menu reaches nothing below it.
            return Classed(new VisualElement { name = "menu-backdrop" }, "tc-backdrop");
        }

        private static VisualElement BuildPanel() => Classed(new VisualElement { name = "menu-panel" }, "tc-menu");
    }
}
