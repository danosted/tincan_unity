#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.Core.UI
{
    /// <summary>
    /// <see cref="IUiLayers"/> on one UIDocument, made on first use: one container per <see cref="UiLayer"/>, added in
    /// enum order, so a later layer draws over an earlier one. The only place a UIDocument is created or sorted. The theme
    /// (<c>Assets/UI/Theme/TinCanTheme.uss</c>) is loaded on the root, so every layer takes its tokens and classes.
    /// </summary>
    public sealed class UiLayers : IUiLayers, IDisposable
    {
        /// <summary>The class the theme's tokens are defined on; every layer sits under it.</summary>
        public const string RootClass = "tc-root";

        private readonly PanelSettings? _settings;
        private readonly StyleSheet? _theme;
        private readonly VisualElement[] _layers = new VisualElement[Enum.GetValues(typeof(UiLayer)).Length];
        private GameObject? _host;
        private bool _built;

        public UiLayers(PanelSettings? settings, StyleSheet? theme)
        {
            _settings = settings;
            _theme = theme;
        }

        public VisualElement Root(UiLayer layer)
        {
            if (!_built) Build();
            return _layers[(int)layer];
        }

        public void Dispose()
        {
            if (_host == null) return;
            if (Application.isPlaying) Object.Destroy(_host);
            else Object.DestroyImmediate(_host);
            _host = null;
        }

        /// <summary>Adds the theme and the layer containers under <paramref name="root"/>, back to front.</summary>
        public static VisualElement[] AddLayers(VisualElement root, StyleSheet? theme = null)
        {
            root.pickingMode = PickingMode.Ignore;
            root.AddToClassList(RootClass);
            if (theme != null) root.styleSheets.Add(theme);
            var layers = new VisualElement[Enum.GetValues(typeof(UiLayer)).Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = new VisualElement { name = "ui-layer-" + (UiLayer)i, pickingMode = PickingMode.Ignore };
                layer.style.position = Position.Absolute;
                layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
                root.Add(layer);
                layers[i] = layer;
            }
            return layers;
        }

        private void Build()
        {
            _built = true;
            VisualElement root;
            if (_settings == null)
            {
                // Nothing draws, but views still have somewhere to put their elements.
                Debug.LogWarning("[UiLayers] No PanelSettings on the UI feature installer; screen UI will not show.");
                root = new VisualElement();
            }
            else
            {
                _host = new GameObject("UiLayers");
                _host.SetActive(false);
                var document = _host.AddComponent<UIDocument>();
                document.panelSettings = _settings;
                document.sortingOrder = 0;
                _host.SetActive(true);
                root = document.rootVisualElement;
            }

            if (_theme == null) Debug.LogWarning("[UiLayers] No theme on the UI feature installer; screen UI falls back to Unity's default look.");
            AddLayers(root, _theme).CopyTo(_layers, 0);
        }
    }
}
