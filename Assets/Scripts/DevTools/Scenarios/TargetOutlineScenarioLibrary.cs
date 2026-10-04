#nullable enable
using System.Collections.Generic;
using TinCan.Features.TargetOutline;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Probe for the Interact target's outline, on the subject's peer: the outline presenter outlines a target whose name
    /// starts with the argument, and some of its meshes are on the outline layer. Passes trivially (with a note) when the
    /// profile does not load the TargetOutline feature. How it looks needs a human eye; the checkpoint captures show it.
    /// </summary>
    public sealed class TargetOutlineScenarioLibrary : IScenarioLibrary
    {
        private readonly TargetOutlinePresenter? _presenter;
        private readonly TargetOutlineConfig? _config;

        public TargetOutlineScenarioLibrary(IObjectResolver resolver)
        {
            resolver.TryResolve(out _presenter);
            resolver.TryResolve(out _config);
        }

        public IEnumerable<ScenarioCommand> Commands => System.Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("TargetOutlined", CheckOutlined)
        };

        private ScenarioCheck CheckOutlined(string objectName)
        {
            if (_presenter == null || _config == null) return ScenarioCheck.Pass("TargetOutline feature not loaded");

            var target = _presenter.Target;
            string detail = target != null ? $"outlining {target.name} ({_config.Highlighted} mesh(es))" : "outlining nothing";
            bool outlined = target != null && target.name.StartsWith(objectName, System.StringComparison.Ordinal) && _config.Highlighted > 0;
            return outlined ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }
    }
}
