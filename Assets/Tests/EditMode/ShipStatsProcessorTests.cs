#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipStatsProcessor"/> and the flight rules in <see cref="ShipDesignValidator"/>: parts sum to mass,
    /// lift, thrust and hull; speed grows with thrust over mass; a ship too heavy or without thrust cannot fly.
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

        [Test]
        public void PartsSum_IntoTheShipsTotals_UnknownPartsLeftOut()
        {
            var stats = Compute(Design((Helm, 0, 0, 0, 0), (Beam, -1, -1, 0, 0), (Block, 0, -1, 1, 0), ("hull.unknown", 5, 5, 5, 0)));

            Assert.That((stats.Mass, stats.Lift, stats.Thrust, stats.Hull), Is.EqualTo((14f, 100f, 100f, 65f)));
            Assert.That(stats.MaxHealth, Is.EqualTo(_tuning.MinHealth), "65 hull is below the minimum health");
        }

        [Test]
        public void Speed_IsThrustOverMass_Capped()
        {
            var light = Compute(Design((Helm, 0, 0, 0, 0)));
            Assert.That(light.MaxSpeed, Is.EqualTo(_tuning.MaxSpeed), "100 thrust on 10 mass is far past the cap");

            // 30 beams of 3, stacked under the helm, plus the helm's 10: mass 100, exactly what it lifts.
            var stack = System.Linq.Enumerable.Range(1, 30).Select(y => (Beam, -1, -y, 0, (byte)0));
            var heavier = Compute(Design(new[] { (Helm, 0, 0, 0, (byte)0) }.Concat(stack).ToArray()));
            Assert.That(heavier.Mass, Is.EqualTo(100f));
            Assert.That(heavier.MaxSpeed, Is.EqualTo(_tuning.SpeedPerThrustOverMass * 100f / 100f).Within(1e-4f));
            Assert.That(heavier.TurnSpeed, Is.EqualTo(_tuning.BaseTurnSpeed * _tuning.TurnScaleRange.y).Within(1e-4f),
                "lighter than the reference mass: the fastest turn");
        }

        [Test]
        public void AShipThatCannotFly_HasNoSpeed_AndTheValidatorSaysWhy()
        {
            var noHelm = Design((Block, 0, 0, 0, 0));

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
                var design = Design((Helm, 0, 0, 0, 0), (Beam, -1, -1, 0, 0));

                Assert.That(designs.TryApply(state, design, out var error), Is.True, error);

                var expected = _stats.Compute(design, _parts.Catalog, config.Flight);
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
