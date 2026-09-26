#nullable enable
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan.DevTools
{
    /// <summary>Live harness readout for human playtests. F3 toggles it. Created at runtime by the telemetry use case.</summary>
    public sealed class NetHarnessOverlayView : MonoBehaviour
    {
        private static readonly Rect Area = new(10f, 10f, 520f, 170f);

        private Func<string>? _text;
        private bool _visible = true;
        private GUIStyle? _style;

        public static NetHarnessOverlayView Create(Func<string> text)
        {
            var host = new GameObject(nameof(NetHarnessOverlayView));
            DontDestroyOnLoad(host);
            var view = host.AddComponent<NetHarnessOverlayView>();
            view._text = text;
            return view;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _text == null) return;

            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = false };
            GUI.Box(Area, _text(), _style);
        }
    }
}
