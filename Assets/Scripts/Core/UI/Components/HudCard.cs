#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// The HUD's card in the top-left corner (HudCard.uss): it keeps readouts legible over bright sky and deck. Meters
    /// and lines go inside; <see cref="AddLine"/> adds a text line. It never takes the pointer.
    /// </summary>
    [UxmlElement]
    public partial class HudCard : VisualElement
    {
        public static readonly string UssClassName = "tc-hud-card";
        public static readonly string LineUssClassName = UssClassName + "__line";

        public HudCard()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
        }

        public Label AddLine(string text)
        {
            var line = new Label(text) { pickingMode = PickingMode.Ignore };
            line.AddToClassList(LineUssClassName);
            Add(line);
            return line;
        }
    }
}
