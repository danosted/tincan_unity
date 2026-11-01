#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// Fills the screen behind a menu and dims it (Backdrop.uss). It takes the pointer, so nothing under a menu can be
    /// clicked.
    /// </summary>
    [UxmlElement]
    public partial class Backdrop : VisualElement
    {
        public static readonly string UssClassName = "tc-backdrop";

        public Backdrop()
        {
            AddToClassList(UssClassName);
        }
    }
}
