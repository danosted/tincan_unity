#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI
{
    /// <summary>
    /// The one screen-space UI surface. A view adds its elements under <see cref="Root"/> of its layer instead of creating
    /// a UIDocument or typing a sort order (an architecture rule checks this).
    /// </summary>
    public interface IUiLayers
    {
        /// <summary>The container for a layer: fills the screen, ignores the pointer itself, drawn in <see cref="UiLayer"/> order.</summary>
        VisualElement Root(UiLayer layer);
    }
}
