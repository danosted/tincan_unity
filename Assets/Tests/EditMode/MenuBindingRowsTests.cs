#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;
using TinCan.Core.UI.Commands;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>The Controls menu inside <see cref="MenuUseCase"/>: a Bindings row becomes one row per key; invoking one rebinds it.</summary>
    public class MenuBindingRowsTests
    {
        private sealed class FakeBindings : IInputBindings
        {
            public readonly List<InputBindingSlot> List = new();
            public readonly Dictionary<int, string> Keys = new();
            public int Resets;

            public IReadOnlyList<InputBindingSlot> Slots => List;
            public string Describe(InputBindingSlot slot) => Keys[slot.BindingIndex];
            public InputBindingSlot? Rebinding { get; set; }
            public string? LastMessage { get; set; }
            public event Action? Changed;

            public void StartRebind(InputBindingSlot slot)
            {
                Rebinding = slot;
                Changed?.Invoke();
            }

            public void CancelRebind()
            {
                Rebinding = null;
            }

            public void ResetAll() => Resets++;

            public void Finish(string key, string? message = null)
            {
                Keys[Rebinding!.Value.BindingIndex] = key;
                Rebinding = null;
                LastMessage = message;
                Changed?.Invoke();
            }
        }

        private FakeBindings _bindings = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _controls = null!;
        private InputActionId _jump = null!;

        [SetUp]
        public void SetUp()
        {
            _jump = InputActionId.Create("Humanoid/Jump");
            _bindings = new FakeBindings();
            _bindings.List.Add(new InputBindingSlot(_jump, 0, "Jump", "Humanoid"));
            _bindings.List.Add(new InputBindingSlot(_jump, 1, "Sprint", "Humanoid"));
            _bindings.Keys[0] = "Space";
            _bindings.Keys[1] = "Left Shift";
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { new ResetBindingsMenuCommand(_bindings) }), _bindings);
            _controls = MenuDefinition.Create("controls", "Controls",
                new MenuItemDefinition { ItemId = "keys", Label = "Keys", Kind = MenuItemKind.Bindings },
                new MenuItemDefinition { ItemId = "reset", Label = "Reset", Kind = MenuItemKind.Command, CommandId = ResetBindingsMenuCommand.Id });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_controls);
            Object.DestroyImmediate(_jump);
        }

        [Test]
        public void BindingsRow_ExpandsToOneRowPerKey()
        {
            _menus.Open(_controls);

            var rows = _menus.Current!.Items;
            Assert.That(rows.Select(r => r.Kind), Is.EqualTo(new[] { MenuItemKind.Binding, MenuItemKind.Binding, MenuItemKind.Command }));
            Assert.That(rows[0].Label, Is.EqualTo("Humanoid: Jump"));
            Assert.That(rows[0].Value, Is.EqualTo("Space"));
        }

        [Test]
        public void InvokingAKey_WaitsForTheNewOne_ThenShowsIt()
        {
            _menus.Open(_controls);
            var jumpRow = _menus.Current!.Items[0].ItemId;

            _menus.Invoke(jumpRow);
            Assert.That(_menus.Current!.Items[0].Value, Is.EqualTo(MenuUseCase.WaitingForKey));

            _bindings.Finish("J");
            Assert.That(_menus.Current!.Items[0].Value, Is.EqualTo("J"));
        }

        [Test]
        public void RefusedKey_ShowsWhy()
        {
            _menus.Open(_controls);
            _menus.Invoke(_menus.Current!.Items[0].ItemId);

            _bindings.Finish("Space", "W is already Humanoid Move Forward.");

            Assert.That(_menus.Current!.Items.Last(r => r.Kind == MenuItemKind.Note).Label, Does.Contain("already"));
        }

        [Test]
        public void Reset_RunsTheCommand_AndLeavingCancelsARebind()
        {
            _menus.Open(_controls);
            _menus.Invoke("reset");
            Assert.That(_bindings.Resets, Is.EqualTo(1));

            _menus.Invoke(_menus.Current!.Items[0].ItemId);
            _menus.Back();
            Assert.That(_bindings.Rebinding, Is.Null);
        }
    }
}
