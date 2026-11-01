#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.Core.UI.Components
{
    /// <summary>
    /// A labelled bar (Meter.uss): the width is <see cref="Fill"/>, the colour the theme's band for it (good, fair below
    /// 60 %, bad below 25 %). It never takes the pointer.
    /// </summary>
    [UxmlElement]
    public partial class Meter : VisualElement
    {
        public static readonly string UssClassName = "tc-meter";
        public static readonly string LabelUssClassName = UssClassName + "__label";
        public static readonly string TrackUssClassName = UssClassName + "__track";
        public static readonly string FillUssClassName = UssClassName + "__fill";
        public static readonly string FairUssClassName = FillUssClassName + "--fair";
        public static readonly string BadUssClassName = FillUssClassName + "--bad";

        private readonly Label _label = new() { pickingMode = PickingMode.Ignore };
        private readonly VisualElement _fill = new() { pickingMode = PickingMode.Ignore };
        private float _value;

        public Meter() : this(string.Empty, 1f) { }

        public Meter(string label, float fill)
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            _label.AddToClassList(LabelUssClassName);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList(TrackUssClassName);
            _fill.AddToClassList(FillUssClassName);
            track.Add(_fill);
            Add(_label);
            Add(track);
            Label = label;
            Fill = fill;
        }

        [UxmlAttribute]
        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        /// <summary>How full, 0 to 1 (clamped).</summary>
        [UxmlAttribute]
        public float Fill
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp01(value);
                // The width comes from data, so it is the one style set in code.
                _fill.style.width = Length.Percent(_value * 100f);
                var band = Band(_value);
                _fill.EnableInClassList(FairUssClassName, band == FairUssClassName);
                _fill.EnableInClassList(BadUssClassName, band == BadUssClassName);
            }
        }

        /// <summary>The fill's band class for a value: none (good), fair below 60 %, bad below 25 %.</summary>
        public static string? Band(float fill) => fill switch
        {
            < 0.25f => BadUssClassName,
            < 0.6f => FairUssClassName,
            _ => null
        };
    }
}
