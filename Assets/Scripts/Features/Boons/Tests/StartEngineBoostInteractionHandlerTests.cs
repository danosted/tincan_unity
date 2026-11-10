#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using TinCan.Features.SkillChecks;
using UnityEngine;

namespace TinCan.Features.Boons.Tests
{
    /// <summary><see cref="StartEngineBoostInteractionHandler"/>: E on an engine station opens its check for the player who pressed it. Plan: hazards-and-boons.md (B3).</summary>
    public class StartEngineBoostInteractionHandlerTests
    {
        private sealed class Player : IPossessable
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public ulong? PossessorId => OwnerId;
            public ulong? OwnerId { get; set; }
            public void AuthoritativeSetPossessor(ulong? playerId) { }
            public bool CanPossess(ulong playerId) => true;
        }

        private sealed class Host : ISkillCheckHost
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public IAirshipView? Ship => null;
            public SkillCheckDefinition? Definition => null;
            public void ServerOpen(ulong clientId, int checkId, int level) { }
            public bool TryTakeOpened(out int checkId, out int level)
            {
                checkId = 0;
                level = 0;
                return false;
            }
            public void SendGrade(int checkId, SkillCheckGrade grade) { }
            public IReadOnlyList<SkillCheckResult> TakeResults() => Array.Empty<SkillCheckResult>();
            public void ServerShowLit(string? challenge, int secondsLeft) { }
            public string? LitChallenge => null;
            public int LitSecondsLeft => 0;
        }

        private sealed class Station : IEngineStation
        {
            public ISkillCheckHost? Check { get; set; }
        }

        private sealed class RecordingChecks : ISkillChecks
        {
            public List<(ISkillCheckHost Host, ulong Client)> Opened { get; } = new();
            public bool TryOpen(ISkillCheckHost host, ulong clientId)
            {
                Opened.Add((host, clientId));
                return true;
            }
            public int Count(SkillCheckGrade grade) => 0;
        }

        private sealed class NullPublisher : IEventPublisher
        {
            public void Publish<TEvent>(TEvent evt) { }
        }

        private InteractionDefinition _definition = null!;
        private RecordingChecks _checks = null!;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            _checks = new RecordingChecks();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_definition);

        [Test]
        public void E_OnAStation_OpensItsCheck_ForThePlayerWhoPressed()
        {
            var host = new Host();
            Handle(new Player { OwnerId = 3 }, new Station { Check = host });

            Assert.That(_checks.Opened, Is.EqualTo(new[] { ((ISkillCheckHost)host, 3UL) }));
        }

        [Test]
        public void NoOwner_OrNoHost_OpensNothing()
        {
            Handle(new Player(), new Station { Check = new Host() });
            Handle(new Player { OwnerId = 3 }, new Station());

            Assert.That(_checks.Opened, Is.Empty);
        }

        private void Handle(IActor requester, IInteractable target) =>
            new StartEngineBoostInteractionHandler(_checks, new NullPublisher()).Handle(new InteractionContext(requester, target, _definition));
    }
}
