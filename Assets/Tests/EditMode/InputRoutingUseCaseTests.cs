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
        private ExitVehicleCommand _exit = null!;
        private OpenMenuCommand _open = null!;
        private FakeInputReader _reader = null!;
        private FixedContexts _contexts = null!;
        private CountingHandler<ExitVehicleCommand> _exitHandler = null!;
        private CountingHandler<OpenMenuCommand> _openHandler = null!;
        private RecordingPublisher _events = null!;

        [SetUp]
        public void SetUp()
        {
            _cancel = InputActionId.Create("Global/Cancel");
            _exit = ScriptableObject.CreateInstance<ExitVehicleCommand>();
            _open = ScriptableObject.CreateInstance<OpenMenuCommand>();
            _reader = new FakeInputReader();
            _contexts = new FixedContexts();
            _exitHandler = new CountingHandler<ExitVehicleCommand>();
            _openHandler = new CountingHandler<OpenMenuCommand>();
            _events = new RecordingPublisher();

            var airship = FakeInputContexts.Plain("Airship", InputContextActivation.WhilePossessing, 300, routes: new[] { new InputRoute(_cancel, _exit) });
            var global = FakeInputContexts.Plain("Global", InputContextActivation.Always, 0, routes: new[] { new InputRoute(_cancel, _open) });
            _contexts.Live.AddRange(new[] { airship, global });
        }

        private InputRoutingUseCase Create(params IInputCommandHandler[] handlers) => new(_contexts, _reader, handlers, _events);

        [Test]
        public void Press_GoesToTheHighestContext_AndIsConsumed()
        {
            var router = Create(_exitHandler, _openHandler);
            _reader.Triggered.Add(_cancel);

            router.Tick();

            Assert.That(_exitHandler.Calls, Is.EqualTo(1));
            Assert.That(_openHandler.Calls, Is.Zero, "Cancel left the helm; it must not also open the menu");
        }

        [Test]
        public void DeclinedCommand_FallsThroughToTheNextContext()
        {
            _exitHandler.Accepts = false;
            var router = Create(_exitHandler, _openHandler);
            _reader.Triggered.Add(_cancel);

            router.Tick();

            Assert.That(_exitHandler.Calls, Is.EqualTo(1));
            Assert.That(_openHandler.Calls, Is.EqualTo(1));
        }

        [Test]
        public void NoPress_RunsNothing()
        {
            var router = Create(_exitHandler, _openHandler);

            router.Tick();

            Assert.That(_exitHandler.Calls + _openHandler.Calls, Is.Zero);
        }

        [Test]
        public void MissingHandler_IsReportedOnce_AndTheNextContextStillRuns()
        {
            var router = Create(_openHandler);
            _reader.Triggered.Add(_cancel);

            router.Tick();
            router.Tick();

            Assert.That(_openHandler.Calls, Is.EqualTo(2));
            Assert.That(_events.Messages.FindAll(m => m.Contains(nameof(ExitVehicleCommand))).Count, Is.EqualTo(1));
            Assert.That(router.HandlerFor(typeof(OpenMenuCommand)), Is.SameAs(_openHandler));
        }
    }
}
