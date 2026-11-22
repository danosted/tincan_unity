#nullable enable
using NUnit.Framework;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Core.Interaction.Tests
{
    /// <summary>The HUD's Interact line (<see cref="InteractPromptPresenter"/>): what Interact does on the target in reach.</summary>
    public class InteractPromptPresenterTests
    {
        private class Target : IInteractionTarget
        {
            public Target(InteractionDefinition definition) => Definition = definition;
            public InteractionDefinition Definition { get; }
        }

        private sealed class CountingTarget : Target, IInteractionPrompt
        {
            public CountingTarget(InteractionDefinition definition) : base(definition) { }
            public string Prompt { get; set; } = string.Empty;
        }

        private InteractionDefinition _mine = null!;
        private InteractionDefinition _silent = null!;
        private FakeHudValues _hud = null!;
        private InteractPromptPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _mine = ScriptableObject.CreateInstance<InteractionDefinition>();
            _mine.Prompt = "Mine crystal";
            _silent = ScriptableObject.CreateInstance<InteractionDefinition>();
            _hud = new FakeHudValues();
            _presenter = new InteractPromptPresenter(new FakeActorRegistry(), _hud);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mine);
            Object.DestroyImmediate(_silent);
        }

        [Test]
        public void ATargetInReach_ShowsItsPrompt_AndGoesWhenOutOfReach()
        {
            _presenter.Step(new Target(_mine));
            Assert.That(_hud.All[InteractPromptPresenter.Key], Is.EqualTo("Mine crystal"));

            _presenter.Step(null);
            Assert.That(_hud.All.ContainsKey(InteractPromptPresenter.Key), Is.False);
        }

        [Test]
        public void ATargetWithoutAPrompt_ShowsNothing()
        {
            _presenter.Step(new Target(_silent));

            Assert.That(_hud.All.ContainsKey(InteractPromptPresenter.Key), Is.False);
        }

        [Test]
        public void ATargetsOwnPrompt_WinsOverItsInteractions()
        {
            var bin = new CountingTarget(_mine) { Prompt = "Crystal bin 4/12" };

            _presenter.Step(bin);

            Assert.That(_hud.All[InteractPromptPresenter.Key], Is.EqualTo("Crystal bin 4/12"));
        }

        [Test]
        public void TheSamePrompt_IsNotSetAgain()
        {
            var vein = new Target(_mine);
            _presenter.Step(vein);
            int calls = _hud.SetCalls;

            _presenter.Step(vein);

            Assert.That(_hud.SetCalls, Is.EqualTo(calls));
        }
    }
}
