#nullable enable
using System;
using VContainer.Unity;

namespace TinCan.Core.UI
{
    /// <summary>Tells the cursor policy whether a menu is open, on every menu change.</summary>
    public sealed class MenuCursorPresenter : IInitializable, IDisposable
    {
        private readonly IMenuSystem _menus;
        private readonly CursorPolicyUseCase _cursor;

        public MenuCursorPresenter(IMenuSystem menus, CursorPolicyUseCase cursor)
        {
            _menus = menus;
            _cursor = cursor;
        }

        public void Initialize()
        {
            _menus.Changed += OnChanged;
            OnChanged();
        }

        public void Dispose() => _menus.Changed -= OnChanged;

        private void OnChanged() => _cursor.SetMenuOpen(_menus.IsOpen);
    }
}
