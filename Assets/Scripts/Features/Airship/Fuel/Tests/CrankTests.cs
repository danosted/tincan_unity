#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Tests
{
    /// <summary>The hand crank: what a turn charges (<see cref="CrankProcessor"/>) and a turn into the tank (<see cref="CrankTurnUseCase.Turn"/>).</summary>
    public class CrankTests
    {
        private sealed class FakeCrank : IFuelCrank
        {
            public InteractionDefinition Definition => null!;
            public IFuelTank? Tank { get; set; }
            public int Turns { get; private set; }
            public void ServerTurned() => Turns++;
        }

        private FuelConfig _config = null!;
        private FakeFuelTank _tank = null!;
        private FakeCrank _crank = null!;
        private CrankTurnUseCase _turns = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<FuelConfig>();
            _config.CrankLitresPerTurn = 0.5f;
            _config.CrankMaxLevel = 10f;
            _tank = new FakeFuelTank { Level = 0f, Config = _config };
            _crank = new FakeCrank { Tank = _tank };
            _turns = new CrankTurnUseCase(new FakeNetworkService(), null!, null!, new CrankInput(null), new FakeEventPublisher());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void ATurn_ChargesItsLitres_UpToTheReserve_NeverPastCapacity()
        {
            Assert.That(CrankProcessor.Charge(0f, 100f, 10f, 0.5f), Is.EqualTo(0.5f));
            Assert.That(CrankProcessor.Charge(9.8f, 100f, 10f, 0.5f), Is.EqualTo(0.2f).Within(1e-4f), "tops up to the reserve");
            Assert.That(CrankProcessor.Charge(10f, 100f, 10f, 0.5f), Is.Zero, "at the reserve");
            Assert.That(CrankProcessor.Charge(4.8f, 5f, 10f, 0.5f), Is.EqualTo(0.2f).Within(1e-4f), "a small tank caps the reserve");
        }

        [Test]
        public void ATurn_OnADryTank_ChargesIt_AndTurnsTheHandle()
        {
            Assert.That(_turns.Turn(_crank, _tank), Is.True);

            Assert.That(_tank.Level, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_crank.Turns, Is.EqualTo(1));
        }

        [Test]
        public void AtTheReserve_ATurnDoesNothing()
        {
            _tank.Level = 10f;

            Assert.That(_turns.Turn(_crank, _tank), Is.False);

            Assert.That(_tank.Level, Is.EqualTo(10f));
            Assert.That(_crank.Turns, Is.Zero);
        }
    }
}
