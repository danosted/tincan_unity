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

namespace TinCan.Tests.Architecture
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
        private static readonly Regex DirectInput = new(
            @"\b(Keyboard|Mouse|Gamepad|Pointer|Touchscreen|Joystick)\.current\b|\bUnityEngine\.Input\.|\bInput\.(GetKey|GetAxis|GetButton|GetMouseButton)\w*\b|\bFindAction\(|""<(Keyboard|Mouse|Gamepad|Pointer)>/");

        private static readonly Regex UiSurface = new(@"\bUIDocument\b|\bsortingOrder\b");
        private static readonly Regex CursorWrite = new(@"\bCursor\.(lockState|visible)\s*=(?!=)");
        private static readonly Regex OnGui = new(@"\bvoid\s+OnGUI\s*\(");
        private static readonly Regex InlineLook = new(
            @"\bstyle\.(color|backgroundColor|fontSize|unityFontStyleAndWeight|border(Top|Bottom|Left|Right)Color)\s*=(?!=)|\bstyle\s*=\s*\{[^}]*\b(color|backgroundColor|fontSize|unityFontStyleAndWeight)\s*=");

        /// <summary>The assemblies allowed to reference Unity's Input System (the input system, its asset builder); test assemblies may too.</summary>
        private static readonly HashSet<string> InputSystemAssemblies = new() { "TinCan.Input", "TinCan.DevTools.Editor" };

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
            var testText = string.Join("\n", TestSources()
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
                + "(AGENTS.md, CODE_STANDARDS.md §7). Add one in the Tests folder beside its assembly's asmdef.");
        }

        [Test]
        public void TestAssemblies_SitBesideWhatTheyTest()
        {
            // X.Tests lives in a Tests folder beside X's asmdef, is Editor only and compiles only with tests, and every
            // file in a Tests folder compiles into a test assembly (.docs/plans/test-assemblies.md).
            var offenders = new List<string>();
            foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
            {
                var name = assembly.name;
                var asmdef = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                if (string.IsNullOrEmpty(asmdef) || !asmdef.StartsWith("Assets/", StringComparison.Ordinal)) continue;

                var inTestFolder = assembly.sourceFiles.Select(f => f.Replace('\\', '/').Substring("Assets/".Length)).Where(IsTestPath).ToList();
                if (!IsTestAssembly(name))
                {
                    offenders.AddRange(inTestFolder.Select(f => $"{f} is in a Tests folder but compiles into {name}"));
                    continue;
                }

                // TinCan.Tests.Shared holds fakes that player code may use, so it is the one test assembly built for players.
                if (name == "TinCan.Tests.Shared") continue;
                var definition = JsonUtility.FromJson<AsmdefSettings>(File.ReadAllText(asmdef));
                if (definition.includePlatforms is not { Length: 1 } || definition.includePlatforms[0] != "Editor")
                    offenders.Add($"{name}: not Editor only");
                if (definition.defineConstraints == null || !definition.defineConstraints.Contains("UNITY_INCLUDE_TESTS"))
                    offenders.Add($"{name}: no UNITY_INCLUDE_TESTS define constraint");

                if (name.StartsWith("TinCan.Tests", StringComparison.Ordinal))
                {
                    if (!asmdef.StartsWith("Assets/Tests/", StringComparison.Ordinal)) offenders.Add($"{name}: a shared test assembly lives under Assets/Tests");
                    continue;
                }
                var tested = name.Substring(0, name.Length - ".Tests".Length);
                var testedAsmdef = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(tested);
                if (string.IsNullOrEmpty(testedAsmdef))
                    offenders.Add($"{name}: no assembly {tested} to test");
                else if (Path.GetDirectoryName(asmdef)!.Replace('\\', '/') != Path.GetDirectoryName(testedAsmdef)!.Replace('\\', '/') + "/Tests")
                    offenders.Add($"{name}: belongs in {Path.GetDirectoryName(testedAsmdef)!.Replace('\\', '/')}/Tests/");
            }

            Assert.That(offenders, Is.Empty,
                "Each assembly X keeps its tests in X.Tests, an Editor-only asmdef with the UNITY_INCLUDE_TESTS constraint in a "
                + "Tests folder beside X's asmdef (CODE_STANDARDS.md §7):\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void TestAssemblies_FollowTheOneWayRule()
        {
            // X.Tests may see what X builds on, any core system, and the shared fakes; never a feature X does not build
            // on. Only TinCan.Tests.Architecture and TinCan.Tests.Integration see everything.
            var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies()
                .Where(a => a.name.StartsWith("TinCan.", StringComparison.Ordinal))
                .ToDictionary(a => a.name);
            var closures = new Dictionary<string, HashSet<string>>();
            HashSet<string> Closure(string name)
            {
                if (closures.TryGetValue(name, out var known)) return known;
                var set = new HashSet<string> { name };
                closures[name] = set;
                if (assemblies.TryGetValue(name, out var assembly))
                    foreach (var reference in assembly.assemblyReferences.Where(r => !IsTestAssembly(r.name)))
                        set.UnionWith(Closure(reference.name));
                return set;
            }
            bool IsCoreSystem(string name) => assemblies.TryGetValue(name, out var a)
                && a.sourceFiles.Any(f => f.Replace('\\', '/').StartsWith("Assets/Scripts/Core/", StringComparison.Ordinal));
            string Tested(string testAssembly) => testAssembly.Substring(0, testAssembly.Length - ".Tests".Length);

            var offenders = new List<string>();
            foreach (var test in assemblies.Values.Where(a => IsTestAssembly(a.name)))
            {
                if (test.name is "TinCan.Tests.Architecture" or "TinCan.Tests.Integration" or "TinCan.Tests.Shared") continue;
                var allowed = test.name == "TinCan.Tests.Fakes" ? new HashSet<string>() : Closure(Tested(test.name));
                foreach (var reference in test.assemblyReferences.Select(r => r.name).Where(n => n.StartsWith("TinCan.", StringComparison.Ordinal)))
                {
                    if (reference is "TinCan.Tests.Fakes" or "TinCan.Tests.Shared") continue;
                    var seen = IsTestAssembly(reference) ? Tested(reference) : reference;
                    if (IsTestAssembly(reference) && reference.StartsWith("TinCan.Tests", StringComparison.Ordinal))
                        offenders.Add($"{test.name} -> {reference}");
                    else if (!allowed.Contains(seen) && !IsCoreSystem(seen))
                        offenders.Add($"{test.name} -> {reference}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "A test assembly sees only what its assembly builds on, the core systems and the shared fakes (CODE_MAP.md, "
                + "\"Assemblies and the one-way rule\"). Move the test to the assembly that builds on both, to "
                + "TinCan.Tests.Integration, or move the fake down to TinCan.Tests.Fakes:\n  " + string.Join("\n  ", offenders));
        }

        [Serializable]
        private sealed class AsmdefSettings
        {
            public string[]? includePlatforms;
            public string[]? defineConstraints;
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
        public void PrefabAudioSources_HaveAnOutputGroup()
        {
            var offenders = new List<string>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
                {
                    if (source.outputAudioMixerGroup == null) offenders.Add($"{path} ({source.name})");
                }
            }

            Assert.That(offenders, Is.Empty,
                "Every AudioSource in a prefab plays through a mixer group (World, Ui, Ambience, ...), so it can be "
                + "balanced and turned down; one-shots go through IAudioPlayer instead (.docs/plans/audio-mixer.md). "
                + "Set its Output:\n  " + string.Join("\n  ", offenders));
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
        public void Input_IsReadOnlyThroughContexts()
        {
            // Devices, Unity actions and binding paths live in one place (Core/Input, plus the asset builder), so what
            // listens to what is decided by the context assets and nothing polls a key behind their back.
            var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies()
                .Where(a => a.name.StartsWith("TinCan.", StringComparison.Ordinal) && !InputSystemAssemblies.Contains(a.name) && !IsTestAssembly(a.name))
                .Where(a => a.assemblyReferences.Any(r => r.name == "Unity.InputSystem"))
                .Select(a => $"{a.name} references Unity.InputSystem");
            var sources = Sources("Scripts")
                .Where(s => !s.Path.StartsWith("Scripts/Core/Input/", StringComparison.Ordinal) && !s.Path.StartsWith("Scripts/DevTools/Editor/", StringComparison.Ordinal))
                .Where(s => DirectInput.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => $"{s.Path} reads a device, an action by name or a binding path");

            var offenders = assemblies.Concat(sources).ToList();
            Assert.That(offenders, Is.Empty,
                "Read input through IInputReader and a context's typed actions, and route discrete actions to an "
                + "InputCommandHandler (.docs/INPUT.md). Only TinCan.Input touches Unity's Input System:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void ScreenUi_IsDrawnIntoTheLayers()
        {
            // One document, one draw order: a view takes its layer's root from IUiLayers instead of making a UIDocument
            // and typing a sort order (.docs/UI_FRAMEWORK.md, "Layers").
            var offenders = RuntimeSources()
                .Where(s => s.Path != "Scripts/Core/UI/UiLayers.cs")
                .Where(s => UiSurface.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Draw screen UI under IUiLayers.Root(UiLayer); only UiLayers makes a UIDocument or sets a sort order:\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void ScreenUi_TakesItsLookFromTheTheme()
        {
            // Colours, font sizes and weights live in Assets/UI/Theme/Tokens.uss; components and views add classes. A
            // value computed from data (a meter's width, a thumbnail) is still set in code.
            var offenders = RuntimeSources()
                .Where(s => s.Text.Contains("UnityEngine.UIElements"))
                .Where(s => InlineLook.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Style screen UI with theme classes (.docs/UI_FRAMEWORK.md, \"Theme\"), not inline colours or font sizes:\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void TheCursor_HasOneWriter()
        {
            var offenders = RuntimeSources()
                .Where(s => s.Path != "Scripts/Core/UI/UnityCursorDevice.cs")
                .Where(s => CursorWrite.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Ask ICursorPolicy for a cursor mode instead of writing Cursor.lockState or Cursor.visible:\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void OnGUI_OnlyInEditorCode()
        {
            var offenders = RuntimeSources()
                .Where(s => OnGui.IsMatch(WithoutCommentLines(s.Text)))
                .Select(s => s.Path)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Runtime UI is UI Toolkit on IUiLayers (dev readouts on UiLayer.Dev); IMGUI is for Editor code:\n  "
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

        [Test]
        public void AsyncMethods_TakeACancellationToken()
        {
            // UniTaskVoid is the fire-and-forget edge (a menu command, a bootstrap); whatever it awaits takes the token.
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var offenders = ProjectTypes()
                .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .SelectMany(t => t.GetMethods(all).Select(m => (Type: t, Method: m)))
                .Where(x => !x.Method.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Where(x => IsUniTask(x.Method.ReturnType))
                .Where(x => x.Method.GetParameters().All(p => p.ParameterType != typeof(System.Threading.CancellationToken)))
                .Select(x => $"{TypeName(x.Type)}.{x.Method.Name}")
                .Distinct()
                .ToList();

            Assert.That(offenders, Is.Empty,
                "An async method takes a CancellationToken and passes it on (CODE_STANDARDS.md §1):\n  " + string.Join("\n  ", offenders));
        }

        private static bool IsUniTask(Type type) =>
            type == typeof(Cysharp.Threading.Tasks.UniTask)
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Cysharp.Threading.Tasks.UniTask<>));

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
                    || (name.StartsWith("TinCan.", StringComparison.Ordinal) && !IsTestAssembly(name));
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
                && !IsTestAssembly(name));

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

        /// <summary>C# files under <c>Assets/{folder}</c> that are not tests, as a path relative to <c>Assets</c> and the text.</summary>
        private static IEnumerable<(string Path, string Text)> Sources(string folder) => AllSources(folder).Where(s => !IsTestPath(s.Path));

        /// <summary>Every test file: the shared test assemblies under Assets/Tests and each assembly's Tests folder.</summary>
        private static IEnumerable<(string Path, string Text)> TestSources() =>
            AllSources("Tests").Concat(AllSources("Scripts").Where(s => IsTestPath(s.Path)));

        private static IEnumerable<(string Path, string Text)> AllSources(string folder)
        {
            var root = Path.Combine(Application.dataPath, folder);
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Application.dataPath, file).Replace('\\', '/');
                yield return (relative, File.ReadAllText(file));
            }
        }

        /// <summary>A path relative to <c>Assets</c> inside a test assembly: under Assets/Tests, or in a Tests folder.</summary>
        private static bool IsTestPath(string path) =>
            path.StartsWith("Tests/", StringComparison.Ordinal) || path.Contains("/Tests/");

        /// <summary>A test assembly: an assembly's own <c>X.Tests</c>, or a shared <c>TinCan.Tests.*</c> one.</summary>
        private static bool IsTestAssembly(string name) =>
            name.StartsWith("TinCan.Tests", StringComparison.Ordinal) || name.EndsWith(".Tests", StringComparison.Ordinal);

        /// <summary>Scripts outside any Editor folder: the code that runs in a player.</summary>
        private static IEnumerable<(string Path, string Text)> RuntimeSources() =>
            Sources("Scripts").Where(s => !s.Path.Contains("/Editor/"));

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
