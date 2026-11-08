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
    /// This peer, per frame: draws the check <see cref="ISkillCheckRunner"/> runs as a <see cref="SkillGauge"/> on the HUD
    /// layer, then its grade for a moment; hidden otherwise. Built on first use.
    /// </summary>
    public sealed class SkillCheckGaugePresenter : ITickable, IDisposable
    {
        private readonly ISkillCheckRunner _runner;
        private readonly IUiLayers _layers;
        private readonly IInputBindings _bindings;
        private readonly SkillCheckInputContext _controls;
        private SkillGauge? _gauge;

        public SkillCheckGaugePresenter(ISkillCheckRunner runner, IUiLayers layers, IInputBindings bindings, SkillCheckInputContext controls)
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
            if (!shown)
            {
                if (_gauge != null) _gauge.style.display = DisplayStyle.None;
                return;
            }

            var gauge = Gauge();
            gauge.style.display = DisplayStyle.Flex;
            gauge.Title = definition!.Title;
            var band = _runner.Band;
            gauge.SetBand(band.Center, band.GoodWidth, band.PerfectWidth);
            gauge.SetNeedle(_runner.Needle);
            if (_runner.IsRunning) gauge.SetLine($"{Key()} in the band, {_runner.SecondsLeft.ToString("0.0", CultureInfo.InvariantCulture)} s");
            else gauge.SetLine(_runner.Shown!.Value.ToString() + "!", _runner.Shown != SkillCheckGrade.Miss);
        }

        private string Key() => _controls.Press != null ? _bindings.DescribeAction(_controls.Press) : "Press";

        public void Dispose() => _gauge?.RemoveFromHierarchy();

        private SkillGauge Gauge()
        {
            if (_gauge != null) return _gauge;
            _gauge = new SkillGauge();
            _layers.Root(UiLayer.Hud).Add(_gauge);
            return _gauge;
        }
    }
}
