#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Core.UI;
using TinCan.Features.SkillChecks.Challenges;
using TinCan.Tests.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.SkillChecks.Tests
{
    /// <summary>
    /// Challenges (hazards-and-boons.md, B4a): <see cref="SkillChallengeRun"/>'s five ways of putting steps together and
    /// its grade rules; <see cref="SkillChallengeUseCase"/> with <see cref="SkillCheckUseCase"/> (an E press on a lit
    /// station does the step, the challenge's effect at the end); the encounter's interval; the HUD line; the harder
    /// levels of a check.
    /// </summary>
    public class SkillChallengeTests
    {
        private const ulong Alice = 1;
        private const ulong Bob = 2;

        private readonly List<Object> _assets = new();
        private SkillCheckDefinition _telegraph = null!;
        private SkillCheckDefinition _boiler = null!;
        private SkillCheckDefinition _valve = null!;

        [SetUp]
        public void SetUp()
        {
            _telegraph = Station("SC_EngineBoost", "Engine telegraph");
            _boiler = Station("SC_BoilerStoke", "Boiler");
            _valve = Station("SC_Valve", "Valve");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // --- The step machine -------------------------------------------------------------------------------------

        [Test]
        public void Sequence_LightsOneStationAtATime_AndTheWorstGradeWins()
        {
            var run = Run(Challenge(new SequenceStep { Steps = { At(_telegraph), At(_boiler) } }));

            Assert.That(LitStations(run), Is.EqualTo(new[] { _telegraph }));
            Do(run, _telegraph, Alice, SkillCheckGrade.Perfect);
            Assert.That(LitStations(run), Is.EqualTo(new[] { _boiler }));
            Do(run, _boiler, Alice, SkillCheckGrade.Good);

            Assert.That(run.IsDone, Is.True);
            Assert.That(run.Grade, Is.EqualTo(SkillCheckGrade.Good));
        }

        [Test]
        public void Relay_TheNextStepWantsAnotherPlayer_UnlessThePlayerIsAlone()
        {
            var run = Run(Challenge(new RelayStep { Steps = { At(_telegraph), At(_boiler) } }));
            Do(run, _telegraph, Alice, SkillCheckGrade.Perfect);

            Assert.That(run.TryClaim(_boiler, Alice, crew: 2, 0f, out _), Is.EqualTo(ChallengeClaim.Refused));
            Assert.That(run.TryClaim(_boiler, Alice, crew: 1, 0f, out _), Is.EqualTo(ChallengeClaim.Claimed), "alone, the same player");
        }

        [Test]
        public void Relay_AnotherPlayerTakesTheNextStep()
        {
            var run = Run(Challenge(new RelayStep { Steps = { At(_telegraph), At(_boiler) } }));
            Do(run, _telegraph, Alice, SkillCheckGrade.Perfect);
            Do(run, _boiler, Bob, SkillCheckGrade.Perfect, crew: 2);

            Assert.That(run.IsDone, Is.True);
            Assert.That(run.Grade, Is.EqualTo(SkillCheckGrade.Perfect));
        }

        [Test]
        public void Parallel_LightsEveryStationAtOnce_AndAveragesWhenAsked()
        {
            var challenge = Challenge(new ParallelStep { Steps = { At(_telegraph), At(_boiler) } });
            challenge.GradeRule = ChallengeGradeRule.Average;
            var run = Run(challenge);

            Assert.That(LitStations(run), Is.EquivalentTo(new[] { _telegraph, _boiler }));
            Do(run, _boiler, Bob, SkillCheckGrade.Miss);
            Assert.That(run.IsDone, Is.False, "waits for every step");
            Do(run, _telegraph, Alice, SkillCheckGrade.Perfect);

            Assert.That(run.Grade, Is.EqualTo(SkillCheckGrade.Good), "Perfect and Miss average to a Good");
        }

        [Test]
        public void Branch_PicksTheNextStepByTheFirstsGrade()
        {
            var branch = new BranchStep { First = At(_telegraph), OnPerfect = At(_boiler), OnMiss = At(_valve) };

            var perfect = Run(Challenge(branch));
            Do(perfect, _telegraph, Alice, SkillCheckGrade.Perfect);
            Assert.That(LitStations(perfect), Is.EqualTo(new[] { _boiler }));

            var miss = Run(Challenge(branch));
            Do(miss, _telegraph, Alice, SkillCheckGrade.Miss);
            Assert.That(LitStations(miss), Is.EqualTo(new[] { _valve }));

            var good = Run(Challenge(branch));
            Do(good, _telegraph, Alice, SkillCheckGrade.Good);
            Assert.That(good.IsDone, Is.True, "no step for a Good: the branch ends with the first grade");
            Assert.That(good.Grade, Is.EqualTo(SkillCheckGrade.Good));
        }

        [Test]
        public void Escalate_RepeatsHarder_ForTheSamePlayer_UntilPerfectAt()
        {
            var run = Run(Challenge(new EscalateStep { Station = _boiler, PerfectAt = 3 }));

            for (int round = 0; round < 3; round++)
            {
                var step = run.Lit.Single();
                Assert.That(step.Level, Is.EqualTo(round));
                if (round > 0)
                {
                    Assert.That(step.HandTo, Is.EqualTo(Alice), "opens for the player who did the last round");
                    Assert.That(run.TryClaim(_boiler, Bob, crew: 2, 0f, out _), Is.EqualTo(ChallengeClaim.Refused));
                    Assert.That(run.DueHandOvers(step.HandAt).Single(), Is.SameAs(step));
                }
                Do(run, _boiler, Alice, SkillCheckGrade.Good);
            }

            Assert.That(run.IsDone, Is.True);
            Assert.That(run.Grade, Is.EqualTo(SkillCheckGrade.Perfect));
        }

        [Test]
        public void Escalate_AMissEndsIt_GoodIfARoundWasPassed_MissIfNone()
        {
            var some = Run(Challenge(new EscalateStep { Station = _boiler, PerfectAt = 3 }));
            Do(some, _boiler, Alice, SkillCheckGrade.Perfect);
            Do(some, _boiler, Alice, SkillCheckGrade.Miss);
            Assert.That(some.Grade, Is.EqualTo(SkillCheckGrade.Good));

            var none = Run(Challenge(new EscalateStep { Station = _boiler, PerfectAt = 3 }));
            Do(none, _boiler, Alice, SkillCheckGrade.Miss);
            Assert.That(none.Grade, Is.EqualTo(SkillCheckGrade.Miss));
        }

        [Test]
        public void ALitStationNoOneStarts_IsAMiss_AfterTheStepTime()
        {
            var challenge = Challenge(At(_telegraph));
            var run = Run(challenge);

            run.Tick(challenge.StepSeconds - 0.1f);
            Assert.That(run.IsDone, Is.False);
            run.Tick(challenge.StepSeconds + 0.1f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(run.Grade, Is.EqualTo(SkillCheckGrade.Miss));
        }

        [Test]
        public void AStartedStep_DoesNotTimeOut_ItsCheckHasItsOwnTime()
        {
            var challenge = Challenge(At(_telegraph));
            var run = Run(challenge);
            run.TryClaim(_telegraph, Alice, 1, 1f, out _);

            run.Tick(challenge.StepSeconds + 5f);

            Assert.That(run.IsDone, Is.False);
        }

        [Test]
        public void AStationTheShipLacks_IsSkipped_AndAChallengeOfOnlyThoseIsEmpty()
        {
            var run = Run(Challenge(new SequenceStep { Steps = { At(_valve), At(_boiler) } }), _telegraph, _boiler);
            Assert.That(LitStations(run), Is.EqualTo(new[] { _boiler }), "the valve is skipped");

            var empty = Run(Challenge(new ParallelStep { Steps = { At(_valve) } }), _telegraph);
            Assert.That(empty.IsDone && empty.WasEmpty, Is.True);
        }

        // --- On the server, with the checks ------------------------------------------------------------------------

        [Test]
        public void APressOnALitStation_DoesTheStep_WithoutTheStationsBoostOrRest_ThenTheChallengeGivesItsEffect()
        {
            using var server = new Server(this);
            var challenge = Challenge(new SequenceStep { Steps = { At(_telegraph), At(_boiler) } });
            challenge.PerfectEffect = server.Boost("GE_ChallengePerfect", 100f);
            Assert.That(server.Challenges.TryStart(challenge), Is.True);
            Assert.That(server.Telegraph.LitChallenge, Is.EqualTo(challenge.Title), "lit on every peer");
            Assert.That(server.Boiler.LitChallenge, Is.Null);

            Assert.That(server.Checks.TryOpen(server.Telegraph, Alice), Is.True);
            server.Telegraph.Answer(SkillCheckGrade.Perfect);
            server.Tick();
            Assert.That(server.ShipSpeed, Is.EqualTo(10f), "no station boost inside a challenge");
            Assert.That(server.Boiler.LitChallenge, Is.EqualTo(challenge.Title));
            Assert.That(server.Telegraph.LitChallenge, Is.Null);

            Assert.That(server.Checks.TryOpen(server.Boiler, Alice), Is.True);
            server.Boiler.Answer(SkillCheckGrade.Perfect);
            server.Tick();

            Assert.That(server.ShipSpeed, Is.EqualTo(110f), "the challenge's Perfect effect");
            Assert.That(server.Challenges.Count(SkillCheckGrade.Perfect), Is.EqualTo(1));
            Assert.That(server.Challenges.Running, Is.Null);
            Assert.That(server.Checks.TryOpen(server.Telegraph, Alice), Is.True, "the telegraph did not rest");
        }

        [Test]
        public void ARelayStepRefusesThePlayerWhoDidTheLastOne_WhenTheCrewHasOthers()
        {
            using var server = new Server(this, crew: 2);
            server.Challenges.TryStart(Challenge(new RelayStep { Steps = { At(_telegraph), At(_boiler) } }));
            server.Checks.TryOpen(server.Telegraph, Alice);
            server.Telegraph.Answer(SkillCheckGrade.Good);
            server.Tick();

            Assert.That(server.Checks.TryOpen(server.Boiler, Alice), Is.False);
            Assert.That(server.Checks.TryOpen(server.Boiler, Bob), Is.True);
        }

        [Test]
        public void AnEscalationsNextRound_OpensByItself_HarderForTheSamePlayer()
        {
            using var server = new Server(this);
            server.Challenges.TryStart(Challenge(new EscalateStep { Station = _boiler, PerfectAt = 2 }));
            server.Checks.TryOpen(server.Boiler, Alice);
            server.Boiler.Answer(SkillCheckGrade.Good);
            server.Tick();
            Assert.That(server.Boiler.Opened, Has.Count.EqualTo(1), "the last grade shows first");

            server.Time.Time += SkillChallengeRun.HandOverDelay;
            server.Tick();

            Assert.That(server.Boiler.Opened.Last(), Is.EqualTo((Alice, server.Boiler.Opened.Last().CheckId, 1)));
        }

        [Test]
        public void Starting_PutsTheStartEffectOnTheShip_Once()
        {
            using var server = new Server(this);
            var challenge = Challenge(At(_telegraph));
            challenge.StartEffect = server.Boost("GE_ChallengeStart", 1f);

            server.Challenges.TryStart(challenge);
            server.Challenges.TryStart(challenge); // still running: refused
            server.Tick();

            Assert.That(server.ShipSpeed, Is.EqualTo(11f));
        }

        [Test]
        public void AChallengeWithoutAStationAboard_DoesNotStart()
        {
            using var server = new Server(this);
            Assert.That(server.Challenges.TryStart(Challenge(At(_valve))), Is.False);
            Assert.That(server.Challenges.Running, Is.Null);
        }

        // --- Encounters, the HUD, harder levels --------------------------------------------------------------------

        [Test]
        public void Encounters_StartAChallengeAfterTheFirstDelay_ThenEveryInterval_OnlyWhileUnderway()
        {
            using var server = new Server(this);
            var config = Create<SkillChallengesConfig>("SkillChallengesConfig");
            config.FirstDelay = 10f;
            config.Interval = 30f;
            config.Challenges.Add(Challenge(At(_telegraph)));
            var encounters = new SkillChallengeEncounterUseCase(new FakeNetworkService(), server.Time, server.Challenges, config);

            encounters.Tick();
            server.Time.Time = 50f;
            encounters.Tick();
            Assert.That(server.Challenges.Running, Is.Null, "not underway");

            encounters.SetSessionActive(true);
            server.Time.Time = 59f;
            encounters.Tick();
            Assert.That(server.Challenges.Running, Is.Null);
            server.Time.Time = 60.5f;
            encounters.Tick();
            Assert.That(server.Challenges.Running, Is.Not.Null);

            encounters.ResetForSession();
            Assert.That(server.Challenges.Running, Is.Null, "a new session drops it");
        }

        [Test]
        public void TheHud_NamesTheChallenge_AndItsLitStations()
        {
            using var server = new Server(this);
            var hud = new HudUseCase();
            var presenter = new SkillChallengeHudPresenter(server.Actors, hud);
            var challenge = Challenge(new ParallelStep { Steps = { At(_telegraph), At(_boiler) } });
            challenge.Title = "Head wind";
            challenge.StepSeconds = 20f;
            server.Challenges.TryStart(challenge);
            server.Checks.TryOpen(server.Boiler, Alice);
            server.Tick();

            presenter.Tick();
            Assert.That(hud.All[SkillChallengeHudPresenter.HudKey], Is.EqualTo("Head wind: Engine telegraph 20 s, Boiler (on it)"));

            server.Challenges.Cancel();
            presenter.Tick();
            Assert.That(hud.All.ContainsKey(SkillChallengeHudPresenter.HudKey), Is.False);
        }

        [Test]
        public void HarderLevels_NarrowTheBand_SpeedTheNeedle_LengthenTheCombo()
        {
            Assert.That(SkillCheckDifficulty.Width(0.2f, 0), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(SkillCheckDifficulty.Width(0.2f, 2), Is.EqualTo(0.1125f).Within(1e-5f));
            Assert.That(SkillCheckDifficulty.Period(1.6f, 1), Is.EqualTo(1.36f).Within(1e-5f));
            Assert.That(SkillCheckDifficulty.ComboLength(4, 2), Is.EqualTo(6));
            Assert.That(SkillCheckDifficulty.ComboLength(5, 9), Is.EqualTo(SkillCheckDifficulty.MaxComboLength));
        }

        // --- Helpers -----------------------------------------------------------------------------------------------

        private SkillChallengeRun Run(SkillChallengeDefinition challenge, params SkillCheckDefinition[] aboard)
        {
            var stations = aboard.Length > 0 ? aboard : new[] { _telegraph, _boiler, _valve };
            return new SkillChallengeRun(challenge, station => stations.Contains(station), 0f);
        }

        private static void Do(SkillChallengeRun run, SkillCheckDefinition station, ulong client, SkillCheckGrade grade, int crew = 1)
        {
            Assert.That(run.TryClaim(station, client, crew, 0f, out var step), Is.EqualTo(ChallengeClaim.Claimed), $"{station.name} lit for {client}");
            run.Report(step!.Id, grade, client, 0f);
        }

        private static SkillCheckDefinition[] LitStations(SkillChallengeRun run) => run.Lit.Select(step => step.Station).ToArray();

        private static StationStep At(SkillCheckDefinition station) => new() { Station = station };

        private SkillChallengeDefinition Challenge(ChallengeStep root)
        {
            var challenge = Create<SkillChallengeDefinition>("CH_Test");
            challenge.Root = root;
            return challenge;
        }

        private SkillCheckDefinition Station(string name, string station)
        {
            var check = Create<SkillCheckDefinition>(name);
            check.Station = station;
            check.Cooldown = 20f;
            return check;
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }

        /// <summary>The server side: a ship with the telegraph and the boiler, the check and challenge use cases, and a crew.</summary>
        private sealed class Server : System.IDisposable
        {
            private readonly SkillChallengeTests _test;
            private readonly FakeAbilityController _controller = new();
            private readonly FakeAirshipView _ship;
            private readonly MoveSpeedAttribute _speed;

            public Server(SkillChallengeTests test, int crew = 1)
            {
                _test = test;
                _speed = test.Create<MoveSpeedAttribute>("Attr_FlightSpeed");
                _controller.SetAttribute(_speed, new AttributeValue(10f));
                _ship = new FakeAirshipView("Airship", _controller);
                Telegraph = new FakeSkillCheckHost { Ship = _ship, Definition = test._telegraph };
                Boiler = new FakeSkillCheckHost { Ship = _ship, Definition = test._boiler };
                Actors.Register(Telegraph);
                Actors.Register(Boiler);
                for (int i = 0; i < crew; i++) Actors.Register(new FakeCrewMember());

                var events = new FakeEventPublisher();
                var network = new FakeNetworkService();
                var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), Actors, Time, events);
                Challenges = new SkillChallengeUseCase(network, Actors, Time, events, abilities);
                Checks = new SkillCheckUseCase(network, Actors, Time, events, abilities, Challenges);
            }

            public FakeActorRegistry Actors { get; } = new();
            public FakeTimeService Time { get; } = new();
            public FakeSkillCheckHost Telegraph { get; }
            public FakeSkillCheckHost Boiler { get; }
            public SkillChallengeUseCase Challenges { get; }
            public SkillCheckUseCase Checks { get; }

            public float ShipSpeed => _controller.TryGetAttribute(_speed, out var value) ? value.CurrentValue : -1f;

            public void Tick()
            {
                Checks.Tick();
                Challenges.Tick();
            }

            public GameplayEffectDefinition Boost(string name, float add)
            {
                var effect = _test.Create<GameplayEffectDefinition>(name);
                effect.DurationType = DurationType.Instant;
                effect.GrantedTags = new List<GameplayTag>();
                effect.Modifiers = new List<AttributeModifier> { new() { Attribute = _speed, Operation = ModifierOp.Add, Value = add } };
                return effect;
            }

            public void Dispose() => _ship.Destroy();
        }
    }
}
