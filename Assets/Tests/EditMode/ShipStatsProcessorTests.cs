#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipStatsProcessor"/> and the flight rules in <see cref="ShipDesignValidator"/>: parts and the frame sum to
    /// mass, lift, thrust and hull; a cairn's spirit lifts; speed grows with thrust over mass; a ship too heavy or without
    /// thrust cannot fly.
    /// </summary>
    public class ShipStatsProcessorTests
    {
        private ShipDesignTestParts _parts = null!;
        private readonly ShipStatsProcessor _stats = new();
        private readonly ShipStatsTuning _tuning = ShipStatsTuning.Default;

        [SetUp]
        public void SetUp() => _parts = new ShipDesignTestParts();

        [TearDown]
        public void TearDown() => _parts.Dispose();

        private ShipStats Compute(ShipDesign design) => _stats.Compute(design, _parts.Catalog, _tuning);

        /// <summary>The design's parts alone, without the test floor's weight.</summary>
        private static ShipDesign Bare(ShipDesign design) => design.WithFrame(ShipFrame.Empty, design.NextPartInstanceId);

        [Test]
        public void PartsSum_IntoTheShipsTotals_UnknownPartsLeftOut()
        {
            var stats = Compute(Bare(Design((Helm, 0, 0, 0), (Beam, -3, 0, 0), (Block, 0, 3, 0), ("hull.unknown", 5, 5, 0))));

            Assert.That((stats.Mass, stats.Lift, stats.Thrust, stats.Hull), Is.EqualTo((14f, 5000f, 100f, 65f)));
            Assert.That(stats.MaxHealth, Is.EqualTo(_tuning.MinHealth), "65 hull is below the minimum health");
        }

        [Test]
        public void ACairn_LiftsWithItsSpirit()
        {
            var stats = Compute(Bare(Design((Block, 0, 0, 0), (Cairn, 2, 0, 0))));

            Assert.That(stats.Lift, Is.EqualTo(_tuning.SpiritLift));
            Assert.That(stats.Mass, Is.EqualTo(21f));
        }

        [Test]
        public void TheFrame_AddsMassAndHull_ByPlankAreaAndBeamLength()
        {
            var tuning = ShipFrameTuning.Default;
            var withFrame = ShipFrameTestDesigns.Deck();
            var (area, length) = ShipFrameShape.From(withFrame.Frame).Totals();

            var framed = _stats.Compute(withFrame, _parts.Catalog, _tuning, tuning);
            var bare = Compute(Bare(withFrame));

            Assert.That(area, Is.EqualTo(16f).Within(0.05f), "4 x 4 m, slightly more for the drifted corner");
            Assert.That(length, Is.EqualTo(16f).Within(0.05f));
            Assert.That(framed.Mass - bare.Mass, Is.EqualTo(area * tuning.PlankMassPerSquareMetre + length * tuning.BeamMassPerMetre).Within(1e-3f));
            Assert.That(framed.Hull - bare.Hull, Is.EqualTo(area * tuning.PlankHullPerSquareMetre + length * tuning.BeamHullPerMetre).Within(1e-3f));
        }

        [Test]
        public void Speed_IsThrustOverMass_Capped()
        {
            var light = Compute(Bare(Design((Helm, 0, 0, 0))));
            Assert.That(light.MaxSpeed, Is.EqualTo(_tuning.MaxSpeed), "100 thrust on 10 mass is far past the cap");

            // 30 beams of 3 beside the helm, plus the helm's 10: mass 100.
            var beams = Enumerable.Range(1, 30).Select(i => (Beam, 3 * (i % 6) - 9, 2 + i / 6, (byte)0));
            var heavier = Compute(Bare(Design(new[] { (Helm, 0, -3, (byte)0) }.Concat(beams).ToArray())));
            Assert.That(heavier.Mass, Is.EqualTo(100f));
            Assert.That(heavier.MaxSpeed, Is.EqualTo(_tuning.SpeedPerThrustOverMass * 100f / 100f).Within(1e-4f));
            Assert.That(heavier.TurnSpeed, Is.EqualTo(_tuning.BaseTurnSpeed * _tuning.TurnScaleRange.y).Within(1e-4f),
                "lighter than the reference mass: the fastest turn");
        }

        [Test]
        public void AShipThatCannotFly_HasNoSpeed_AndTheValidatorSaysWhy()
        {
            var noHelm = Design((Block, 0, 0, 0));

            var stats = Compute(noHelm);
            var validation = new ShipDesignValidator().Validate(noHelm, _parts.Catalog, ShipDesignLimits.Default);

            Assert.That((stats.MaxSpeed, stats.TurnSpeed), Is.EqualTo((0f, 0f)));
            Assert.That(validation.Has(ShipDesignProblemKind.TooHeavy) && validation.Has(ShipDesignProblemKind.NoThrust), Is.True, validation.ToString());
        }

        [Test]
        public void ADesignGivenToAShip_SetsItsSpeedTurnAndHealth()
        {
            var ship = new FakeAirshipView("Ship");
            try
            {
                var state = new FakeShipDesignState(ship);
                var config = UnityEngine.ScriptableObject.CreateInstance<ShipDesignsConfig>();
                var designs = new ShipDesignUseCase(new FakeActorRegistry(), new FakeNetworkService(), _parts.Catalog,
                    new FileShipDesignStore(System.IO.Path.GetTempPath(), new System.Collections.Generic.Dictionary<string, string>(),
                        new ShipDesignJsonCodec(ShipDesignLimits.Default), 1000),
                    new ShipDesignValidator(), config, new FakeEventPublisher(), _stats, new string[0]);
                var design = Design((Helm, 0, 0, 0), (Beam, 2, 0, 0));

                Assert.That(designs.TryApply(state, design, out var error), Is.True, error);

                var expected = _stats.Compute(design, _parts.Catalog, config.Flight, config.Frames);
                Assert.That(ship.BaseStats, Is.EqualTo((expected.MaxSpeed, expected.TurnSpeed, expected.MaxHealth)));
                UnityEngine.Object.DestroyImmediate(config);
            }
            finally
            {
                ship.Destroy();
            }
        }
    }
}
