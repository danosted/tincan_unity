#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Application Layer, server only. Opens a host's check on one player's peer (picking where the band sits), then
    /// takes the grade that peer sends back: only for a check it opened for that player, before the check's time and a
    /// grace for the round trip run out (then it is a Miss). The grade's effect goes on the host's ship, with the bonus
    /// effect if the ship carries the bonus tag. Whatever the grade, the host then rests for the check's cooldown. Co-op
    /// trusts the grade itself. Plan: .docs/plans/hazards-and-boons.md (B3).
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
        }

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly List<Open> _open = new();
        private readonly Dictionary<Guid, float> _restingUntil = new();
        private readonly int[] _counts = new int[3];
        private readonly System.Random _random = new();
        private int _nextCheckId;

        public SkillCheckUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            AbilitySystemUseCase abilities)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
        }

        public int Count(SkillCheckGrade grade) => _counts[(int)grade];

        public bool TryOpen(ISkillCheckHost host, ulong clientId)
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

            if (_restingUntil.TryGetValue(host.Id, out float until) && _time.Time < until)
            {
                _events.LogInfo(LogSource, $"{definition.name} rests for {Mathf.CeilToInt(until - _time.Time)} s more.");
                return false;
            }

            var open = new Open
            {
                Host = host, Definition = definition, ClientId = clientId, CheckId = ++_nextCheckId,
                Deadline = _time.Time + definition.Seconds + GraceSeconds
            };
            _open.Add(open);
            float half = definition.GoodWidth * 0.5f;
            float center = Mathf.Lerp(Mathf.Max(half, 0.25f), Mathf.Min(1f - half, 0.75f), (float)_random.NextDouble());
            host.ServerOpen(clientId, open.CheckId, center);
            _events.LogInfo(LogSource, $"{definition.name} opened for client {clientId} (check {open.CheckId}).");
            return true;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var host in _actors.GetActors<ISkillCheckHost>())
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
            _restingUntil[open.Host.Id] = _time.Time + open.Definition.Cooldown;
            _counts[(int)grade]++;

            bool bonus = false;
            var controller = (open.Host.Ship as IShipState)?.Controller;
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
