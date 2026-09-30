#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using TinCan.Features.Boarding;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for boarding. <c>SubjectAboard "radius"</c> passes when the subject stands within that many metres
    /// of the ship's boarding pose, measured level in ship space (host and client see the ship at different world poses),
    /// no higher than it and not fallen far below it. Answers on any peer.
    /// </summary>
    public sealed class BoardingScenarioLibrary : IScenarioLibrary
    {
        /// <summary>How far below the boarding pose still counts as on board: it hovers above the deck and players fall onto it.</summary>
        private const float MaxDropBelow = 10f;

        /// <summary>
        /// How far above it still counts. The spawn point can sit straight above the ship's origin (the test range), so
        /// the level distance alone cannot tell a boarded player from one not yet moved; the height can.
        /// </summary>
        private const float MaxRiseAbove = 1.5f;

        private readonly ScenarioSubject _subject;
        private readonly IActorRegistry _actors;
        private readonly BoardingConfig _config;

        public BoardingScenarioLibrary(ScenarioSubject subject, IActorRegistry actors, BoardingConfig config)
        {
            _subject = subject;
            _actors = actors;
            _config = config;
        }

        public IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectAboard", CheckAboard)
        };

        private ScenarioCheck CheckAboard(string radiusText)
        {
            if (!float.TryParse(radiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out float radius)) radius = 4f;

            var body = _subject.Body;
            if (body == null) return ScenarioCheck.Fail("no subject player yet");
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
            if (ship == null) return ScenarioCheck.Fail("no ship on this peer");

            var (boardingPosition, _) = AirshipBoardingPose.Resolve(ship, _config.BoardingOffset);
            Vector3 target = ship.Transform.InverseTransformPoint(boardingPosition);
            Vector3 local = ship.Transform.InverseTransformPoint(body.position);
            float level = Vector2.Distance(new Vector2(local.x, local.z), new Vector2(target.x, target.z));
            float below = target.y - local.y;

            string detail = $"{level:0.0} m from the boarding point level, {below:0.0} m below it, in ship space on this peer";
            return level <= radius && below <= MaxDropBelow && -below <= MaxRiseAbove
                ? ScenarioCheck.Pass(detail)
                : ScenarioCheck.Fail(detail);
        }
    }
}
