#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The UI theme (Assets/UI/Theme/TinCanTheme.uss): it imports, every token it uses is defined, the UI installer loads
    /// it, the layers carry its root class, and the HUD meter picks its band by value.
    /// </summary>
    public class UiThemeTests
    {
        private const string ThemePath = "Assets/UI/Theme/TinCanTheme.uss";
        private static readonly Regex Use = new(@"var\(\s*(--[\w-]+)\s*\)");
        private static readonly Regex Definition = new(@"^\s*(--[\w-]+)\s*:", RegexOptions.Multiline);

        [Test]
        public void TheTheme_Imports_AndDefinesEveryTokenItUses()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath), Is.Not.Null, $"{ThemePath} did not import as a style sheet");

            string text = File.ReadAllText(Path.Combine(Application.dataPath, "..", ThemePath));
            var defined = Definition.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
            var missing = Use.Matches(text).Select(m => m.Groups[1].Value).Where(token => !defined.Contains(token)).Distinct().ToList();

            Assert.That(missing, Is.Empty, "Used but never defined on .tc-root: " + string.Join(", ", missing));
        }

        [Test]
        public void TheUiInstaller_LoadsTheTheme()
        {
            var installer = AssetDatabase.LoadAssetAtPath<UiFeatureInstaller>("Assets/Resources/Installers/UiFeatureInstaller.asset");
            var theme = new SerializedObject(installer).FindProperty("_theme").objectReferenceValue;

            Assert.That(AssetDatabase.GetAssetPath(theme), Is.EqualTo(ThemePath));
        }

        [Test]
        public void TheLayers_SitUnderTheThemesRootClass()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);
            var root = new VisualElement();

            UiLayers.AddLayers(root, sheet);

            Assert.That(root.ClassListContains(UiLayers.RootClass), Is.True);
            Assert.That(root.styleSheets.Contains(sheet), Is.True);
        }

        [Test]
        public void TheMeter_PicksItsBandByValue()
        {
            // Named test assemblies cannot reference the predefined Assembly-CSharp assembly.
            var band = Type.GetType("TinCan.App.Views.HudOverlayView, TinCan.App", true)!.GetMethod("MeterBand", BindingFlags.Public | BindingFlags.Static)!;
            string? Band(float fill) => (string?)band.Invoke(null, new object[] { fill });

            Assert.That(Band(0.1f), Is.EqualTo("tc-meter__fill--bad"));
            Assert.That(Band(0.4f), Is.EqualTo("tc-meter__fill--fair"));
            Assert.That(Band(0.9f), Is.Null, "the plain fill is the good colour");
        }

        [Test]
        public void TheMenu_UsesThemeClasses()
        {
            var layers = new FakeUiLayers();
            var menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            var host = new GameObject("ThemeTest");
            var view = host.AddComponent(Type.GetType("TinCan.App.Views.MenuOverlayView, TinCan.App", true)!);
            view.GetType().GetMethod("Construct")!.Invoke(view, new object[] { menus, layers });
            var menu = MenuDefinition.Create("main", "Main", new MenuItemDefinition { ItemId = "quit", Label = "Quit", Kind = MenuItemKind.Command });

            menus.Open(menu);

            var panel = layers.Root(UiLayer.Menus).Q("menu-panel");
            Assert.That(panel.ClassListContains("tc-menu"), Is.True);
            Assert.That(panel.Q<Button>().ClassListContains("tc-menu__button"), Is.True);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(menu);
        }
    }
}
