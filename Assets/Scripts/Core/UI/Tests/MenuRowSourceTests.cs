#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.UI;
using UnityEngine;

namespace TinCan.Core.UI.Tests
{
    /// <summary>A <see cref="MenuItemKind.Rows"/> placeholder: rows from a source, live, picked back to the source.</summary>
    public class MenuRowSourceTests
    {
        private sealed class ListSource : IMenuRowSource
        {
            public readonly List<MenuSourceRow> List = new();
            public readonly List<bool> ShownCalls = new();
            public readonly List<string> Invoked = new();

            public string SourceId => "list";
            public IReadOnlyList<MenuSourceRow> Rows => List;
            public string EmptyLabel { get; set; } = "Nothing yet.";
            public event Action? Changed;

            public void SetShown(bool shown) => ShownCalls.Add(shown);
            public void Invoke(string key, MenuContext context) => Invoked.Add(key);

            public void Set(params MenuSourceRow[] rows)
            {
                List.Clear();
                List.AddRange(rows);
                Changed?.Invoke();
            }
        }

        private ListSource _source = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _menu = null!;
        private MenuDefinition _other = null!;

        [SetUp]
        public void SetUp()
        {
            _source = new ListSource();
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()), null, null, new IMenuRowSource[] { _source });
            _other = MenuDefinition.Create("other", "Other", new MenuItemDefinition { ItemId = "back", Label = "Back", Kind = MenuItemKind.Back });
            _menu = MenuDefinition.Create("join", "Join",
                new MenuItemDefinition { ItemId = "connect", Label = "Connect", Kind = MenuItemKind.Command, CommandId = "none" },
                new MenuItemDefinition { ItemId = "games", Label = "Games on this network", Kind = MenuItemKind.Rows, RowSource = "list" },
                new MenuItemDefinition { ItemId = "more", Label = "More", Kind = MenuItemKind.Submenu, Submenu = _other },
                new MenuItemDefinition { ItemId = "back", Label = "Back", Kind = MenuItemKind.Back });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_menu);
            UnityEngine.Object.DestroyImmediate(_other);
        }

        private string[] Ids() => _menus.Current!.Items.Select(r => r.ItemId).ToArray();

        [Test]
        public void NoRows_ShowsTheHeadingAndTheEmptyNote()
        {
            _menus.Open(_menu);

            Assert.That(Ids(), Is.EqualTo(new[] { "connect", "games/heading", "games/empty", "more", "back" }));
            Assert.That(_menus.Current!.Items[2].Label, Is.EqualTo("Nothing yet."));
            Assert.That(_menus.Current.Items[2].Kind, Is.EqualTo(MenuItemKind.Note));
        }

        [Test]
        public void Rows_AreLive_WhileTheMenuIsOpen()
        {
            _menus.Open(_menu);

            _source.Set(new MenuSourceRow("a", "Ada's game  2/8"), new MenuSourceRow("b", "Bo's game  1/8"));

            Assert.That(Ids(), Is.EqualTo(new[] { "connect", "games/heading", "games/a", "games/b", "more", "back" }));
            Assert.That(_menus.Current!.Items[2].Kind, Is.EqualTo(MenuItemKind.Command));
            Assert.That(_menus.Current.Items[2].Label, Is.EqualTo("Ada's game  2/8"));
        }

        [Test]
        public void ADisabledRow_IsANote_AndCannotBePicked()
        {
            _menus.Open(_menu);
            _source.Set(new MenuSourceRow("old", "Old game  (version 0.3)", enabled: false));

            _menus.Invoke("games/old");

            Assert.That(_menus.Current!.Items[2].Kind, Is.EqualTo(MenuItemKind.Note));
            Assert.That(_source.Invoked, Is.Empty);
        }

        [Test]
        public void PickingARow_GoesToTheSource_WithItsKey()
        {
            _menus.Open(_menu);
            _source.Set(new MenuSourceRow("a", "Ada's game"));

            _menus.Invoke("games/a");

            Assert.That(_source.Invoked, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Focus_StaysOnARow_AsTheListChanges()
        {
            _menus.Open(_menu);
            _source.Set(new MenuSourceRow("b", "Bo's game"));
            _menus.Focus("games/b");

            _source.Set(new MenuSourceRow("a", "Ada's game"), new MenuSourceRow("b", "Bo's game"));

            Assert.That(_menus.Current!.Items[_menus.Current.Focused].ItemId, Is.EqualTo("games/b"));
        }

        [Test]
        public void TheSourceHears_WhenItIsOnScreen()
        {
            _menus.Open(_menu);
            _menus.Invoke("more");
            _menus.Back();
            _menus.CloseAll();

            Assert.That(_source.ShownCalls, Is.EqualTo(new[] { true, false, true, false }));
        }
    }
}
