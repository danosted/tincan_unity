#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Features.DesignedEvents;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>Covers the event catalog: events come from the loaded installers, and every contributed event is runnable.</summary>
    public class EventCatalogTests
    {
        private sealed class ContributingInstaller : FeatureInstaller, FeatureInstaller.IExtension<EventDefinition>
        {
            public List<EventDefinition?> Events = new();
            public override void Install(IContainerBuilder builder) { }
            IEnumerable<EventDefinition> FeatureInstaller.IExtension<EventDefinition>.Contributions => Events!;
        }

        private sealed class PlainInstaller : FeatureInstaller
        {
            public override void Install(IContainerBuilder builder) { }
        }

        private readonly List<Object> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Collect_GathersEventsFromContributingInstallers_SkippingNulls()
        {
            var groan = new EventDefinition.Builder(100, "Groan").Phase("Only", p => p.Duration(1f)).Build();
            var contributor = Create<ContributingInstaller>();
            contributor.Events.Add(groan);
            contributor.Events.Add(null);

            var events = EventCatalog.Collect(new FeatureInstallerCatalog(new FeatureInstaller[] { contributor, Create<PlainInstaller>() }));

            Assert.That(events, Is.EqualTo(new[] { groan }));
        }

        [Test]
        public void ContributedEvents_IdsAreUniqueAcrossFeatures()
        {
            var duplicates = AllContributedEvents().GroupBy(definition => definition.Id).Where(group => group.Count() > 1).Select(group => group.Key);
            Assert.That(duplicates, Is.Empty);
        }

        [Test]
        public void ContributedEvents_EveryStepTypeHasAHandlerSomewhere()
        {
            // Handlers live in the features that own the steps, so look in every project assembly.
            var handled = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("TinCan.", StringComparison.Ordinal) || a.GetName().Name == "Assembly-CSharp")
                .SelectMany(a => a.GetTypes())
                .Where(type => !type.IsAbstract)
                .SelectMany(BaseTypes)
                .Where(type => type.IsGenericType &&
                    (type.GetGenericTypeDefinition() == typeof(EventActionHandler<>) || type.GetGenericTypeDefinition() == typeof(EventConditionHandler<>)))
                .Select(type => type.GetGenericArguments()[0])
                .ToHashSet();

            var events = AllContributedEvents();
            Assert.That(events, Is.Not.Empty, "No installer contributes designed events, so this check would pass vacuously.");
            var missing = events.SelectMany(definition => definition.Steps).Select(step => step.GetType()).Distinct()
                .Where(type => !handled.Contains(type)).Select(type => type.Name);
            Assert.That(missing, Is.Empty, "Write an EventActionHandler<T> / EventConditionHandler<T> and register it in the owning installer.");
        }

        // Every event any installer asset in the project contributes.
        private static List<EventDefinition> AllContributedEvents() =>
            UnityEditor.AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets" })
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<FeatureInstaller>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<EventDefinition>>()
                .SelectMany(e => e.Contributions)
                .ToList();

        private static IEnumerable<Type> BaseTypes(Type type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType) yield return current;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
