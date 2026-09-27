#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Networking;
using TinCan.Features.HumanoidMovement;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>Steps every scenario can use: waiting for the subject, reading its gameplay tags, and who is connected.</summary>
    public sealed class CommonScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;
        private readonly IGameplayTagRegistry? _tags;

        public CommonScenarioLibrary(ScenarioSubject subject, IActorRegistry registry, INetworkService network, IObjectResolver resolver)
        {
            _subject = subject;
            _registry = registry;
            _network = network;
            _tags = resolver.TryResolve<IGameplayTagRegistry>(out var tagRegistry) ? tagRegistry : null;
        }

        public IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectReady", _ => _subject.Body != null
                ? ScenarioCheck.Pass()
                : ScenarioCheck.Fail("no subject player yet")),
            new ScenarioProbe("SubjectHasTag", tag => CheckTag(tag, expected: true)),
            new ScenarioProbe("SubjectLacksTag", tag => CheckTag(tag, expected: false)),
            new ScenarioProbe("TagRegistryActive", _ => _tags != null
                ? ScenarioCheck.Pass($"{_tags.All.Count} tags registered")
                : ScenarioCheck.Fail("no IGameplayTagRegistry registered (GameplayTagsFeatureInstaller missing or unassigned)")),
            new ScenarioProbe("NoRemotePlayers", _ => CheckNoRemotePlayers())
        };

        /// <summary>
        /// No player owned by another peer exists here yet. On the server of a late-join run, this proves the arrange steps
        /// before it happened before the client joined. Always passes in a solo run.
        /// </summary>
        private ScenarioCheck CheckNoRemotePlayers()
        {
            int remote = _registry.GetActors<IHumanoidCharacterView>()
                .Count(player => ((IPossessable)player).OwnerId is { } owner && owner != _network.LocalClientId);
            return remote == 0
                ? ScenarioCheck.Pass("no remote players")
                : ScenarioCheck.Fail($"{remote} remote player(s) already joined");
        }

        private ScenarioCheck CheckTag(string tagName, bool expected)
        {
            var tag = ScenarioTags.Find(tagName, _tags);
            if (tag == null) return ScenarioCheck.Fail($"no GameplayTag asset named '{tagName}' is loaded");

            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            bool has = subject.HasTag(tag);
            return has == expected
                ? ScenarioCheck.Pass($"{tagName} {(has ? "present" : "absent")}")
                : ScenarioCheck.Fail($"{tagName} {(has ? "present" : "absent")}");
        }
    }
}
