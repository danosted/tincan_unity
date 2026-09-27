#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

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
            var actual = new List<string>();
            foreach (var (path, text) in Sources("Scripts/Features"))
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
            var text = Sources("Scripts/Core/Infrastructure").Single(s => s.Path.EndsWith("/ProjectLifetimeScope.cs")).Text;
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
