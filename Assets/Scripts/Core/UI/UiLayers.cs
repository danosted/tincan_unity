#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.Core.UI
{
    /// <summary>
    /// <see cref="IUiLayers"/> on one UIDocument, made on first use: one container per <see cref="UiLayer"/>, added in
    /// enum order, so a later layer draws over an earlier one. The only place a UIDocument is created or sorted. The look
    /// is the panel settings' theme (Assets/UI/Theme/TinCan.tss: tokens, controls, core components) plus the component
    /// sheets the loaded features contribute (<see cref="UiStyleSheets"/>), added on the root.
    /// </summary>
    public sealed class UiLayers : IUiLayers, IDisposable
    {
        /// <summary>The class on the layers' root; the theme scopes its control styles under it.</summary>
        public const string RootClass = "tc-root";

        private readonly PanelSettings? _settings;
        private readonly UiStyleSheets _sheets;
        private readonly VisualElement[] _layers = new VisualElement[Enum.GetValues(typeof(UiLayer)).Length];
        private GameObject? _host;
        private bool _built;

        public UiLayers(PanelSettings? settings, UiStyleSheets sheets)
        {
            _settings = settings;
            _sheets = sheets;
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

        /// <summary>Adds the root class, the feature sheets and the layer containers under <paramref name="root"/>, back to front.</summary>
        public static VisualElement[] AddLayers(VisualElement root, IEnumerable<StyleSheet>? sheets = null)
        {
            root.pickingMode = PickingMode.Ignore;
            root.AddToClassList(RootClass);
            foreach (var sheet in sheets ?? Array.Empty<StyleSheet>()) root.styleSheets.Add(sheet);
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

            AddLayers(root, _sheets.All).CopyTo(_layers, 0);
        }
    }
}
