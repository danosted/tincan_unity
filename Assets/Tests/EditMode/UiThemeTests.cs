#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.UI;
using TinCan.Core.UI.Components;
using TinCan.Tests.EditMode.Fakes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The component-based theme: TinCan.tss is the panel's theme and imports every core component sheet; every sheet
    /// in the project takes colours and font sizes from the tokens only; a feature's component sheets reach the screen
    /// through its installer; the views compose components.
    /// </summary>
    public class UiThemeTests
    {
        private const string ThemePath = "Assets/UI/Theme/TinCan.tss";
        private const string TokensPath = "Assets/UI/Theme/Tokens.uss";
        private const string PanelSettingsPath = "Assets/Settings/UI/DefaultPanelSettings.asset";
        private const string CoreComponents = "Assets/Scripts/Core/UI/Components";

        private static readonly Regex Use = new(@"var\(\s*(--[\w-]+)\s*\)");
        private static readonly Regex Definition = new(@"^\s*(--[\w-]+)\s*:", RegexOptions.Multiline);
        private static readonly Regex LiteralColour = new(@"\brgba?\(|#[0-9a-fA-F]{3,8}\b");
        private static readonly Regex LiteralFontSize = new(@"font-size\s*:(?!\s*var\()");
        private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline);

        private static IEnumerable<string> ProjectSheets() =>
            Directory.EnumerateFiles(Application.dataPath, "*.uss", SearchOption.AllDirectories)
                .Select(path => "Assets/" + Path.GetRelativePath(Application.dataPath, path).Replace('\\', '/'))
                .Where(path => !path.StartsWith("Assets/UI Toolkit/", StringComparison.Ordinal));

        private static string Read(string assetPath) =>
            Comment.Replace(File.ReadAllText(Path.Combine(Application.dataPath, "..", assetPath)), string.Empty);

        [Test]
        public void TheTheme_IsThePanelsTheme()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            Assert.That(AssetDatabase.GetAssetPath(settings.themeStyleSheet), Is.EqualTo(ThemePath));
        }

        [Test]
        public void TheTheme_ImportsEveryCoreComponentSheet()
        {
            string theme = Read(ThemePath);
            var missing = ProjectSheets()
                .Where(path => path.StartsWith(CoreComponents + "/", StringComparison.Ordinal))
                .Where(path => !theme.Contains($"\"/{path}\""))
                .ToList();

            Assert.That(missing, Is.Empty, "Add these to TinCan.tss:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void EverySheet_TakesColoursAndFontSizesFromTheTokens()
        {
            var defined = Definition.Matches(Read(TokensPath)).Select(m => m.Groups[1].Value).ToHashSet();
            var offenders = new List<string>();
            foreach (var path in ProjectSheets().Where(p => p != TokensPath))
            {
                string text = Read(path);
                offenders.AddRange(Use.Matches(text).Select(m => m.Groups[1].Value)
                    .Where(token => !defined.Contains(token) && !token.StartsWith("--unity-", StringComparison.Ordinal))
                    .Distinct().Select(token => $"{path}: {token} is not a token"));
                if (LiteralColour.IsMatch(text)) offenders.Add($"{path}: a literal colour (use a token from Tokens.uss)");
                if (LiteralFontSize.IsMatch(text)) offenders.Add($"{path}: a literal font size (use --tc-font-*)");
            }

            Assert.That(offenders, Is.Empty, "\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void EveryFeatureSheet_IsContributedByAnInstaller()
        {
            var contributed = AssetDatabase.FindAssets("t:" + nameof(FeatureInstaller))
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<StyleSheet>>()
                .SelectMany(installer => installer.Contributions)
                .Select(AssetDatabase.GetAssetPath)
                .ToHashSet();
            var orphans = ProjectSheets()
                .Where(path => path.StartsWith("Assets/Scripts/", StringComparison.Ordinal) && !path.StartsWith(CoreComponents + "/", StringComparison.Ordinal))
                .Where(path => !contributed.Contains(path))
                .ToList();

            Assert.That(orphans, Is.Empty, "List these on their installer (IExtension<StyleSheet>):\n  " + string.Join("\n  ", orphans));
        }

        [Test]
        public void TheLayers_AddTheRootClassAndTheFeatureSheets()
        {
            var sheet = ScriptableObject.CreateInstance<StyleSheet>();
            var root = new VisualElement();

            UiLayers.AddLayers(root, new[] { sheet });

            Assert.That(root.ClassListContains(UiLayers.RootClass), Is.True);
            Assert.That(root.styleSheets.Contains(sheet), Is.True);
            Object.DestroyImmediate(sheet);
        }

        [Test]
        public void TheMeter_PicksItsBandByValue()
        {
            Assert.That(Meter.Band(0.1f), Is.EqualTo(Meter.BadUssClassName));
            Assert.That(Meter.Band(0.4f), Is.EqualTo(Meter.FairUssClassName));
            Assert.That(Meter.Band(0.9f), Is.Null, "the plain fill is the good colour");

            var meter = new Meter("Fuel", 0.1f);
            meter.Fill = 0.9f;
            Assert.That(meter.Q(className: Meter.FillUssClassName).ClassListContains(Meter.BadUssClassName), Is.False, "the band follows the value");
        }

        [Test]
        public void TheMenu_IsComposedFromComponents()
        {
            var layers = new FakeUiLayers();
            var menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            var host = new GameObject("ThemeTest");
            // Named test assemblies cannot reference the predefined Assembly-CSharp assembly.
            var view = host.AddComponent(Type.GetType("TinCan.App.Views.MenuOverlayView, TinCan.App", true)!);
            view.GetType().GetMethod("Construct")!.Invoke(view, new object[] { menus, layers });
            var menu = MenuDefinition.Create("main", "Main", new MenuItemDefinition { ItemId = "quit", Label = "Quit", Kind = MenuItemKind.Command });

            menus.Open(menu);

            var panel = layers.Root(UiLayer.Menus).Q<MenuPanel>();
            Assert.That(panel?.Title, Is.EqualTo("Main"));
            Assert.That(panel!.Q<MenuButton>()?.text, Is.EqualTo("Quit"));
            Assert.That(layers.Root(UiLayer.Menus).Q<Backdrop>(), Is.Not.Null);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(menu);
        }
    }
}
