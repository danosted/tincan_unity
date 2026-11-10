#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>
    /// Application Layer, server only: the running challenge (<see cref="SkillChallengeRun"/>) on the ship with stations.
    /// Every tick it times out lit stations no one started and shows each station's lit state to every peer.
    /// <see cref="SkillCheckUseCase"/> asks it whether an E press takes a lit step (<see cref="TryClaim"/>), reports
    /// that step's grade back (<see cref="Report"/>) and opens an escalation's next rounds (<see cref="DueHandOvers"/>).
    /// Its start effect goes on the ship when it starts (its cue tells the crew); when it ends, its grade's effect. Plan: hazards-and-boons.md (B4a).
    /// </summary>
    public sealed class SkillChallengeUseCase : ISimulationTickable, ISkillChallenges
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "SkillChallenges";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly int[] _counts = new int[3];
        private SkillChallengeRun? _run;
        private IAirshipView? _ship;

        public SkillChallengeUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            AbilitySystemUseCase abilities)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
        }

        public SkillChallengeDefinition? Running => _run?.Definition;

        public int Count(SkillCheckGrade grade) => _counts[(int)grade];

        public bool TryStart(SkillChallengeDefinition challenge)
        {
            if (!_network.IsServer) return false;
            if (_run != null)
            {
                _events.LogInfo(LogSource, $"{challenge.name} waits: {_run.Definition.name} still runs.");
                return false;
            }

            var ship = ShipWithStations();
            if (ship == null)
            {
                _events.LogInfo(LogSource, $"{challenge.name}: no ship with stations.");
                return false;
            }

            var run = new SkillChallengeRun(challenge, station => HostFor(ship, station) != null, _time.Time);
            if (run.WasEmpty)
            {
                _events.LogInfo(LogSource, $"{challenge.name}: none of its stations is aboard.");
                return false;
            }

            _run = run;
            _ship = ship;
            _events.LogInfo(LogSource, $"{challenge.name} started: {challenge.Title}.");
            var controller = (ship as IShipState)?.Controller;
            if (controller != null && challenge.StartEffect != null) _abilities.ApplyEffect(controller, challenge.StartEffect);
            ShowLit();
            return true;
        }

        public void Cancel()
        {
            if (_run == null) return;
            _events.LogInfo(LogSource, $"{_run.Definition.name} dropped.");
            _run = null;
            ShowLit();
            _ship = null;
        }

        /// <summary>A player pressed E on this host: does it take a lit step? If so, the step's number and level.</summary>
        public ChallengeClaim TryClaim(ISkillCheckHost host, ulong clientId, out int stepId, out int level)
        {
            stepId = 0;
            level = 0;
            if (_run == null || host.Definition == null || !ReferenceEquals(host.Ship, _ship)) return ChallengeClaim.NotLit;

            var claim = _run.TryClaim(host.Definition, clientId, _actors.CrewCount(), _time.Time, out var step);
            if (claim == ChallengeClaim.Claimed)
            {
                stepId = step!.Id;
                level = step.Level;
                _events.LogInfo(LogSource, $"{_run.Definition.name}: client {clientId} takes {host.Definition.Station} (step {stepId}, level {level}).");
            }
            else if (claim == ChallengeClaim.Refused)
            {
                _events.LogInfo(LogSource, $"{_run.Definition.name}: someone else has to do {host.Definition.Station}.");
            }
            return claim;
        }

        /// <summary>A claimed step's check ended.</summary>
        public void Report(int stepId, SkillCheckGrade grade, ulong clientId)
        {
            if (_run == null) return;
            _run.Report(stepId, grade, clientId, _time.Time);
            if (_run.IsDone) End();
        }

        /// <summary>An escalation's next rounds, to open for their player now (at the host they were done on).</summary>
        public IReadOnlyList<(ISkillCheckHost Host, ulong ClientId)> DueHandOvers()
        {
            if (_run == null || _ship == null) return Array.Empty<(ISkillCheckHost, ulong)>();

            List<(ISkillCheckHost, ulong)>? due = null;
            foreach (var step in _run.DueHandOvers(_time.Time))
            {
                var host = HostFor(_ship, step.Station);
                if (host != null) (due ??= new List<(ISkillCheckHost, ulong)>()).Add((host, step.HandTo!.Value));
            }
            return due ?? (IReadOnlyList<(ISkillCheckHost, ulong)>)Array.Empty<(ISkillCheckHost, ulong)>();
        }

        public void Tick()
        {
            if (!_network.IsServer || _run == null) return;

            _run.Tick(_time.Time);
            if (_run.IsDone) End();
            else ShowLit();
        }

        private void End()
        {
            var run = _run!;
            _run = null;
            ShowLit();

            var grade = run.Grade;
            _counts[(int)grade]++;
            var controller = (_ship as IShipState)?.Controller;
            var effect = run.Definition.EffectFor(grade);
            if (controller != null && effect != null) _abilities.ApplyEffect(controller, effect);
            _ship = null;

            _events.Publish(new SkillChallengeEndedEvent(run.Definition.name, grade));
            _events.LogInfo(LogSource, $"{run.Definition.name}: {grade}.");
        }

        /// <summary>Each host shows whether the running challenge lights it; hosts it does not light go out.</summary>
        private void ShowLit()
        {
            foreach (var host in _actors.GetActors<ISkillCheckHost>())
            {
                ChallengeLitStep? lit = null;
                if (_run != null && host.Definition != null && ReferenceEquals(host.Ship, _ship))
                {
                    foreach (var step in _run.Lit)
                    {
                        if (step.Station != host.Definition) continue;
                        if (lit == null || (step.Started && !lit.Started)) lit = step;
                    }
                }

                if (lit == null) host.ServerShowLit(null, 0);
                else host.ServerShowLit(_run!.Definition.Title, lit.Started ? -1 : Mathf.Max(0, Mathf.CeilToInt(lit.Deadline - _time.Time)));
            }
        }

        private IAirshipView? ShipWithStations()
        {
            foreach (var host in _actors.GetActors<ISkillCheckHost>())
            {
                if (host.Ship != null && host.Definition != null) return host.Ship;
            }
            return null;
        }

        private ISkillCheckHost? HostFor(IAirshipView ship, SkillCheckDefinition station)
        {
            foreach (var host in _actors.GetActors<ISkillCheckHost>())
            {
                if (host.Definition == station && ReferenceEquals(host.Ship, ship)) return host;
            }
            return null;
        }
    }
}
