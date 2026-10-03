#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Core.Interaction;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class InputRoutingUseCaseTests
    {
        private sealed class FixedContexts : IInputContexts
        {
            public List<InputContext> Live = new();
            public IReadOnlyList<InputContext> Active => Live;
            public bool IsActive(InputContext? context) => context != null && Live.Contains(context);
            public event Action? Changed { add { } remove { } }
        }

        private sealed class CountingHandler<T> : InputCommandHandler<T> where T : InputCommand
        {
            public int Calls;
            public bool Accepts = true;

            protected override bool Handle(T command)
            {
                Calls++;
                return Accepts;
            }
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public readonly List<string> Messages = new();
            public void Publish<TEvent>(TEvent evt) => Messages.Add(evt?.ToString() ?? string.Empty);
        }

        private InputActionId _cancel = null!;
        private MenuBackCommand _back = null!;
        private OpenMenuCommand _open = null!;
        private FakeInputReader _reader = null!;
        private FixedContexts _contexts = null!;
        private CountingHandler<MenuBackCommand> _backHandler = null!;
        private CountingHandler<OpenMenuCommand> _openHandler = null!;
        private RecordingPublisher _events = null!;

        [SetUp]
        public void SetUp()
        {
            _cancel = InputActionId.Create("Global/Cancel");
            _back = ScriptableObject.CreateInstance<MenuBackCommand>();
            _open = ScriptableObject.CreateInstance<OpenMenuCommand>();
            _reader = new FakeInputReader();
            _contexts = new FixedContexts();
            _backHandler = new CountingHandler<MenuBackCommand>();
            _openHandler = new CountingHandler<OpenMenuCommand>();
            _events = new RecordingPublisher();

            var menu = FakeInputContexts.Plain("Menu", InputContextActivation.WhileMenuOpen, 900, routes: new[] { new InputRoute(_cancel, _back) });
            var global = FakeInputContexts.Plain("Global", InputContextActivation.Always, 0, routes: new[] { new InputRoute(_cancel, _open) });
            _contexts.Live.AddRange(new[] { menu, global });
        }

        private InputRoutingUseCase Create(params IInputCommandHandler[] handlers) => new(_contexts, _reader, handlers, _events);

        [Test]
        public void Press_GoesToTheHighestContext_AndIsConsumed()
        {
            var router = Create(_backHandler, _openHandler);
            _reader.Triggered.Add(_cancel);

            router.Tick();

            Assert.That(_backHandler.Calls, Is.EqualTo(1));
            Assert.That(_openHandler.Calls, Is.Zero, "Cancel stepped back in the menu; it must not also open the main menu");
        }

        [Test]
        public void DeclinedCommand_FallsThroughToTheNextContext()
        {
            _backHandler.Accepts = false;
            var router = Create(_backHandler, _openHandler);
            _reader.Triggered.Add(_cancel);

            router.Tick();

            Assert.That(_backHandler.Calls, Is.EqualTo(1));
            Assert.That(_openHandler.Calls, Is.EqualTo(1));
        }

        [Test]
        public void NoPress_RunsNothing()
        {
            var router = Create(_backHandler, _openHandler);

            router.Tick();

            Assert.That(_backHandler.Calls + _openHandler.Calls, Is.Zero);
        }

        [Test]
        public void MissingHandler_IsReportedOnce_AndTheNextContextStillRuns()
        {
            var router = Create(_openHandler);
            _reader.Triggered.Add(_cancel);

            router.Tick();
            router.Tick();

            Assert.That(_openHandler.Calls, Is.EqualTo(2));
            Assert.That(_events.Messages.FindAll(m => m.Contains(nameof(MenuBackCommand))).Count, Is.EqualTo(1));
            Assert.That(router.HandlerFor(typeof(OpenMenuCommand)), Is.SameAs(_openHandler));
        }
    }
}
