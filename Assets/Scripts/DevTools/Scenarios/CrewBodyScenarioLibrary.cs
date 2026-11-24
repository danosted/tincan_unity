#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Features.CrewBody;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for the crew body (.docs/plans/crew-animation.md, S2). Presentation, measured on each peer, so
    /// every peer checks the subject's body as it draws it. Probes: SubjectBodyGait &lt;Idle|Run|Sprint|Airborne&gt;:
    /// the subject's body shows that gait here; SubjectBodyHiddenFromOwner: on the subject's own peer, its body is
    /// drawn and casts shadows only (the first-person rig took it), and the placeholder capsule is off.
    /// </summary>
    public sealed class CrewBodyScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;

        public CrewBodyScenarioLibrary(ScenarioSubject subject, INetworkService network)
        {
            _subject = subject;
            _network = network;
        }

        public IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectBodyGait", CheckGait),
            new ScenarioProbe("SubjectBodyHiddenFromOwner", _ => CheckHiddenFromOwner()),
        };

        private CrewBodyView? Body() => (_subject.Resolve() as Component)?.GetComponentInChildren<CrewBodyView>();

        private ScenarioCheck CheckGait(string argument)
        {
            if (!Enum.TryParse<CrewBodyGait>(argument, out var expected)) return ScenarioCheck.Fail($"'{argument}' is not a gait");
            var body = Body();
            if (body == null) return ScenarioCheck.Fail("the subject has no crew body");
            string detail = $"gait {body.Gait}, velocity {body.LocalVelocity}, grounded {body.IsGrounded}";
            return body.Gait == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckHiddenFromOwner()
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer probe");
            var body = Body();
            if (body == null) return ScenarioCheck.Fail("the subject has no crew body");

            var parts = body.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0) return ScenarioCheck.Fail("the body has no renderers");
            var shown = parts.Where(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly).Select(r => r.name).ToList();
            if (shown.Count > 0) return ScenarioCheck.Fail($"drawn to its own camera: {string.Join(", ", shown)}");

            var anchor = (subject as Component)?.GetComponent<IHumanoidVisualAnchor>();
            var capsule = anchor?.VisualRoot?.GetComponents<Renderer>().FirstOrDefault(r => r.enabled);
            return capsule != null ? ScenarioCheck.Fail($"the capsule {capsule.name} is still on") : ScenarioCheck.Pass($"{parts.Length} body renderers shadow-only");
        }
    }
}
