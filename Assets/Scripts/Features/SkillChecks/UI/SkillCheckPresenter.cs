#nullable enable
using System;
using System.Globalization;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace TinCan.Features.SkillChecks.UI
{
    /// <summary>
    /// This peer, per frame: draws the check <see cref="ISkillCheckRunner"/> runs on the HUD layer (a
    /// <see cref="SkillGauge"/> for the pulsing gauge, a <see cref="SkillCombo"/> for the button combo), then its grade
    /// for a moment; hidden otherwise. Each view is built on first use.
    /// </summary>
    public sealed class SkillCheckPresenter : ITickable, IDisposable
    {
        private readonly ISkillCheckRunner _runner;
        private readonly IUiLayers _layers;
        private readonly IInputBindings _bindings;
        private readonly SkillCheckInputContext _controls;
        private SkillGauge? _gauge;
        private SkillCombo? _combo;

        public SkillCheckPresenter(ISkillCheckRunner runner, IUiLayers layers, IInputBindings bindings, SkillCheckInputContext controls)
        {
            _runner = runner;
            _layers = layers;
            _bindings = bindings;
            _controls = controls;
        }

        public void Tick()
        {
            var definition = _runner.Definition;
            bool shown = definition != null && (_runner.IsRunning || _runner.Shown != null);
            bool combo = shown && definition!.Kind == SkillCheckKind.Combo;
            if (_gauge != null) _gauge.style.display = shown && !combo ? DisplayStyle.Flex : DisplayStyle.None;
            if (_combo != null) _combo.style.display = combo ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) return;

            if (combo) DrawCombo(definition!);
            else DrawGauge(definition!);
        }

        private void DrawGauge(SkillCheckDefinition definition)
        {
            var gauge = _gauge ??= Add(new SkillGauge());
            gauge.style.display = DisplayStyle.Flex;
            gauge.Title = definition.Title;
            var band = _runner.Band;
            gauge.SetBand(band.Center, band.GoodWidth, band.PerfectWidth);
            gauge.SetNeedle(_runner.Needle);
            if (_runner.IsRunning) gauge.SetLine($"{Key()} in the band, {Seconds()} s");
            else gauge.SetLine(_runner.Shown!.Value + "!", _runner.Shown != SkillCheckGrade.Miss);
        }

        private void DrawCombo(SkillCheckDefinition definition)
        {
            var combo = _combo ??= Add(new SkillCombo());
            combo.style.display = DisplayStyle.Flex;
            combo.Title = definition.Title;
            combo.SetSteps(_runner.Combo, _runner.ComboDone, _runner.IsRunning);
            if (_runner.IsRunning) combo.SetLine($"Press the arrows in order, {Seconds()} s");
            else combo.SetLine(_runner.Shown!.Value + "!", _runner.Shown != SkillCheckGrade.Miss);
        }

        private string Seconds() => _runner.SecondsLeft.ToString("0.0", CultureInfo.InvariantCulture);

        private string Key() => _controls.Press != null ? _bindings.DescribeAction(_controls.Press) : "Press";

        public void Dispose()
        {
            _gauge?.RemoveFromHierarchy();
            _combo?.RemoveFromHierarchy();
        }

        private T Add<T>(T view) where T : VisualElement
        {
            _layers.Root(UiLayer.Hud).Add(view);
            return view;
        }
    }
}
