#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Input;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Tests.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.SkillChecks.Tests
{
    /// <summary>
    /// <see cref="PulsingGaugeProcessor"/>, <see cref="SkillCheckUseCase"/> and <see cref="SkillCheckRunner"/>: the needle
    /// and the grade; a check opened for one player, graded once, its effect (and the window's bonus) on the ship, then
    /// a rest; the peer's run from opening to the grade it sends. Plan: hazards-and-boons.md (B3).
    /// </summary>
    public class SkillCheckTests
    {
        private sealed class FakeHost : ISkillCheckHost
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating { get; set; } = true;
            public IAirshipView? Ship { get; set; }
            public SkillCheckDefinition? Definition { get; set; }
            public List<(ulong Client, int CheckId, float Band)> Opened { get; } = new();
            public List<(int CheckId, SkillCheckGrade Grade)> Sent { get; } = new();
            public List<SkillCheckResult> Incoming { get; } = new();
            public SkillCheckOpening? Pending;

            public void ServerOpen(ulong clientId, int checkId, float bandCenter) => Opened.Add((clientId, checkId, bandCenter));

            public bool TryTakeOpened(out SkillCheckOpening opening)
            {
                opening = Pending ?? default;
                bool had = Pending != null;
                Pending = null;
                return had;
            }

            public void SendGrade(int checkId, SkillCheckGrade grade) => Sent.Add((checkId, grade));

            public IReadOnlyList<SkillCheckResult> TakeResults()
            {
                var taken = Incoming.ToArray();
                Incoming.Clear();
                return taken;
            }
        }

        private sealed class FakeContextSwitch : IInputContextSwitch
        {
            private readonly HashSet<InputContext> _open = new();
            public void SetOpen(InputContext context, bool open)
            {
                if (open) _open.Add(context);
                else _open.Remove(context);
            }
            public bool IsOpen(InputContext context) => _open.Contains(context);
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private const ulong Player = 7;

        private readonly List<Object> _assets = new();
        private readonly PulsingGaugeProcessor _gauge = new();
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private RecordingPublisher _events = null!;
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _ship = null!;
        private MoveSpeedAttribute _speed = null!;
        private GameplayTag _window = null!;
        private SkillCheckDefinition _check = null!;
        private FakeHost _host = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService();
            _events = new RecordingPublisher();
            _speed = Create<MoveSpeedAttribute>("Attr_FlightSpeed");
            _window = Create<GameplayTag>("State.Ship.BoostWindow");

            _check = Create<SkillCheckDefinition>("SC_EngineBoost");
            _check.Seconds = 4f;
            _check.Cooldown = 20f;
            _check.GoodEffect = Boost("GE_Good", 1f);
            _check.PerfectEffect = Boost("GE_Perfect", 3f);
            _check.BonusTag = _window;
            _check.BonusEffect = Boost("GE_Extra", 10f);

            _shipController = new FakeAbilityController();
            _shipController.SetAttribute(_speed, new AttributeValue(10f));
            _ship = new FakeAirshipView("Airship", _shipController);
            _host = new FakeHost { Ship = _ship, Definition = _check };
            _actors.Register(_host);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private float ShipSpeed => _shipController.TryGetAttribute(_speed, out var value) ? value.CurrentValue : -1f;

        [Test]
        public void Needle_SweepsThereAndBack_OncePerPeriod()
        {
            Assert.That(_gauge.Needle(0f, 2f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_gauge.Needle(0.5f, 2f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_gauge.Needle(1f, 2f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(_gauge.Needle(1.5f, 2f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_gauge.Needle(2.5f, 2f), Is.EqualTo(0.5f).Within(1e-4f), "and again");
        }

        [Test]
        public void Grade_PerfectInTheMiddle_GoodInTheBand_MissOutside()
        {
            var band = new PulsingGaugeBand(0.5f, 0.2f, 0.06f);
            Assert.That(_gauge.Grade(0.51f, band), Is.EqualTo(SkillCheckGrade.Perfect));
            Assert.That(_gauge.Grade(0.58f, band), Is.EqualTo(SkillCheckGrade.Good));
            Assert.That(_gauge.Grade(0.62f, band), Is.EqualTo(SkillCheckGrade.Miss));
        }

        [Test]
        public void Open_SendsTheCheckToThatPlayer_WithTheBandInsideTheGauge()
        {
            Assert.That(UseCase().TryOpen(_host, Player), Is.True);

            var opened = _host.Opened.Single();
            Assert.That(opened.Client, Is.EqualTo(Player));
            Assert.That(opened.Band, Is.InRange(_check.GoodWidth * 0.5f, 1f - _check.GoodWidth * 0.5f));
        }

        [Test]
        public void AGrade_GivesItsEffect_Once_ThenTheHostRests()
        {
            var checks = UseCase();
            checks.TryOpen(_host, Player);
            int id = _host.Opened.Single().CheckId;

            _host.Incoming.Add(new SkillCheckResult(Player, id, SkillCheckGrade.Perfect));
            _host.Incoming.Add(new SkillCheckResult(Player, id, SkillCheckGrade.Perfect));
            checks.Tick();

            Assert.That(ShipSpeed, Is.EqualTo(13f).Within(1e-3f), "the Perfect effect, once");
            Assert.That(checks.Count(SkillCheckGrade.Perfect), Is.EqualTo(1));
            Assert.That(checks.TryOpen(_host, Player), Is.False, "resting");

            _time.Time += 21f;
            Assert.That(checks.TryOpen(_host, Player), Is.True, "rested");
        }

        [Test]
        public void InsideTheBoostWindow_AGoodAlsoGivesTheBonus()
        {
            _shipController.AddTag(_window);
            var checks = UseCase();
            checks.TryOpen(_host, Player);

            _host.Incoming.Add(new SkillCheckResult(Player, _host.Opened.Single().CheckId, SkillCheckGrade.Good));
            checks.Tick();

            Assert.That(ShipSpeed, Is.EqualTo(21f).Within(1e-3f), "Good (+1) and the bonus (+10)");
            Assert.That(_events.Events.OfType<SkillCheckGradedEvent>().Single().Bonus, Is.True);
        }

        [Test]
        public void AMiss_GivesNothing_NotEvenInsideTheWindow()
        {
            _shipController.AddTag(_window);
            var checks = UseCase();
            checks.TryOpen(_host, Player);

            _host.Incoming.Add(new SkillCheckResult(Player, _host.Opened.Single().CheckId, SkillCheckGrade.Miss));
            checks.Tick();

            Assert.That(ShipSpeed, Is.EqualTo(10f));
            Assert.That(checks.Count(SkillCheckGrade.Miss), Is.EqualTo(1));
        }

        [Test]
        public void AGradeFromAnotherPlayer_OrForAnotherCheck_IsIgnored()
        {
            var checks = UseCase();
            checks.TryOpen(_host, Player);
            int id = _host.Opened.Single().CheckId;

            _host.Incoming.Add(new SkillCheckResult(Player + 1, id, SkillCheckGrade.Perfect));
            _host.Incoming.Add(new SkillCheckResult(Player, id + 1, SkillCheckGrade.Perfect));
            checks.Tick();

            Assert.That(ShipSpeed, Is.EqualTo(10f));
            Assert.That(checks.TryOpen(_host, Player + 1), Is.False, "still open for the first player");
        }

        [Test]
        public void ACheckNoGradeCameBackFor_EndsAsAMiss_AfterItsTimeAndTheGrace()
        {
            var checks = UseCase();
            checks.TryOpen(_host, Player);

            _time.Time += _check.Seconds + SkillCheckUseCase.GraceSeconds - 0.1f;
            checks.Tick();
            Assert.That(checks.Count(SkillCheckGrade.Miss), Is.Zero);

            _time.Time += 0.2f;
            checks.Tick();
            Assert.That(checks.Count(SkillCheckGrade.Miss), Is.EqualTo(1));

            _host.Incoming.Add(new SkillCheckResult(Player, _host.Opened.Single().CheckId, SkillCheckGrade.Perfect));
            checks.Tick();
            Assert.That(ShipSpeed, Is.EqualTo(10f), "too late");
        }

        [Test]
        public void Runner_PlaysTheCheck_InItsOwnContext_AndSendsTheGradeOfThePress()
        {
            var (runner, input, contexts, controls) = Runner();
            _check.Period = 2f;
            _host.Pending = new SkillCheckOpening(5, 0.5f);

            runner.Tick();
            Assert.That(runner.IsRunning, Is.True);
            Assert.That(contexts.IsOpen(controls), Is.True, "walking and looking go quiet");

            _time.DeltaTime = 0.5f; // the needle reaches the middle
            input.Triggered.Add(controls.Press!);
            runner.Tick();

            Assert.That(_host.Sent, Is.EqualTo(new[] { (5, SkillCheckGrade.Perfect) }));
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.Shown, Is.EqualTo(SkillCheckGrade.Perfect));
            Assert.That(contexts.IsOpen(controls), Is.False);
        }

        [Test]
        public void Runner_WithoutAPress_SendsAMiss_WhenTimeRunsOut()
        {
            var (runner, _, contexts, controls) = Runner();
            _host.Pending = new SkillCheckOpening(9, 0.5f);
            runner.Tick();

            _time.DeltaTime = 1f;
            for (int i = 0; i < 4; i++) runner.Tick();

            Assert.That(_host.Sent, Is.EqualTo(new[] { (9, SkillCheckGrade.Miss) }));
            Assert.That(contexts.IsOpen(controls), Is.False);

            for (int i = 0; i < 2; i++) runner.Tick();
            Assert.That(runner.Shown, Is.Null, "the grade shows only for a moment");
        }

        private (SkillCheckRunner Runner, FakeInputReader Input, FakeContextSwitch Contexts, SkillCheckInputContext Controls) Runner()
        {
            var controls = Create<SkillCheckInputContext>("Context_SkillCheck");
            controls.Press = Create<InputActionId>("SkillCheck.Press");
            var input = new FakeInputReader();
            var contexts = new FakeContextSwitch();
            return (new SkillCheckRunner(_actors, input, contexts, controls, _time, _gauge), input, contexts, controls);
        }

        private SkillCheckUseCase UseCase()
        {
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);
            return new SkillCheckUseCase(new FakeNetworkService(), _actors, _time, _events, abilities);
        }

        /// <summary>An instant speed boost (so the attribute shows it at once).</summary>
        private GameplayEffectDefinition Boost(string name, float add)
        {
            var effect = Create<GameplayEffectDefinition>(name);
            effect.DurationType = DurationType.Instant;
            effect.GrantedTags = new List<GameplayTag>();
            effect.Modifiers = new List<AttributeModifier> { new() { Attribute = _speed, Operation = ModifierOp.Add, Value = add } };
            return effect;
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }
}
