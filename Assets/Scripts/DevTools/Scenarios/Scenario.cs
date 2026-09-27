#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// A feature test run in the live game, in three phases. The server arranges the world around the subject player
    /// (spawn, equip, break), the subject acts through real input and checks what it sees, and the server asserts the
    /// authoritative outcome. Phases synchronise through replicated state (WaitUntil), never through timing guesses.
    /// </summary>
    public sealed class Scenario
    {
        public string Name { get; }
        public string Description { get; }
        public float TimeoutSeconds { get; }
        /// <summary>The scene the scenario runs in (a project path, see <see cref="TestScenes"/>); null runs it in whatever scene is open.</summary>
        public string? ScenePath { get; }
        /// <summary>Seconds a host + client run holds the client back before it joins (0: at once), for late-join checks. Solo runs ignore it.</summary>
        public float ClientJoinDelaySeconds { get; }
        public IReadOnlyList<ScenarioStep> Arrange { get; }
        public IReadOnlyList<ScenarioStep> Act { get; }
        public IReadOnlyList<ScenarioStep> Assert { get; }

        private Scenario(string name, string description, float timeoutSeconds, string? scenePath, float clientJoinDelaySeconds, IReadOnlyList<ScenarioStep> arrange, IReadOnlyList<ScenarioStep> act, IReadOnlyList<ScenarioStep> assert)
        {
            Name = name;
            Description = description;
            TimeoutSeconds = timeoutSeconds;
            ScenePath = scenePath;
            ClientJoinDelaySeconds = clientJoinDelaySeconds;
            Arrange = arrange;
            Act = act;
            Assert = assert;
        }

        public IReadOnlyList<ScenarioStep> StepsFor(ScenarioRole role) => role switch
        {
            ScenarioRole.Server => Arrange.Concat(Assert).ToArray(),
            ScenarioRole.Subject => Act,
            _ => Array.Empty<ScenarioStep>()
        };

        public sealed class Builder
        {
            private readonly string _name;
            private string _description = string.Empty;
            private float _timeout = 60f;
            private string? _scenePath;
            private float _clientJoinDelay;
            private readonly ScenarioSteps _arrange = new();
            private readonly ScenarioSteps _act = new();
            private readonly ScenarioSteps _assert = new();

            public Builder(string name) => _name = name;

            public Builder Describe(string description)
            {
                _description = description;
                return this;
            }

            public Builder Timeout(float seconds)
            {
                _timeout = seconds;
                return this;
            }

            public Builder InScene(string? scenePath)
            {
                _scenePath = scenePath;
                return this;
            }

            /// <summary>The client joins this many seconds after launch, so the server acts before it is connected.</summary>
            public Builder JoinLate(float seconds)
            {
                _clientJoinDelay = seconds;
                return this;
            }

            public Builder Arrange(Action<ScenarioSteps> steps)
            {
                steps(_arrange);
                return this;
            }

            public Builder Act(Action<ScenarioSteps> steps)
            {
                steps(_act);
                return this;
            }

            public Builder Assert(Action<ScenarioSteps> steps)
            {
                steps(_assert);
                return this;
            }

            public Scenario Build() => new(_name, _description, _timeout, _scenePath, _clientJoinDelay, _arrange.ToArray(), _act.ToArray(), _assert.ToArray());
        }
    }
}
