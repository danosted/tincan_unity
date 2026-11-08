#nullable enable
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TinCan.Features.SkillChecks.UI
{
    /// <summary>
    /// The button combo (SkillCombo.uss): a title, a row of arrows to press in order (the pressed ones lit, the next one
    /// marked), and a line under it (the time left, or the grade). It never takes the pointer.
    /// </summary>
    [UxmlElement]
    public partial class SkillCombo : VisualElement
    {
        public static readonly string UssClassName = "tc-skill-combo";
        public static readonly string TitleUssClassName = UssClassName + "__title";
        public static readonly string RowUssClassName = UssClassName + "__row";
        public static readonly string StepUssClassName = UssClassName + "__step";
        public static readonly string DoneUssClassName = StepUssClassName + "--done";
        public static readonly string NextUssClassName = StepUssClassName + "--next";
        public static readonly string LineUssClassName = UssClassName + "__line";
        public static readonly string MissUssClassName = LineUssClassName + "--miss";
        public static readonly string HitUssClassName = LineUssClassName + "--hit";

        private readonly Label _title = new() { pickingMode = PickingMode.Ignore };
        private readonly VisualElement _row = new() { pickingMode = PickingMode.Ignore };
        private readonly List<Label> _steps = new();
        private readonly Label _line = new() { pickingMode = PickingMode.Ignore };

        public SkillCombo()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            _title.AddToClassList(TitleUssClassName);
            _row.AddToClassList(RowUssClassName);
            _line.AddToClassList(LineUssClassName);
            Add(_title);
            Add(_row);
            Add(_line);
        }

        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        /// <summary>The arrows, the first <paramref name="done"/> of them lit; the next is marked while <paramref name="running"/>.</summary>
        public void SetSteps(IReadOnlyList<ComboDirection> combo, int done, bool running)
        {
            while (_steps.Count < combo.Count)
            {
                var step = new Label { pickingMode = PickingMode.Ignore };
                step.AddToClassList(StepUssClassName);
                _steps.Add(step);
                _row.Add(step);
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                bool used = i < combo.Count;
                step.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                step.text = Arrow(combo[i]);
                step.EnableInClassList(DoneUssClassName, i < done);
                step.EnableInClassList(NextUssClassName, running && i == done);
            }
        }

        /// <summary>The line under the row; a grade line is marked as a hit or a miss.</summary>
        public void SetLine(string text, bool? hit = null)
        {
            _line.text = text;
            _line.EnableInClassList(HitUssClassName, hit == true);
            _line.EnableInClassList(MissUssClassName, hit == false);
        }

        private static string Arrow(ComboDirection direction) => direction switch
        {
            ComboDirection.Left => "←",
            ComboDirection.Right => "→",
            ComboDirection.Up => "↑",
            _ => "↓"
        };
    }
}
