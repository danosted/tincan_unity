#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.DesignedEvents;

namespace TinCan.Tests.EditMode
{
    public class EventCatalogTests
    {
        [Test]
        public void Catalog_IdsAreUnique()
        {
            var duplicates = EventCatalog.All.GroupBy(definition => definition.Id).Where(group => group.Count() > 1).Select(group => group.Key);
            Assert.That(duplicates, Is.Empty);
        }

        [Test]
        public void Catalog_EveryStepTypeHasAHandlerSomewhere()
        {
            var handled = typeof(EventDirectorUseCase).Assembly.GetTypes()
                .Where(type => !type.IsAbstract)
                .SelectMany(type => BaseTypes(type))
                .Where(type => type.IsGenericType &&
                    (type.GetGenericTypeDefinition() == typeof(EventActionHandler<>) || type.GetGenericTypeDefinition() == typeof(EventConditionHandler<>)))
                .Select(type => type.GetGenericArguments()[0])
                .ToHashSet();

            var missing = EventCatalog.All.SelectMany(definition => definition.Steps).Select(step => step.GetType()).Distinct()
                .Where(type => !handled.Contains(type)).Select(type => type.Name);
            Assert.That(missing, Is.Empty, "Write an EventActionHandler<T> / EventConditionHandler<T> and register it in the owning installer.");
        }

        private static System.Collections.Generic.IEnumerable<Type> BaseTypes(Type type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType) yield return current;
        }
    }
}
