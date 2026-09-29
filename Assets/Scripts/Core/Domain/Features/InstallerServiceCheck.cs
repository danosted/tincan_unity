#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VContainer;

namespace TinCan.Core.Domain.Features
{
    /// <summary>
    /// Checks that every service a feature installer registers can actually be built: each type its constructor or
    /// <c>[Inject]</c> members take must be registered by the core or by a loaded installer. A feature whose profile
    /// leaves out something it needs is found when the scope is configured, listing every gap at once, instead of
    /// one "No such registration" when the service is first resolved. A dependency taken as <see cref="IObjectResolver"/>
    /// (resolved optionally with <c>TryResolve</c>) and collections (which resolve empty) are never missing.
    /// Instance and factory registrations are not analysed: an instance is not injected, and a factory's lambda is opaque.
    /// </summary>
    public static class InstallerServiceCheck
    {
        private const BindingFlags Declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly FieldInfo ImplementationTypeField = typeof(RegistrationBuilder).GetField("ImplementationType", Internal)!;
        private static readonly FieldInfo ParametersField = typeof(RegistrationBuilder).GetField("Parameters", Internal)!;

        /// <summary>One registered implementation and the values its registration supplies itself (<c>WithParameter</c>).</summary>
        public readonly struct Service
        {
            public Service(Type implementation, IReadOnlyList<IInjectParameter> parameters)
            {
                Implementation = implementation;
                Parameters = parameters;
            }

            public Type Implementation { get; }
            public IReadOnlyList<IInjectParameter> Parameters { get; }
        }

        /// <summary>What one installer registered, as the services whose dependencies are checked.</summary>
        public readonly struct Installed
        {
            public Installed(FeatureInstaller installer, IReadOnlyList<Service> services)
            {
                Installer = installer;
                Services = services;
            }

            public FeatureInstaller Installer { get; }
            public IReadOnlyList<Service> Services { get; }
        }

        /// <summary>Runs each installer's <see cref="FeatureInstaller.Install"/> on <paramref name="builder"/>, in order, recording what each added.</summary>
        public static List<Installed> Install(IEnumerable<FeatureInstaller> installers, IContainerBuilder builder)
        {
            var result = new List<Installed>();
            foreach (var installer in installers)
            {
                int before = builder.Count;
                installer.Install(builder);

                var services = new List<Service>();
                for (int i = before; i < builder.Count; i++)
                {
                    if (AnalysedImplementation(builder[i]) is not { } type) continue;
                    var parameters = ParametersField.GetValue(builder[i]) as List<IInjectParameter>;
                    services.Add(new Service(type, (IReadOnlyList<IInjectParameter>?)parameters ?? Array.Empty<IInjectParameter>()));
                }
                result.Add(new Installed(installer, services));
            }
            return result;
        }

        /// <summary>
        /// One "installer: service needs dependency" line per dependency <paramref name="isProvided"/> rejects. At
        /// runtime pass the scope's builder (<c>t =&gt; builder.Exists(t, true, true)</c>) once every registration is made.
        /// </summary>
        public static List<string> FindMissing(IEnumerable<Installed> installed, Func<Type, bool> isProvided) =>
            installed
                .SelectMany(i => i.Services.SelectMany(service => NamedDependencies(service.Implementation)
                    .Where(d => !service.Parameters.Any(p => p.Match(d.Type, d.Name)))
                    .Select(d => d.Type)
                    .Where(t => !IsImplicit(t) && !isProvided(t))
                    .Distinct()
                    .Select(t => $"{i.Installer.name}: {service.Implementation.Name} needs {Describe(t)}")))
                .Distinct()
                .ToList();

        /// <summary>The types the container passes to <paramref name="implementation"/>: VContainer's constructor choice (the single [Inject] one, else the most parameters; none for a Component) and every [Inject] method, field and property, base types included.</summary>
        public static IEnumerable<Type> Dependencies(Type implementation) => NamedDependencies(implementation).Select(d => d.Type);

        // With the parameter or member name, which a registration's WithParameter(name, value) matches on.
        private static IEnumerable<(Type Type, string Name)> NamedDependencies(Type implementation)
        {
            if (!typeof(UnityEngine.Component).IsAssignableFrom(implementation))
            {
                var constructors = implementation.GetConstructors(Declared);
                var constructor = constructors.FirstOrDefault(c => c.IsDefined(typeof(InjectAttribute), false))
                                  ?? constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
                if (constructor != null)
                {
                    foreach (var parameter in constructor.GetParameters()) yield return (parameter.ParameterType, parameter.Name);
                }
            }

            for (var type = implementation; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var method in type.GetMethods(Declared).Where(m => m.IsDefined(typeof(InjectAttribute), false)))
                    foreach (var parameter in method.GetParameters()) yield return (parameter.ParameterType, parameter.Name);
                foreach (var field in type.GetFields(Declared).Where(f => f.IsDefined(typeof(InjectAttribute), false)))
                    yield return (field.FieldType, field.Name);
                foreach (var property in type.GetProperties(Declared).Where(p => p.IsDefined(typeof(InjectAttribute), false)))
                    yield return (property.PropertyType, property.Name);
            }
        }

        // Only plain and component registrations are built by injection; instances, factories and open generics are not analysed.
        private static Type? AnalysedImplementation(RegistrationBuilder registration)
        {
            var kind = registration.GetType().Name;
            if (kind != nameof(RegistrationBuilder) && kind != "ComponentRegistrationBuilder") return null;

            var type = ImplementationTypeField.GetValue(registration) as Type;
            return type == null || type.IsGenericTypeDefinition ? null : type;
        }

        private static bool IsImplicit(Type type) =>
            typeof(IObjectResolver).IsAssignableFrom(type)
            || type.IsArray
            || (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type.IsInterface);

        private static string Describe(Type type) => type.IsGenericType
            ? $"{type.Name.Substring(0, type.Name.IndexOf('`'))}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;
    }
}
