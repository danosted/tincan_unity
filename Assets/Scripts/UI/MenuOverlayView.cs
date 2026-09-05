#nullable enable
using TinCan.Core.Domain.Networking;
using TinCan.Features.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace TinCan.UI
{
    /// <summary>
    /// Throwaway presentation for <see cref="IMenuSystem"/>: rebuilds a plain UI Toolkit tree from the current
    /// MenuSnapshot on every change. Replace this class (not the menus) when real UI arrives.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MenuOverlayView : MonoBehaviour
    {
        private IMenuSystem? _menus;
        private INetworkService? _networkService;
        private UIDocument? _document;
        private VisualElement? _panel;

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

            if (_panel == null)
            {
                _panel = BuildPanel();
                root.Add(_panel);
            }

            _panel.Clear();
            var snapshot = _menus.Current;
            if (snapshot == null)
            {
                _panel.style.display = DisplayStyle.None;
                ApplyCursor(false);
                return;
            }

            _panel.style.display = DisplayStyle.Flex;
            ApplyCursor(true);

            _panel.Add(new Label(snapshot.Title) { style = { fontSize = 28, marginBottom = 12, unityFontStyleAndWeight = FontStyle.Bold } });
            foreach (var row in snapshot.Items)
            {
                _panel.Add(BuildRow(row));
            }
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
