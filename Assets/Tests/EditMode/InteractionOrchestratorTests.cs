#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.Interaction;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers InteractionOrchestrator routing a target to its handler, and warning (once per handler type) when no
    /// loaded installer registers that handler, instead of doing nothing silently.
    /// </summary>
    public class InteractionOrchestratorTests
    {
        private sealed class RecordingHandler : IInteractionHandler
        {
            public int Handled { get; private set; }
            public void Handle(InteractionContext context) => Handled++;
        }

        private sealed class UnregisteredHandler : IInteractionHandler
        {
            public void Handle(InteractionContext context) { }
        }

        private sealed class Handlers : IInteractionHandlerRegistry
        {
            public readonly Dictionary<Type, IInteractionHandler> ByType = new();

            public bool TryGetHandler(Type handlerType, out IInteractionHandler handler) =>
                ByType.TryGetValue(handlerType, out handler!);

            public bool TryGetHandler<THandler>(out THandler handler) where THandler : class, IInteractionHandler
            {
                handler = (ByType.TryGetValue(typeof(THandler), out var found) ? found as THandler : null)!;
                return handler != null;
            }
        }

        private sealed class Target : IInteractionTarget
        {
            public Target(InteractionDefinition definition) => Definition = definition;
            public InteractionDefinition Definition { get; }
        }

        private sealed class PlainActor : IActor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
        }

        private readonly List<Object> _assets = new();
        private Handlers _handlers = null!;
        private InteractionOrchestrator _orchestrator = null!;

        [SetUp]
        public void SetUp()
        {
            _handlers = new Handlers();
            // Routing to a known target uses only the handler registry.
            _orchestrator = new InteractionOrchestrator(null!, null!, _handlers, null!);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void RegisteredHandler_HandlesTheInteraction()
        {
            var handler = new RecordingHandler();
            _handlers.ByType[typeof(RecordingHandler)] = handler;

            _orchestrator.HandleInteraction(new PlainActor(), new Target(Definition<RecordingHandler>("IA_Known")));

            Assert.That(handler.Handled, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingHandler_WarnsOncePerHandlerType()
        {
            var first = Definition<UnregisteredHandler>("IA_First");
            var second = Definition<UnregisteredHandler>("IA_Second");
            LogAssert.Expect(LogType.Warning, new Regex(@"No UnregisteredHandler is registered for IA_First.*FeatureProfile"));

            _orchestrator.HandleInteraction(new PlainActor(), new Target(first));
            _orchestrator.HandleInteraction(new PlainActor(), new Target(first));
            _orchestrator.HandleInteraction(new PlainActor(), new Target(second));

            LogAssert.NoUnexpectedReceived();
        }

        private InteractionDefinition Definition<THandler>(string name) where THandler : IInteractionHandler
        {
            var definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            definition.name = name;
            typeof(InteractionDefinition).GetField("_handlerTypeName", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(definition, typeof(THandler).AssemblyQualifiedName);
            _assets.Add(definition);
            return definition;
        }
    }
}
