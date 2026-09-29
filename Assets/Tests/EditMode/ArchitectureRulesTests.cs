#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Gas;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The checkable rules from AGENTS.md, ARCHITECTURE.md and CODE_STANDARDS.md. Known offenders live in
    /// <see cref="ArchitectureRulesBaseline"/>; a test fails on a new offender and on a fixed one still listed.
    /// Source scans are regex over text, not a parser, so a comment can trip one.
    /// </summary>
    public class ArchitectureRulesTests
    {
        private static readonly (string Name, Regex Pattern)[] GlobalLookups =
        {
            ("NetworkManager.Singleton", new Regex(@"\bNetworkManager\.Singleton\b")),
            ("Camera.main", new Regex(@"\bCamera\.main\b")),
            ("Find*ObjectsBy/OfType", new Regex(@"\bFind(Any|First)?Objects?(By|Of)Type\b")),
            ("GameObject.Find", new Regex(@"\bGameObject\.Find(WithTag|GameObjectsWithTag)?\b")),
            ("Resources.FindObjectsOfTypeAll", new Regex(@"\bResources\.FindObjectsOfTypeAll\b")),
        };

        private static readonly Regex Registration =
            new(@"^\s*(builder\.Register(?!BuildCallback)\w*|entryPoints\.Add)\b", RegexOptions.Multiline);
        private static readonly Regex NullableEnable = new(@"^\s*#nullable\s+enable\b", RegexOptions.Multiline);
        private static readonly Regex Region = new(@"^\s*#region\b", RegexOptions.Multiline);
        private static readonly Regex Coroutine =
            new(@"\b(Start|Stop)Coroutine\b|\bnew\s+(WaitForSeconds\w*|WaitForEndOfFrame|WaitForFixedUpdate|WaitUntil|WaitWhile)\b");
        private static readonly Regex TestedSuffix = new(@"(Processor|UseCase|InteractionHandler)$");
        private static readonly Regex EntityRegistration = new(@"\.(Register|Unregister)Entity\(");
        private static readonly Regex NewGuid = new(@"\bGuid\.NewGuid\(");

        /// <summary>Prefabs every scene spawns whatever its profile loads, so nothing on them may need a feature.</summary>
        private static readonly string[] SharedPrefabs =
        {
            "Assets/Prefabs/NetworkPlayer.prefab",
            "Assets/Prefabs/Airship/Airship_Prefab.prefab",
            "Assets/Prefabs/Test/TestShip_Prefab.prefab",
        };

        private const string ScopePrefab = "Assets/Prefabs/Singletons/GameLifetimeScope.prefab";

        /// <summary>Assemblies features are being carved out of; a new feature installer may not be compiled into one.</summary>
        private static readonly HashSet<string> SharedAssemblies = new() { "TinCan.Core.Domain", "TinCan.Core.Infrastructure", "TinCan.Features", "Assembly-CSharp" };

        private VContainer.Unity.LifetimeScope? _scope;

        [Test]
        public void NetworkBehaviours_EndInNetworkMediator()
        {
            var actual = ProjectTypes()
                .Where(t => typeof(NetworkBehaviour).IsAssignableFrom(t))
                .Select(TypeName)
                .Where(n => !n.EndsWith("NetworkMediator", StringComparison.Ordinal));

            AssertMatchesBaseline(actual, ArchitectureRulesBaseline.MisnamedNetworkBehaviours,
                nameof(ArchitectureRulesBaseline.MisnamedNetworkBehaviours),
                "Every NetworkBehaviour is named *NetworkMediator (AGENTS.md, CODE_STANDARDS.md §5). Rename it.");
        }

        [Test]
        public void Features_DoNotUseGlobalLookups()
        {
            // Features and the core systems (Core/<System>); Core/Domain and Core/Infrastructure are not gameplay code.
            var gameplay = Sources("Scripts/Features").Concat(Sources("Scripts/Core")
                .Where(s => !s.Path.StartsWith("Scripts/Core/Domain/", StringComparison.Ordinal)
                            && !s.Path.StartsWith("Scripts/Core/Infrastructure/", StringComparison.Ordinal)));
            var actual = new List<string>();
            foreach (var (path, text) in gameplay)
            {
                var code = WithoutCommentLines(text);
                foreach (var (name, pattern) in GlobalLookups)
                    if (pattern.IsMatch(code)) actual.Add($"{path} | {name}");
            }

            AssertMatchesBaseline(actual, ArchitectureRulesBaseline.GlobalLookupsInFeatures,
                nameof(ArchitectureRulesBaseline.GlobalLookupsInFeatures),
                "Features get dependencies by injection or a registry, never by global or scene lookup "
                + "(ARCHITECTURE.md §1, §5). Inject the service or register the object with the ActorOrchestrator.");
        }

        [Test]
        public void ProjectLifetimeScope_RegistrationsDoNotGrow()
        {
            var text = Sources("Scripts/App").Single(s => s.Path.EndsWith("/ProjectLifetimeScope.cs")).Text;
            var count = Registration.Matches(WithoutCommentLines(text)).Count;

            AssertRatchet(count, ArchitectureRulesBaseline.ProjectLifetimeScopeRegistrationLimit,
                nameof(ArchitectureRulesBaseline.ProjectLifetimeScopeRegistrationLimit),
                "registrations in ProjectLifetimeScope.cs",
                "New features register through a FeatureInstaller asset, not the root scope (AGENTS.md, "
                + "ARCHITECTURE.md §7, TUTORIAL_NEW_FEATURE.md). Write an installer.");
        }

        [Test]
        public void ProcessorsUseCasesAndHandlers_HaveTests()
        {
            // The rules files name the untested types themselves, so they do not count as tests.
            var testText = string.Join("\n", Sources("Tests")
                .Where(s => !s.Path.Contains("/ArchitectureRules"))
                .Select(s => s.Text));
            var actual = ProjectTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(TypeName)
                .Where(n => TestedSuffix.IsMatch(n))
                .Distinct()
                .Where(n => !Regex.IsMatch(testText, $@"\b{n}\b"));

            AssertMatchesBaseline(actual, ArchitectureRulesBaseline.UntestedTypes,
                nameof(ArchitectureRulesBaseline.UntestedTypes),
                "Every processor, use case and interaction handler ships with an EditMode test that names it "
                + "(AGENTS.md, CODE_STANDARDS.md §7). Add one under Assets/Tests/EditMode.");
        }

        [Test]
        public void Scripts_EnableNullable()
        {
            var missing = Sources("Scripts").Where(s => !NullableEnable.IsMatch(s.Text)).Select(s => s.Path).ToList();

            AssertRatchet(missing.Count, ArchitectureRulesBaseline.FilesWithoutNullableLimit,
                nameof(ArchitectureRulesBaseline.FilesWithoutNullableLimit),
                "scripts without #nullable enable",
                "Every script starts with #nullable enable (CODE_STANDARDS.md §4). Add it to the new file.");
        }

        [Test]
        public void NetworkedPrefabs_HaveExactlyOneEntity()
        {
            var offenders = new List<string>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<NetworkObject>() == null) continue;

                int entities = prefab.GetComponentsInChildren<TinCan.Core.Entities.EntityNetworkMediator>(true).Length;
                bool onRoot = prefab.GetComponent<TinCan.Core.Entities.EntityNetworkMediator>() != null;
                if (entities != 1 || !onRoot) offenders.Add($"{path} ({entities} entities, on root: {onRoot})");
            }

            Assert.That(offenders, Is.Empty,
                "Every networked prefab has one EntityNetworkMediator on its root: it owns the id and registers the object "
                + "(ARCHITECTURE.md, \"Entities\"). Add one:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void OnlyEntitiesRegister_AndOnlyEntityIdsMakeIds()
        {
            var registering = Sources("Scripts")
                .Where(s => !s.Path.EndsWith("/EntityNetworkMediator.cs") && EntityRegistration.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path);
            var makingIds = Sources("Scripts")
                .Where(s => !s.Path.EndsWith("/Entities/EntityIds.cs") && NewGuid.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path);

            Assert.That(registering.Concat(makingIds), Is.Empty,
                "Only EntityNetworkMediator registers (RegisterEntity), and ids come from entities: an actor takes its id "
                + "from ActorIdentity, a new id from EntityIds.New() (ARCHITECTURE.md, \"Entities\").");
        }

        [Test]
        public void FeatureInstallers_LiveInTheirOwnAssembly()
        {
            var actual = ProjectTypes()
                .Where(t => typeof(FeatureInstaller).IsAssignableFrom(t) && !t.IsAbstract)
                .Where(t => SharedAssemblies.Contains(t.Assembly.GetName().Name))
                .Select(TypeName);

            AssertMatchesBaseline(actual, ArchitectureRulesBaseline.InstallersInSharedAssemblies,
                nameof(ArchitectureRulesBaseline.InstallersInSharedAssemblies),
                "A feature is its own assembly: give its folder an asmdef named TinCan.Features.<Name> that references "
                + "only what the feature uses (CODE_MAP.md, \"Assemblies and the one-way rule\"; the worked example is "
                + "Features/GasChallenge).");
        }

        [Test]
        public void AssemblyCSharp_HoldsOnlyTheTopLayer()
        {
            // Unity's default assembly catches any script outside an asmdef and sees everything. Only the top layer
            // (composition root, NGO adapters, overlay views) may live there, and it shrinks as those get assemblies.
            var offenders = UnityEditor.Compilation.CompilationPipeline.GetAssemblies()
                .Where(a => a.name == "Assembly-CSharp")
                .SelectMany(a => a.sourceFiles)
                .Select(f => f.Replace('\\', '/'))
                .Where(f => f.StartsWith("Assets/Scripts/", StringComparison.Ordinal))
                .Where(f => !ArchitectureRulesBaseline.AssemblyCSharpFolders.Any(folder => f.StartsWith(folder, StringComparison.Ordinal)))
                .ToList();

            Assert.That(offenders, Is.Empty,
                "A script outside every asmdef compiles into Assembly-CSharp and can see everything (CODE_MAP.md, "
                + "\"Assemblies and the one-way rule\"). Put it in its feature's or system's assembly:\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void InteractionDefinitions_ResolveTheirHandler()
        {
            // The handler is stored as an assembly-qualified type name, so moving a handler to another assembly (or
            // renaming it) leaves the IA_* asset pointing at nothing and the interaction silently does nothing.
            var offenders = LoadAssets<TinCan.Core.Interaction.InteractionDefinition>()
                .Select(d => (Asset: d, Name: new UnityEditor.SerializedObject(d).FindProperty("_handlerTypeName").stringValue))
                .Where(d => !string.IsNullOrEmpty(d.Name) && d.Asset.HandlerType == null)
                .Select(d => $"{UnityEditor.AssetDatabase.GetAssetPath(d.Asset)}: {d.Name.Split(',')[0]} (stored as {string.Join(",", d.Name.Split(',').Skip(1).Take(1)).Trim()})")
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Every InteractionDefinition's handler resolves (CODE_MAP.md, \"Legacy, oddities and traps\"). The type "
                + "moved or was renamed: re-pick it in the IA_* asset's handler dropdown:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void GameplayCueNotifies_LoadAllTheirActions()
        {
            // Cue actions are [SerializeReference] data stored with their class, namespace and assembly name. Moving an
            // action type to another assembly or namespace without [MovedFrom] silently drops it from every GCN_* asset.
            var notifies = LoadAssets<TinCan.Core.Gas.Cues.GameplayCueNotify>().ToList();
            Assert.That(notifies, Is.Not.Empty, "No GameplayCueNotify assets found, so this rule would check nothing.");

            var offenders = notifies
                .Where(UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes)
                .Select(UnityEditor.AssetDatabase.GetAssetPath)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "These cue assets reference action types that no longer resolve. Give the moved action type "
                + "[MovedFrom(false, sourceAssembly: \"<old assembly>\")] (CODE_MAP.md, \"Legacy, oddities and traps\"):\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void SharedAssemblies_DoNotReferenceFeatureAssemblies()
        {
            var features = FeatureAssemblies();
            var offenders = UnityEditor.Compilation.CompilationPipeline.GetAssemblies()
                .Where(a => IsCoreAssembly(a.name))
                .SelectMany(a => a.assemblyReferences.Where(r => features.Contains(r.name)).Select(r => $"{a.name} -> {r.name}"))
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Core never depends on a feature (CODE_MAP.md, \"Assemblies and the one-way rule\"). Move the contract the "
                + "core needs down into Core.Domain and let the feature implement it:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void FeatureProfiles_ListNoCoreInstallers()
        {
            // Core installers load in every scene (FeatureInstallerCatalog.LoadForScene); a profile listing one suggests
            // it can be switched off, which it can't.
            var offenders = LoadAssets<FeatureProfile>()
                .SelectMany(p => p.Installers.Where(i => i != null && FeatureInstallerCatalog.IsCore(i)).Select(i => $"{p.name}: {i.name}"))
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Profiles list features only; core installers always load (FEATURE_INSTALLERS.md, \"Selecting features "
                + "per scene\"). Remove them from the profile:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void FeatureProfiles_LoadTheFeaturesTheirFeaturesReference()
        {
            // A feature assembly's references to other feature assemblies are its requirements: the compiler already
            // enforces them in code, so a profile must load them too.
            var features = FeatureAssemblies();
            var references = UnityEditor.Compilation.CompilationPipeline.GetAssemblies()
                .Where(a => features.Contains(a.name))
                .ToDictionary(a => a.name, a => a.assemblyReferences.Select(r => r.name).Where(features.Contains).ToList());

            var offenders = new List<string>();
            foreach (var profile in LoadAssets<FeatureProfile>())
            {
                var installers = profile.ResolveInstallers().ToList();
                var loaded = new HashSet<string>(installers.Select(i => i.GetType().Assembly.GetName().Name));
                foreach (var installer in installers)
                {
                    var assembly = installer.GetType().Assembly.GetName().Name;
                    if (!references.TryGetValue(assembly, out var required)) continue;
                    offenders.AddRange(required.Where(r => !loaded.Contains(r)).Select(r => $"{profile.name}: {installer.name} ({assembly}) needs {r}"));
                }
            }

            Assert.That(offenders.Distinct(), Is.Empty,
                "A profile loads every feature its features reference (FEATURE_INSTALLERS.md, \"Selecting features per "
                + "scene\"). Add the referenced feature's installer to the profile:\n  " + string.Join("\n  ", offenders.Distinct()));
        }

        [Test]
        public void FeatureProfiles_CanBuildTheirServices()
        {
            // Runs the real ProjectLifetimeScope.Configure (core registrations plus the profile's installers, then
            // InstallerServiceCheck) for every profile asset, on an inactive copy of the scope prefab: it registers but
            // never builds, so a profile that would refuse to start at Play fails here first.
            LogAssert.ignoreFailingMessages = true;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ScopePrefab);
            Assert.That(prefab, Is.Not.Null, $"Scope prefab moved or renamed: {ScopePrefab}. Update ScopePrefab.");

            // The test harness installer registers the active scenario's libraries, read from launch arguments and
            // MPPM player tags. Check the profiles as a plain Play would see them, not whatever scenario ran last.
            var launchArguments = typeof(LaunchArguments).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
            var savedArguments = launchArguments.GetValue(null);
            launchArguments.SetValue(null, Array.Empty<string>());

            var offenders = new List<string>();
            try
            {
                offenders.AddRange(ConfigureEveryProfile(prefab));
            }
            finally
            {
                launchArguments.SetValue(null, savedArguments);
            }

            Assert.That(offenders, Is.Empty,
                "Every service a profile's installers register must be buildable from that profile (FEATURE_INSTALLERS.md, "
                + "\"Features and shared prefabs\"). Add the installer that provides it to the profile, or resolve it "
                + "optionally through IObjectResolver.TryResolve:\n  " + string.Join("\n  ", offenders));
        }

        private static IEnumerable<string> ConfigureEveryProfile(GameObject prefab)
        {
            var offenders = new List<string>();
            foreach (var profile in LoadAssets<FeatureProfile>())
            {
                var host = new GameObject("ScopeUnderTest");
                host.SetActive(false);
                try
                {
                    var scope = UnityEngine.Object.Instantiate(prefab, host.transform).GetComponent<VContainer.Unity.LifetimeScope>();
                    scope.GetType().GetField("_featureProfile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scope, profile);
                    var configure = typeof(VContainer.Unity.LifetimeScope).GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    configure.Invoke(scope, new object[] { new ContainerBuilder { ApplicationOrigin = scope } });
                }
                catch (TargetInvocationException e)
                {
                    offenders.Add($"{profile.name} | {e.InnerException?.Message ?? e.Message}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }

            return offenders;
        }

        [Test]
        public void SharedPrefabs_DoNotRequireFeatureServices()
        {
            // Some installers warn about unassigned optional config while installing; that is not this rule's concern.
            LogAssert.ignoreFailingMessages = true;
            var installers = LoadAssets<FeatureInstaller>().ToList();
            // Core services always load, so only a service that only a feature installer provides is optional.
            var features = FeatureRegistrations(installers.Where(i => !FeatureInstallerCatalog.IsCore(i)));
            bool FeatureProvided(Type type) => features.Exists(type, includeInterfaceTypes: true);

            var granted = new HashSet<UnityEngine.Object>(installers
                .OfType<FeatureInstaller.IExtension<ActorAbilityGrant>>()
                .SelectMany(e => e.Contributions)
                .Where(g => g?.Ability != null)
                .Select(g => (UnityEngine.Object)g.Ability!));

            var actual = new List<string>();
            foreach (var path in SharedPrefabs)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, $"Shared prefab moved or renamed: {path}. Update SharedPrefabs.");
                var name = Path.GetFileNameWithoutExtension(path);

                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    foreach (var dependency in InstallerServiceCheck.Dependencies(component.GetType()).Where(t => !IsCollection(t) && FeatureProvided(t)).Distinct())
                        actual.Add($"{name} | {TypeName(component.GetType())} | {TypeName(dependency)}");

                    var starting = new UnityEditor.SerializedObject(component).FindProperty("_startingAbilities");
                    if (starting is not { isArray: true }) continue;
                    for (int i = 0; i < starting.arraySize; i++)
                    {
                        var ability = starting.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (ability != null && granted.Contains(ability))
                            actual.Add($"{name} | {TypeName(component.GetType())} | starting ability {ability.name}");
                    }
                }
            }

            AssertMatchesBaseline(actual, ArchitectureRulesBaseline.SharedPrefabFeatureDependencies,
                nameof(ArchitectureRulesBaseline.SharedPrefabFeatureDependencies),
                "Shared prefabs carry core components and sockets only (FEATURE_INSTALLERS.md, \"Features and shared "
                + "prefabs\"). Resolve a feature's service optionally through IObjectResolver.TryResolve, and let the "
                + "feature grant its abilities with an ActorAbilityGrant instead of the prefab's starting list.");
        }

        [Test]
        public void Scripts_HaveNoRegionsOrCoroutines()
        {
            var offenders = Sources("Scripts")
                .Where(s => Region.IsMatch(s.Text) || Coroutine.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "No #region and no coroutines (CODE_STANDARDS.md §1, §6). Use async/await or a tick instead, "
                + "and split the class rather than folding it:\n  " + string.Join("\n  ", offenders));
        }

        private static void AssertMatchesBaseline(IEnumerable<string> actual, IEnumerable<string> baseline,
            string baselineName, string rule)
        {
            var actualSet = new SortedSet<string>(actual, StringComparer.Ordinal);
            var baselineSet = new SortedSet<string>(baseline, StringComparer.Ordinal);
            var added = actualSet.Except(baselineSet).ToList();
            var fixedOnes = baselineSet.Except(actualSet).ToList();

            var message = "";
            if (added.Count > 0)
                message += $"New violation(s). {rule}\n  " + string.Join("\n  ", added) + "\n";
            if (fixedOnes.Count > 0)
                message += $"Fixed, so remove from ArchitectureRulesBaseline.{baselineName}:\n  "
                    + string.Join("\n  ", fixedOnes) + "\n";
            if (message.Length > 0) Assert.Fail(message);
        }

        private static void AssertRatchet(int count, int limit, string limitName, string what, string rule)
        {
            if (count > limit)
                Assert.Fail($"{count} {what}, limit {limit}. {rule}");
            if (count < limit)
                Assert.Fail($"Improved: {count} {what}. Lower ArchitectureRulesBaseline.{limitName} to {count}.");
        }

        private static IEnumerable<Type> ProjectTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                var isProject = name == "Assembly-CSharp"
                    || (name.StartsWith("TinCan.", StringComparison.Ordinal)
                        && !name.StartsWith("TinCan.Tests", StringComparison.Ordinal));
                if (!isProject) continue;

                Type?[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (var type in types)
                    if (type != null && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                        yield return type;
            }
        }

        /// <summary>The given installers, installed into one builder: with feature installers, the service types only a feature provides.</summary>
        private ContainerBuilder FeatureRegistrations(IEnumerable<FeatureInstaller> installers)
        {
            var builder = SceneBuilder();
            InstallerServiceCheck.Install(installers, builder);
            Assert.That(builder.Exists(typeof(TinCan.Features.Airship.Damage.IShipBreakage), includeInterfaceTypes: true), Is.True,
                "Installer discovery found no feature services, so this rule would check nothing.");
            return builder;
        }

        // RegisterComponentInHierarchy reads the scene from the builder's LifetimeScope. An inactive scope never runs
        // Awake, so it supplies the scene without building a container.
        private ContainerBuilder SceneBuilder()
        {
            if (_scope == null)
            {
                var host = new GameObject("InstallerCheckScope");
                host.SetActive(false);
                _scope = host.AddComponent<VContainer.Unity.LifetimeScope>();
            }
            return new ContainerBuilder { ApplicationOrigin = _scope };
        }

        [TearDown]
        public void DestroyScope()
        {
            if (_scope != null) UnityEngine.Object.DestroyImmediate(_scope.gameObject);
            _scope = null;
        }

        /// <summary>
        /// Core assemblies: every TinCan.* assembly that is not a feature (TinCan.Features.*), DevTools or a test assembly,
        /// plus the shared TinCan.Features block. Core may never reference a feature.
        /// </summary>
        private static bool IsCoreAssembly(string name) =>
            name == "TinCan.Features"
            || (name.StartsWith("TinCan.", StringComparison.Ordinal)
                && !name.StartsWith("TinCan.Features.", StringComparison.Ordinal)
                && !name.StartsWith("TinCan.DevTools", StringComparison.Ordinal)
                && !name.StartsWith("TinCan.Tests", StringComparison.Ordinal));

        /// <summary>Feature assemblies: a TinCan.Features.* assembly that defines a feature installer.</summary>
        private static HashSet<string> FeatureAssemblies() => new(ProjectTypes()
            .Where(t => typeof(FeatureInstaller).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Assembly.GetName().Name)
            .Where(n => n.StartsWith("TinCan.Features.", StringComparison.Ordinal)));

        private static bool IsCollection(Type type) =>
            type.IsArray || (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type));

        private static IEnumerable<T> LoadAssets<T>() where T : UnityEngine.Object =>
            UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" })
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null);

        private static string TypeName(Type type)
        {
            var tick = type.Name.IndexOf('`');
            return tick < 0 ? type.Name : type.Name.Substring(0, tick);
        }

        /// <summary>C# files under <c>Assets/{folder}</c>, as a path relative to <c>Assets</c> and the text.</summary>
        private static IEnumerable<(string Path, string Text)> Sources(string folder)
        {
            var root = Path.Combine(Application.dataPath, folder);
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Application.dataPath, file).Replace('\\', '/');
                yield return (relative, File.ReadAllText(file));
            }
        }

        private static string WithoutCommentLines(string text) =>
            string.Join("\n", text.Split('\n').Where(line =>
            {
                var trimmed = line.TrimStart();
                return !trimmed.StartsWith("//", StringComparison.Ordinal)
                    && !trimmed.StartsWith("*", StringComparison.Ordinal)
                    && !trimmed.StartsWith("/*", StringComparison.Ordinal);
            }));
    }
}
