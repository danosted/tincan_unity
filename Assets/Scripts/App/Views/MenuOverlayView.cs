#nullable enable
using System.Collections.Generic;
using System.Globalization;
using TinCan.Core.Domain;
using TinCan.Core.UI;
using TinCan.Core.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace TinCan.App.Views
{
    /// <summary>
    /// Throwaway presentation for <see cref="IMenuSystem"/>: composes the UI components (<see cref="MenuPanel"/>,
    /// <see cref="MenuButton"/>, <see cref="ChoiceField"/>, <see cref="Backdrop"/>, ...) from the current MenuSnapshot,
    /// keeping controls alive while values change. A modal request draws on the Modal layer, any other menu on the Menus
    /// layer; the backdrop shows when <see cref="MenuBackdropRules"/> says so. Looks come from the components' sheets, not
    /// from here. The cursor is <see cref="ICursorPolicy"/>'s. Replace this class when real UI arrives.
    /// </summary>
    public class MenuOverlayView : MonoBehaviour, IInjectedView
    {
        private IMenuSystem? _menus;
        private IUiLayers? _layers;
        private Backdrop? _backdrop;
        private MenuPanel? _panel;
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

            _backdrop ??= new Backdrop { name = "menu-backdrop" };
            _panel ??= new MenuPanel { name = "menu-panel" };

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
                    switch (_panel.Rows.ElementAt(i))
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
                        case ChoiceField choice:
                            choice.Value = value;
                            break;
                    }
                }
                _renderedSnapshot = snapshot;
                return;
            }

            _panel.Title = snapshot.Title;
            _panel.Rows.Clear();
            foreach (var row in snapshot.Items) _panel.Rows.Add(BuildRow(row));
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
                    return new MenuButton(BindingText(row), () => menus.Invoke(row.ItemId)) { Compact = true };
                case MenuItemKind.Slider:
                    return SliderRow(row, menus);
                case MenuItemKind.Choice:
                {
                    var choice = new ChoiceField(row.Label, row.Value);
                    // The field outlives value changes (the layout is kept), so it steps from the model's value, not the row's.
                    choice.Stepped += direction => menus.SetValue(row.ItemId, Step(row.Options, menus.GetValue(row.ItemId), direction));
                    return choice;
                }
                case MenuItemKind.Note:
                    return new MenuNote(row.Label);
                default:
                    return new MenuButton(row.Label, () => menus.Invoke(row.ItemId));
            }
        }

        private static string BindingText(MenuItemRow row) => $"{row.Label}  -  {row.Value}";

        /// <summary>A slider with its value in a field beside it; the model snaps the value to the row's step.</summary>
        private static VisualElement SliderRow(MenuItemRow row, IMenuSystem menus)
        {
            var slider = new Slider(row.Label, row.Min, row.Max) { value = ParseFloat(row.Value), showInputField = true };
            slider.RegisterValueChangedCallback(evt => menus.SetValue(row.ItemId, evt.newValue.ToString("0.###", CultureInfo.InvariantCulture)));
            // Keyboard arrows move it by one step.
            if (row.Step > 0f) slider.pageSize = row.Step;
            return slider;
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
    }
}
