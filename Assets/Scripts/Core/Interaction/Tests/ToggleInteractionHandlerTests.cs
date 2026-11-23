#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Tests.Fakes;

namespace TinCan.Core.Interaction.Tests
{
    /// <summary>The one toggle handler (<see cref="ToggleInteractionHandler"/>): Interact flips any <see cref="IToggleable"/>.</summary>
    public class ToggleInteractionHandlerTests
    {
        private sealed class Switch : IToggleable
        {
            public bool On;
            public bool ServerToggle() => On = !On;
        }

        private sealed class NotASwitch : IInteractable { }

        private sealed class Player : IActor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
        }

        [Test]
        public void Interact_FlipsIt_EachTime()
        {
            var handler = new ToggleInteractionHandler(new FakeEventPublisher());
            var door = new Switch();

            handler.Handle(new InteractionContext(new Player(), door, null!));
            Assert.That(door.On, Is.True);

            handler.Handle(new InteractionContext(new Player(), door, null!));
            Assert.That(door.On, Is.False);
        }

        [Test]
        public void SomethingThatDoesNotToggle_IsLeftAlone()
        {
            var handler = new ToggleInteractionHandler(new FakeEventPublisher());

            Assert.DoesNotThrow(() => handler.Handle(new InteractionContext(new Player(), new NotASwitch(), null!)));
        }
    }
}
