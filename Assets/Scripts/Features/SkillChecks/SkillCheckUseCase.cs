#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Features.SkillChecks.Challenges;
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Application Layer, server only. Opens a host's check on one player's peer, then
    /// takes the grade that peer sends back: only for a check it opened for that player, before the check's time and a
    /// grace for the round trip run out (then it is a Miss). The grade's effect goes on the host's ship (or on the target it was opened for: a broken
    /// part's repair, plan R1), with the bonus
    /// effect if the ship carries the bonus tag. Whatever the grade, the host then rests for the check's cooldown. Co-op
    /// trusts the grade itself. A press on a station a challenge lights does the challenge's step instead
    /// (<see cref="SkillChallengeUseCase"/>): the grade goes to the challenge, with no effect and no rest, and an
    /// escalation's next round opens by itself. Plan: .docs/plans/hazards-and-boons.md (B3, B4a).
    /// </summary>
    public class SkillCheckUseCase : ISimulationTickable, ISkillChecks
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "SkillChecks";

        /// <summary>Seconds past the check's own time a grade may still arrive (the round trip, a slow frame).</summary>
        public const float GraceSeconds = 2f;

        private sealed class Open
        {
            public ISkillCheckHost Host = null!;
            public SkillCheckDefinition Definition = null!;
            public ulong ClientId;
            public int CheckId;
            public float Deadline;
            /// <summary>Where its grade's effect goes instead of the ship (a broken part), or null.</summary>
            public IAbilityControllerBase? Target;
            /// <summary>The challenge step it does, or 0.</summary>
            public int StepId;
        }

        private readonly INetworkService _network;
        private readonly SkillCheckHostRegistry _hosts;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly SkillChallengeUseCase _challenges;
        private readonly List<Open> _open = new();
        private readonly Dictionary<Guid, float> _restingUntil = new();
        private readonly int[] _counts = new int[3];
        private int _nextCheckId;

        public SkillCheckUseCase(INetworkService network, SkillCheckHostRegistry hosts, ITimeService time, IEventPublisher events,
            AbilitySystemUseCase abilities, SkillChallengeUseCase challenges)
        {
            _network = network;
            _hosts = hosts;
            _time = time;
            _events = events;
            _abilities = abilities;
            _challenges = challenges;
        }

        public int Count(SkillCheckGrade grade) => _counts[(int)grade];

        public bool TryOpen(ISkillCheckHost host, ulong clientId, IAbilityControllerBase? target = null)
        {
            if (!_network.IsServer) return false;
            var definition = host.Definition;
            if (definition == null)
            {
                _events.LogWarning(LogSource, "This part has no skill check assigned.");
                return false;
            }

            if (_open.Exists(open => open.Host.Id == host.Id))
            {
                _events.LogInfo(LogSource, $"{definition.name} is already being done.");
                return false;
            }

            var claim = _challenges.TryClaim(host, clientId, out int stepId, out int level);
            if (claim == ChallengeClaim.Refused) return false;

            if (claim == ChallengeClaim.NotLit && _restingUntil.TryGetValue(host.Id, out float until) && _time.Time < until)
            {
                _events.LogInfo(LogSource, $"{definition.name} rests for {Mathf.CeilToInt(until - _time.Time)} s more.");
                return false;
            }

            var open = new Open
            {
                Host = host, Definition = definition, ClientId = clientId, CheckId = ++_nextCheckId,
                Deadline = _time.Time + definition.Seconds + GraceSeconds, StepId = stepId, Target = target
            };
            _open.Add(open);
            host.ServerOpen(clientId, open.CheckId, level);
            _events.LogInfo(LogSource, $"{definition.name} opened for client {clientId} (check {open.CheckId}).");
            return true;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var (host, clientId) in _challenges.DueHandOvers()) TryOpen(host, clientId);

            foreach (var host in _hosts.All)
            {
                foreach (var result in host.TakeResults())
                {
                    var open = _open.Find(o => o.Host.Id == host.Id && o.CheckId == result.CheckId && o.ClientId == result.ClientId);
                    if (open == null)
                    {
                        _events.LogInfo(LogSource, $"Ignored a grade from client {result.ClientId} for check {result.CheckId}: not open for them.");
                        continue;
                    }
                    End(open, result.Grade);
                }
            }

            for (int i = _open.Count - 1; i >= 0; i--)
            {
                if (_time.Time > _open[i].Deadline) End(_open[i], SkillCheckGrade.Miss);
            }
        }

        private void End(Open open, SkillCheckGrade grade)
        {
            _open.Remove(open);
            _counts[(int)grade]++;
            if (open.StepId != 0)
            {
                _events.Publish(new SkillCheckGradedEvent(open.Definition.name, grade, false));
                _events.LogInfo(LogSource, $"{open.Definition.name}: {grade}, for the challenge.");
                _challenges.Report(open.StepId, grade, open.ClientId);
                return;
            }

            _restingUntil[open.Host.Id] = _time.Time + open.Definition.Cooldown;

            bool bonus = false;
            var controller = open.Target ?? (open.Host.Ship as IShipState)?.Controller;
            var effect = open.Definition.EffectFor(grade);
            if (controller != null && effect != null)
            {
                var bonusTag = open.Definition.BonusTag;
                bonus = bonusTag != null && open.Definition.BonusEffect != null && controller.HasTag(bonusTag);
                _abilities.ApplyEffect(controller, effect);
                if (bonus) _abilities.ApplyEffect(controller, open.Definition.BonusEffect!);
            }

            _events.Publish(new SkillCheckGradedEvent(open.Definition.name, grade, bonus));
            _events.LogInfo(LogSource, $"{open.Definition.name}: {grade}{(bonus ? " + the boost window's bonus" : "")}.");
        }
    }
}
