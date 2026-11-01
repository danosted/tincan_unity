#nullable enable
using TinCan.Core.UI;
using UnityEngine.UIElements;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>The layer containers under a root of the test's choosing (a document's, or a bare element).</summary>
    public sealed class FakeUiLayers : IUiLayers
    {
        private readonly VisualElement[] _layers;

        public FakeUiLayers(VisualElement? root = null)
        {
            Container = root ?? new VisualElement();
            _layers = UiLayers.AddLayers(Container);
        }

        /// <summary>The element the layers were added under.</summary>
        public VisualElement Container { get; }

        public VisualElement Root(UiLayer layer) => _layers[(int)layer];
    }
}
