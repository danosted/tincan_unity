#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.DevTools;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class HarnessOptionsTests
    {
        [Test]
        public void Parse_NoFlags_IsInert()
        {
            var options = HarnessOptions.Parse(new[] { "game.exe", "-autohost" });

            Assert.That(options.IsActive, Is.False);
        }

        [Test]
        public void Parse_BotImpliesTelemetry()
        {
            var options = HarnessOptions.Parse(new[] { "-bot", "DeckWalk", "-netsim", "Lag100" });

            Assert.That(options.BotRoute, Is.EqualTo("DeckWalk"));
            Assert.That(options.NetworkPreset, Is.EqualTo("Lag100"));
            Assert.That(options.TelemetryEnabled, Is.True);
        }

        [Test]
        public void Parse_TelemetryAlone_IsActive()
        {
            var options = HarnessOptions.Parse(new[] { "-telemetry" });

            Assert.That(options.IsActive, Is.True);
            Assert.That(options.BotRoute, Is.Null);
        }
    }

    public class NetworkConditionPresetsTests
    {
        [Test]
        public void TryGet_KnownPreset_IsCaseInsensitive()
        {
            Assert.That(NetworkConditionPresets.TryGet("lag100", out var preset), Is.True);
            Assert.That(preset.SendDelayMs, Is.EqualTo(50));
            Assert.That(preset.RoundTripMs, Is.EqualTo(100));
        }

        [Test]
        public void TryGet_UnknownPreset_FallsBackToNone()
        {
            Assert.That(NetworkConditionPresets.TryGet("Satellite", out var preset), Is.False);
            Assert.That(preset.Name, Is.EqualTo("None"));
        }
    }

    public class BotRouteCursorTests
    {
        private static BotRoute ThreeSteps() => new BotRoute.Builder("Test")
            .Wait(1f)
            .Tap(ActionNames.Jump, 0.5f)
            .Hold(2f, ActionNames.MoveForward)
            .Build();

        [Test]
        public void Build_SumsDurations()
        {
            Assert.That(ThreeSteps().TotalDuration, Is.EqualTo(3.5f).Within(1e-5f));
        }

        [Test]
        public void Advance_EntersEachStepOnce()
        {
            var cursor = new BotRouteCursor(ThreeSteps());
            var entered = new List<BotStep>();

            Assert.That(cursor.Advance(0f, entered), Has.Count.EqualTo(1));
            Assert.That(cursor.Advance(0.5f, entered), Is.Empty);
            Assert.That(cursor.Advance(1.2f, entered)[0].Taps, Does.Contain(ActionNames.Jump));
            Assert.That(cursor.Advance(1.3f, entered), Is.Empty);
            Assert.That(cursor.IsComplete, Is.False);
        }

        [Test]
        public void Advance_LongFrame_ReportsSkippedStepsInOrder()
        {
            var cursor = new BotRouteCursor(ThreeSteps());
            var entered = new List<BotStep>();

            cursor.Advance(2f, entered);

            Assert.That(entered, Has.Count.EqualTo(3));
            Assert.That(entered[1].Taps, Does.Contain(ActionNames.Jump));
            Assert.That(entered[2].Held, Does.Contain(ActionNames.MoveForward));
        }

        [Test]
        public void Advance_PastEnd_Completes()
        {
            var cursor = new BotRouteCursor(ThreeSteps());
            var entered = new List<BotStep>();

            cursor.Advance(10f, entered);

            Assert.That(cursor.IsComplete, Is.True);
            Assert.That(entered, Has.Count.EqualTo(3));
            Assert.That(cursor.Advance(11f, entered), Is.Empty);
        }

        [Test]
        public void Routes_AreKnownByName()
        {
            Assert.That(BotRoutes.TryGet("deckwalk", out var route), Is.True);
            Assert.That(route.TotalDuration, Is.GreaterThan(30f));
            Assert.That(BotRoutes.TryGet("nope", out var fallback), Is.False);
            Assert.That(fallback.Name, Is.EqualTo("Idle"));
        }
    }

    public class LatencyStatsTests
    {
        [Test]
        public void Summarise_ComputesNearestRankPercentiles()
        {
            var stats = new LatencyStats();
            for (int i = 1; i <= 20; i++) stats.Add(i * 10f);
            stats.AddTimeout();

            var summary = stats.Summarise();

            Assert.That(summary.n, Is.EqualTo(20));
            Assert.That(summary.timeouts, Is.EqualTo(1));
            Assert.That(summary.p50Ms, Is.EqualTo(100f));
            Assert.That(summary.p95Ms, Is.EqualTo(190f));
            Assert.That(summary.maxMs, Is.EqualTo(200f));
            Assert.That(summary.meanMs, Is.EqualTo(105f));
        }

        [Test]
        public void Summarise_Empty_IsZero()
        {
            var summary = new LatencyStats().Summarise();

            Assert.That(summary.n, Is.Zero);
            Assert.That(summary.p95Ms, Is.Zero);
        }
    }

    public class MovementResponseTrackerTests
    {
        private const float Frame = 1f / 60f;

        private sealed class Driver
        {
            public readonly MovementResponseTracker Tracker = new(20f);
            public float Time;
            public Vector3 Position;
            public bool Moving;
            public bool Jumping;
            public bool PlatformMoving;

            public void Step(Vector3 velocity, int frames = 1)
            {
                for (int i = 0; i < frames; i++)
                {
                    Time += Frame;
                    Position += velocity * Frame;
                    Tracker.Add(new MovementSample(Time, Moving, Jumping, Position, false, PlatformMoving));
                }
            }
        }

        [Test]
        public void StartLatency_IsTimeUntilVisibleMotion()
        {
            var driver = new Driver();
            driver.Step(Vector3.zero, 10);

            driver.Moving = true;
            driver.Step(Vector3.zero, 12); // 200 ms of lag
            driver.Step(new Vector3(0f, 0f, 5f), 10);

            var start = driver.Tracker.Summarise(false).start;
            Assert.That(start.n, Is.EqualTo(1));
            Assert.That(start.p50Ms, Is.InRange(200f, 240f));
        }

        [Test]
        public void StopLatency_IsTimeUntilHalfSpeed()
        {
            var driver = new Driver { Moving = true };
            driver.Step(new Vector3(5f, 0f, 0f), 60);

            driver.Moving = false;
            driver.Step(new Vector3(5f, 0f, 0f), 18); // 300 ms still moving
            driver.Step(Vector3.zero, 30);

            var stop = driver.Tracker.Summarise(false).stop;
            Assert.That(stop.n, Is.EqualTo(1));
            Assert.That(stop.p50Ms, Is.InRange(300f, 420f));
        }

        [Test]
        public void JumpLatency_IsTimeUntilRise()
        {
            var driver = new Driver();
            driver.Step(Vector3.zero, 5);

            driver.Jumping = true;
            driver.Step(Vector3.zero, 6); // 100 ms
            driver.Step(new Vector3(0f, 8f, 0f), 5);

            var jump = driver.Tracker.Summarise(false).jump;
            Assert.That(jump.n, Is.EqualTo(1));
            Assert.That(jump.p50Ms, Is.InRange(100f, 150f));
        }

        [Test]
        public void NoResponse_CountsTimeout()
        {
            var driver = new Driver();
            driver.Step(Vector3.zero, 2);

            driver.Moving = true;
            driver.Step(Vector3.zero, 150);

            var start = driver.Tracker.Summarise(false).start;
            Assert.That(start.n, Is.Zero);
            Assert.That(start.timeouts, Is.EqualTo(1));
        }

        [Test]
        public void Teleport_CountsSnap()
        {
            var driver = new Driver();
            driver.Step(Vector3.zero, 3);
            driver.Position += new Vector3(2f, 0f, 0f);
            driver.Step(Vector3.zero);

            var summary = driver.Tracker.Summarise(false);
            Assert.That(summary.snaps, Is.EqualTo(1));
            Assert.That(summary.maxSnapM, Is.GreaterThan(1.5f));
        }

        [Test]
        public void FrameChange_DoesNotCountAsSnap()
        {
            var tracker = new MovementResponseTracker(20f);
            tracker.Add(new MovementSample(0f, false, false, Vector3.zero, false, false));
            tracker.Add(new MovementSample(Frame, false, false, new Vector3(50f, 0f, 0f), true, false));

            Assert.That(tracker.Summarise(false).snaps, Is.Zero);
        }

        [Test]
        public void Oscillation_WithSteadyInput_CountsReversals()
        {
            var driver = new Driver { Moving = true };
            driver.Step(new Vector3(5f, 0f, 0f), 30);

            for (int i = 0; i < 6; i++)
            {
                driver.Step(new Vector3(3f, 0f, 0f));
                driver.Step(new Vector3(-3f, 0f, 0f));
            }

            Assert.That(driver.Tracker.Summarise(false).reversals, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void MovingPlatform_GoesToMovingBucket()
        {
            var driver = new Driver { PlatformMoving = true };
            driver.Step(Vector3.zero, 3);
            driver.Moving = true;
            driver.Step(new Vector3(5f, 0f, 0f), 10);

            Assert.That(driver.Tracker.Summarise(true).start.n, Is.EqualTo(1));
            Assert.That(driver.Tracker.Summarise(false).start.n, Is.Zero);
        }

        [Test]
        public void IsPlatformMoving_DetectsSpeedAndTurn()
        {
            Assert.That(MovementTelemetryUseCase.IsPlatformMoving(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.identity, Frame), Is.False);
            Assert.That(MovementTelemetryUseCase.IsPlatformMoving(Vector3.zero, Quaternion.identity, new Vector3(0.1f, 0f, 0f), Quaternion.identity, Frame), Is.True);
            Assert.That(MovementTelemetryUseCase.IsPlatformMoving(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.Euler(0f, 1f, 0f), Frame), Is.True);
        }
    }

    public class HarnessPathsTests
    {
        [Test]
        public void TelemetryDirectory_MainProject()
        {
            Assert.That(HarnessPaths.TelemetryDirectory("C:/Proj/Assets"), Is.EqualTo("C:/Proj/Logs/net-telemetry"));
        }

        [Test]
        public void TelemetryDirectory_MppmClone_MapsToMainProject()
        {
            Assert.That(HarnessPaths.TelemetryDirectory("C:/Proj/Library/VP/mppm1234/Assets"), Is.EqualTo("C:/Proj/Logs/net-telemetry"));
        }
    }
}
