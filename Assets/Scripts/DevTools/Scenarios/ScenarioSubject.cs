#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Networking;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// The player a scenario is about. On the subject peer and in a solo run it is the local player; on the server of
    /// a host + client run it is the first player owned by a remote client, so server-side arrange steps act on the
    /// client's player and the client verifies what it sees.
    /// </summary>
    public sealed class ScenarioSubject
    {
        private readonly HarnessOptions _options;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;

        public ScenarioSubject(HarnessOptions options, IActorRegistry registry, INetworkService network)
        {
            _options = options;
            _registry = registry;
            _network = network;
        }

        public IHumanoidCharacterView? Resolve()
        {
            if (_options.ScenarioSolo || !_network.IsServer) return _registry.GetLocalPlayerActor<IHumanoidCharacterView>();

            return _registry.GetActors<IHumanoidCharacterView>()
                .FirstOrDefault(player => ((IPossessable)player).OwnerId is { } owner && owner != _network.LocalClientId);
        }

        /// <summary>The subject's simulated body. Null until the player has spawned.</summary>
        public Transform? Body => Resolve()?.Movement?.Transform;
    }

    /// <summary>
    /// Finds gameplay tag assets by name for scenario arguments. Dev-only: it scans loaded assets, the same
    /// approach the ability mediator uses for tag RPCs today, until a tag registry replaces both.
    /// </summary>
    public static class ScenarioTags
    {
        private static readonly Dictionary<string, GameplayTag> Cache = new(StringComparer.Ordinal);

        public static GameplayTag? Find(string name)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;

            var tag = Resources.FindObjectsOfTypeAll<GameplayTag>().FirstOrDefault(candidate => candidate.name == name);
            if (tag != null) Cache[name] = tag;
            return tag;
        }
    }

    /// <summary>Steps every scenario can use: waiting for the subject, and reading its gameplay tags.</summary>
    public sealed class CommonScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;

        public CommonScenarioLibrary(ScenarioSubject subject) => _subject = subject;

        public IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectReady", _ => _subject.Body != null
                ? ScenarioCheck.Pass()
                : ScenarioCheck.Fail("no subject player yet")),
            new ScenarioProbe("SubjectHasTag", tag => CheckTag(tag, expected: true)),
            new ScenarioProbe("SubjectLacksTag", tag => CheckTag(tag, expected: false))
        };

        private ScenarioCheck CheckTag(string tagName, bool expected)
        {
            var tag = ScenarioTags.Find(tagName);
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
