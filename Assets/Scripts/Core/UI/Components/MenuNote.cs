#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>A line of text in a menu that tells the player something (why a key was refused), MenuNote.uss.</summary>
    [UxmlElement]
    public partial class MenuNote : Label
    {
        public static readonly string UssClassName = "tc-menu-note";

        public MenuNote() : this(string.Empty) { }

        public MenuNote(string text) : base(text)
        {
            AddToClassList(UssClassName);
        }
    }
}
