#nullable enable
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// A rounded surface that groups a panel's content (Card.uss). <see cref="Grow"/> lets it shrink to fit a column;
    /// <see cref="Below"/> spaces it under the card above.
    /// </summary>
    [UxmlElement]
    public partial class Card : VisualElement
    {
        public static readonly string UssClassName = "tc-card";
        public static readonly string GrowUssClassName = UssClassName + "--grow";
        public static readonly string BelowUssClassName = UssClassName + "--below";
        public static readonly string HeadingUssClassName = UssClassName + "__heading";
        public static readonly string HintUssClassName = UssClassName + "__hint";

        public Card()
        {
            AddToClassList(UssClassName);
        }

        [UxmlAttribute]
        public bool Grow
        {
            get => ClassListContains(GrowUssClassName);
            set => EnableInClassList(GrowUssClassName, value);
        }

        [UxmlAttribute]
        public bool Below
        {
            get => ClassListContains(BelowUssClassName);
            set => EnableInClassList(BelowUssClassName, value);
        }

        /// <summary>A bold, muted heading line.</summary>
        public static Label Heading(string text)
        {
            var heading = new Label(text);
            heading.AddToClassList(HeadingUssClassName);
            return heading;
        }

        /// <summary>A muted, wrapping hint.</summary>
        public static Label Hint(string text)
        {
            var hint = new Label(text);
            hint.AddToClassList(HintUssClassName);
            return hint;
        }
    }
}
