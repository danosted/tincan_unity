#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Possession;
using VContainer.Unity;

namespace TinCan.Core.UI
{
    /// <summary>
    /// <see cref="ICursorPolicy"/>, per frame: decides the mode from the menus, the asks and possession
    /// (<see cref="CursorPolicyProcessor"/>) and writes it to the device only when it changes, so it never fights the
    /// Editor freeing the cursor. It does not depend on the menus (the shipyard's commands reach the menu system and its
    /// stage asks for the cursor, so that would be a cycle): <see cref="MenuCursorPresenter"/> tells it when one is open.
    /// </summary>
    public sealed class CursorPolicyUseCase : ICursorPolicy, ITickable
    {
        private readonly INetworkService _network;
        private readonly IPossessionState _possession;
        private readonly ICursorDevice _device;
        private readonly List<(string Owner, CursorState Mode)> _asks = new();
        private CursorState? _applied;
        private bool _menuOpen;

        public CursorPolicyUseCase(INetworkService network, IPossessionState possession, ICursorDevice device)
        {
            _network = network;
            _possession = possession;
            _device = device;
        }

        public CursorState Mode => _applied ?? CursorState.Free;

        public void Request(string owner, CursorState mode)
        {
            int index = _asks.FindIndex(a => a.Owner == owner);
            if (index >= 0 && _asks[index].Mode == mode) return;
            if (index >= 0) _asks.RemoveAt(index);
            _asks.Add((owner, mode));
            Apply();
        }

        public void Release(string owner)
        {
            if (_asks.RemoveAll(a => a.Owner == owner) > 0) Apply();
        }

        /// <summary>A menu open frees the cursor over every ask (<see cref="MenuCursorPresenter"/>).</summary>
        public void SetMenuOpen(bool open)
        {
            _menuOpen = open;
            Apply();
        }

        public void Tick() => Apply();

        private void Apply()
        {
            CursorState? newest = _asks.Count > 0 ? _asks[_asks.Count - 1].Mode : null;
            var mode = CursorPolicyProcessor.Decide(_menuOpen, newest, _network.IsActive, _possession.CurrentPossession != null);
            if (_applied == mode) return;

            _applied = mode;
            _device.Apply(mode);
        }
    }
}
