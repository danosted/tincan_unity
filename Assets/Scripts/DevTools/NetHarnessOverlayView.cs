#nullable enable
using System;
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.DevTools
{
    /// <summary>
    /// Live harness readout for human playtests, top-left on the Dev layer (<see cref="IUiLayers"/>), over everything
    /// else. F3 toggles it (routed by the DevTools input context to <see cref="ToggleNetOverlayInputHandler"/>). Created
    /// at runtime by the telemetry use case; the text is read once a frame.
    /// </summary>
    public sealed class NetHarnessOverlayView : MonoBehaviour
    {
        private Func<string>? _text;
        private Label? _label;
        private bool _visible = true;

        public static NetHarnessOverlayView Create(Func<string> text, IUiLayers layers)
        {
            var host = new GameObject(nameof(NetHarnessOverlayView));
            DontDestroyOnLoad(host);
            var view = host.AddComponent<NetHarnessOverlayView>();
            view._text = text;
            view._label = new Label { name = "net-harness-overlay", pickingMode = PickingMode.Ignore };
            var style = view._label.style;
            style.position = Position.Absolute;
            style.left = style.top = 10;
            style.width = 560;
            style.minHeight = 210;
            style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = 6;
            style.fontSize = 13;
            style.whiteSpace = WhiteSpace.Normal;
            style.color = Color.white;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            layers.Root(UiLayer.Dev).Add(view._label);
            return view;
        }

        public void Toggle() => _visible = !_visible;

        private void Update()
        {
            if (_label == null || _text == null) return;

            _label.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_visible) _label.text = _text();
        }

        private void OnDestroy()
        {
            _label?.RemoveFromHierarchy();
        }
    }
}
