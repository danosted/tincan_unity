#nullable enable
using System;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>A scenario plus the step libraries it needs, registered only when it is the one being run.</summary>
    public sealed class ScenarioEntry
    {
        public Scenario Scenario { get; }
        public Action<IContainerBuilder> RegisterLibraries { get; }

        public ScenarioEntry(Scenario scenario, Action<IContainerBuilder> registerLibraries)
        {
            Scenario = scenario;
            RegisterLibraries = registerLibraries;
        }
    }
}
