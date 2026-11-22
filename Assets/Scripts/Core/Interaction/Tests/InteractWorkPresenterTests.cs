#nullable enable
using NUnit.Framework;
using TinCan.Core.Gas.Inputs;
using TinCan.Core.Targeting;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Core.Interaction.Tests
{
    /// <summary>The local work meter (<see cref="InteractWorkPresenter"/>): it fills while Interact is held on work.</summary>
    public class InteractWorkPresenterTests
    {
        private sealed class Target : IInteractionTarget
        {
            public Target(InteractionDefinition definition) => Definition = definition;
            public InteractionDefinition Definition { get; }
        }

        private InteractInput _input = null!;
        private TargetingDefinition _targeting = null!;
        private InteractionDefinition _work = null!;
        private InteractionDefinition _instant = null!;
        private FakeHudValues _hud = null!;
        private InteractWorkPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _input = ScriptableObject.CreateInstance<InteractInput>();
            _targeting = ScriptableObject.CreateInstance<TargetingDefinition>();
            _work = ScriptableObject.CreateInstance<InteractionDefinition>();
            _work.HoldSeconds = 1f;
            _instant = ScriptableObject.CreateInstance<InteractionDefinition>();
            _hud = new FakeHudValues();
            _presenter = new InteractWorkPresenter(new FakeActorRegistry(), new InteractionTargetingSettings(_input, _targeting), _hud);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_input);
            Object.DestroyImmediate(_targeting);
            Object.DestroyImmediate(_work);
            Object.DestroyImmediate(_instant);
        }

        [Test]
        public void HoldingOnWork_FillsTheMeter()
        {
            var vein = new Target(_work);

            _presenter.Step(vein, held: true, 0.25f);
            _presenter.Step(vein, held: true, 0.25f);

            Assert.That(_hud.Meters[InteractWorkPresenter.MeterKey], Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void LettingGo_EmptiesIt()
        {
            var vein = new Target(_work);
            _presenter.Step(vein, held: true, 0.5f);

            _presenter.Step(vein, held: false, 0.1f);

            Assert.That(_hud.Meters.ContainsKey(InteractWorkPresenter.MeterKey), Is.False);
            Assert.That(_presenter.Showing, Is.False);
        }

        [Test]
        public void ADoneJob_HidesTheMeter_UntilTheKeyIsLetGo()
        {
            var vein = new Target(_work);
            _presenter.Step(vein, held: true, 0.6f);
            _presenter.Step(vein, held: true, 0.6f);
            _presenter.Step(vein, held: true, 0.6f);

            Assert.That(_presenter.Showing, Is.False);
        }

        [Test]
        public void AnInstantInteraction_ShowsNoMeter()
        {
            _presenter.Step(new Target(_instant), held: true, 0.5f);

            Assert.That(_presenter.Showing, Is.False);
        }

        [Test]
        public void AnotherTarget_StartsOver()
        {
            _presenter.Step(new Target(_work), held: true, 0.5f);
            var other = new Target(_work);

            _presenter.Step(other, held: true, 0.1f);
            _presenter.Step(other, held: true, 0.2f);

            Assert.That(_hud.Meters[InteractWorkPresenter.MeterKey], Is.EqualTo(0.2f).Within(1e-4f));
        }
    }
}
