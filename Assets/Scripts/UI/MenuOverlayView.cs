#nullable enable
using TinCan.Core.Domain.Networking;
using TinCan.Features.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace TinCan.UI
{
    /// <summary>
    /// Throwaway presentation for <see cref="IMenuSystem"/>: renders a plain UI Toolkit tree from the current
    /// MenuSnapshot, preserving controls while values change. Replace this class when real UI arrives.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MenuOverlayView : MonoBehaviour
    {
        private IMenuSystem? _menus;
        private INetworkService? _networkService;
        private UIDocument? _document;
        private VisualElement? _panel;
        private MenuSnapshot? _renderedSnapshot;

        [Inject]
        public void Construct(IMenuSystem menus, INetworkService networkService)
        {
            _menus = menus;
            _networkService = networkService;
            _menus.Changed += Render;
            Render();
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Render();
        }

        private void OnDestroy()
        {
            if (_menus != null) _menus.Changed -= Render;
        }

        private void Render()
        {
            if (_document == null || _menus == null) return;

            var root = _document.rootVisualElement;
            if (root == null) return;

            if (_panel == null || _panel.parent != root)
            {
                _panel = BuildPanel();
                _renderedSnapshot = null;
                root.Add(_panel);
            }

            var snapshot = _menus.Current;
            if (snapshot == null)
            {
                _renderedSnapshot = null;
                _panel.style.display = DisplayStyle.None;
                ApplyCursor(false);
                return;
            }

            _panel.style.display = DisplayStyle.Flex;
            ApplyCursor(true);

            if (HasSameLayout(snapshot))
            {
                // Keep the focused control and its caret/selection alive during text edits.
                for (int i = 0; i < snapshot.Items.Count; i++)
                {
                    var value = snapshot.Items[i].Value;
                    switch (_panel.ElementAt(i + 1))
                    {
                        case TextField field when field.value != value:
                            field.SetValueWithoutNotify(value);
                            break;
                        case Toggle toggle when toggle.value != (value == bool.TrueString):
                            toggle.SetValueWithoutNotify(value == bool.TrueString);
                            break;
                    }
                }
                _renderedSnapshot = snapshot;
                return;
            }

            _panel.Clear();
            _panel.Add(new Label(snapshot.Title) { style = { fontSize = 28, marginBottom = 12, unityFontStyleAndWeight = FontStyle.Bold } });
            foreach (var row in snapshot.Items)
            {
                _panel.Add(BuildRow(row));
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
                default:
                {
                    var button = new Button(() => menus.Invoke(row.ItemId)) { text = row.Label };
                    button.style.marginTop = 4;
                    button.style.height = 32;
                    return button;
                }
            }
        }

        private static VisualElement BuildPanel()
        {
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(50);
            panel.style.top = Length.Percent(50);
            panel.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            panel.style.minWidth = 320;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            panel.style.color = Color.white;
            return panel;
        }

        private void ApplyCursor(bool menuOpen)
        {
            if (menuOpen)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                return;
            }

            // Restore the in-game cursor only once a session is running; offline there is nothing to lock for.
            if (_networkService == null || !_networkService.IsActive) return;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }
}
