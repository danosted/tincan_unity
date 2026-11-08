#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.Features.SkillChecks.UI
{
    /// <summary>
    /// The pulsing gauge (SkillGauge.uss): a title, a track with the band (its Good part and the Perfect part in its
    /// middle) and the needle, and a line under it (the time left, or the grade). Positions come from data, so they are
    /// the styles set in code. It never takes the pointer.
    /// </summary>
    [UxmlElement]
    public partial class SkillGauge : VisualElement
    {
        public static readonly string UssClassName = "tc-skill-gauge";
        public static readonly string TitleUssClassName = UssClassName + "__title";
        public static readonly string TrackUssClassName = UssClassName + "__track";
        public static readonly string GoodUssClassName = UssClassName + "__good";
        public static readonly string PerfectUssClassName = UssClassName + "__perfect";
        public static readonly string NeedleUssClassName = UssClassName + "__needle";
        public static readonly string LineUssClassName = UssClassName + "__line";
        public static readonly string MissUssClassName = LineUssClassName + "--miss";
        public static readonly string HitUssClassName = LineUssClassName + "--hit";

        private readonly Label _title = new() { pickingMode = PickingMode.Ignore };
        private readonly VisualElement _good = new() { pickingMode = PickingMode.Ignore };
        private readonly VisualElement _perfect = new() { pickingMode = PickingMode.Ignore };
        private readonly VisualElement _needle = new() { pickingMode = PickingMode.Ignore };
        private readonly Label _line = new() { pickingMode = PickingMode.Ignore };

        public SkillGauge()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            _title.AddToClassList(TitleUssClassName);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList(TrackUssClassName);
            _good.AddToClassList(GoodUssClassName);
            _perfect.AddToClassList(PerfectUssClassName);
            _needle.AddToClassList(NeedleUssClassName);
            track.Add(_good);
            track.Add(_perfect);
            track.Add(_needle);
            _line.AddToClassList(LineUssClassName);
            Add(_title);
            Add(track);
            Add(_line);
        }

        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        public void SetBand(float center, float goodWidth, float perfectWidth)
        {
            Place(_good, center - goodWidth * 0.5f, goodWidth);
            Place(_perfect, center - perfectWidth * 0.5f, perfectWidth);
        }

        /// <summary>The needle, 0 to 1 along the track.</summary>
        public void SetNeedle(float at) => _needle.style.left = Length.Percent(Mathf.Clamp01(at) * 100f);

        /// <summary>The line under the track; a grade line is marked as a hit or a miss.</summary>
        public void SetLine(string text, bool? hit = null)
        {
            _line.text = text;
            _line.EnableInClassList(HitUssClassName, hit == true);
            _line.EnableInClassList(MissUssClassName, hit == false);
        }

        private static void Place(VisualElement element, float from, float width)
        {
            float start = Mathf.Clamp01(from);
            element.style.left = Length.Percent(start * 100f);
            element.style.width = Length.Percent((Mathf.Clamp01(from + width) - start) * 100f);
        }
    }
}
