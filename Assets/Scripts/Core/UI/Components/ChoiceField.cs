#nullable enable
using System;
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// One of a few options: the label, then the option between a previous and a next arrow (ChoiceField.uss). The field
    /// only shows the value and reports the arrows (<see cref="Stepped"/>, -1 or +1); the owner decides the new value.
    /// </summary>
    [UxmlElement]
    public partial class ChoiceField : VisualElement
    {
        public static readonly string UssClassName = "tc-choice-field";
        public static readonly string LabelUssClassName = UssClassName + "__label";
        public static readonly string ArrowUssClassName = UssClassName + "__arrow";
        public static readonly string ValueUssClassName = UssClassName + "__value";

        private readonly Label _label = new();
        private readonly Label _value = new();

        public ChoiceField() : this(string.Empty, string.Empty) { }

        public ChoiceField(string label, string value)
        {
            AddToClassList(UssClassName);
            _label.AddToClassList(LabelUssClassName);
            _value.AddToClassList(ValueUssClassName);
            Add(_label);
            Add(Arrow("<", -1));
            Add(_value);
            Add(Arrow(">", 1));
            Label = label;
            Value = value;
        }

        /// <summary>An arrow was clicked: -1 for the previous option, +1 for the next.</summary>
        public event Action<int>? Stepped;

        [UxmlAttribute]
        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        [UxmlAttribute]
        public string Value
        {
            get => _value.text;
            set => _value.text = value;
        }

        private Button Arrow(string text, int direction)
        {
            var arrow = new Button(() => Stepped?.Invoke(direction)) { text = text };
            arrow.AddToClassList(ArrowUssClassName);
            return arrow;
        }
    }
}
