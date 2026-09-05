#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using TinCan.Features.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class MenuOverlayViewTests
    {
        private GameObject _object = null!;
        private PanelSettings _settings = null!;
        private UIDocument _document = null!;
        private MenuDefinition _menu = null!;
        private MenuUseCase _menus = null!;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible;

        [SetUp]
        public void SetUp()
        {
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _object = new GameObject("MenuOverlayTest");
            _document = _object.AddComponent<UIDocument>();
            _document.panelSettings = _settings;
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            _menu = MenuDefinition.Create("join", "Join",
                new MenuItemDefinition { ItemId = "address", Label = "Address", Kind = MenuItemKind.TextField, DefaultValue = "" });

            // Named test assemblies cannot reference the predefined Assembly-CSharp assembly.
            var viewType = Type.GetType("TinCan.UI.MenuOverlayView, Assembly-CSharp", true)!;
            var view = _object.AddComponent(viewType);
            viewType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null);
            viewType.GetMethod("Construct")!.Invoke(view, new object[] { _menus, new FakeNetworkService() });
            _menus.Open(_menu);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_menu);
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
        }

        [Test]
        public void Typing_PreservesTheFieldAndStoresEveryEdit()
        {
            var field = _document.rootVisualElement.Q<TextField>();
            Assert.That(field, Is.Not.Null);
            foreach (char character in "192.168.1.42")
            {
                field.value += character;
                Assert.That(_document.rootVisualElement.Q<TextField>(), Is.SameAs(field));
                Assert.That(_menus.GetValue("address"), Is.EqualTo(field.value));
            }
        }

        [Test]
        public void ProgrammaticUpdate_RefreshesExistingFieldWithoutAnotherChangeEvent()
        {
            var field = _document.rootVisualElement.Q<TextField>();
            int changes = 0;
            field.RegisterValueChangedCallback(_ => changes++);

            _menus.SetValue("address", "10.0.0.7");

            Assert.That(_document.rootVisualElement.Q<TextField>(), Is.SameAs(field));
            Assert.That(field.value, Is.EqualTo("10.0.0.7"));
            Assert.That(changes, Is.Zero);
        }
    }
}
