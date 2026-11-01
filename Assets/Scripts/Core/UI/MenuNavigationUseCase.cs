#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Keyboard and gamepad in menus, per frame, from the Menu context (live only while a menu is open, so nothing else
    /// hears these keys): Navigate up and down moves the focus, left and right adjust the focused row, Submit uses it.
    /// A held direction repeats after a short delay, the way a held key types. The stronger axis wins, so a stick held
    /// diagonally still moves one way.
    /// </summary>
    public sealed class MenuNavigationUseCase : ITickable
    {
        public const float RepeatDelaySeconds = 0.4f;
        public const float RepeatIntervalSeconds = 0.1f;
        private const float DeadZone = 0.5f;

        private readonly IInputReader _input;
        private readonly MenuInputContext _controls;
        private readonly IMenuSystem _menus;
        private readonly ITimeService _time;
        private Vector2Int _held;
        private float _nextRepeat;

        public MenuNavigationUseCase(IInputReader input, MenuInputContext controls, IMenuSystem menus, ITimeService time)
        {
            _input = input;
            _controls = controls;
            _menus = menus;
            _time = time;
        }

        public void Tick()
        {
            if (!_menus.IsOpen)
            {
                _held = Vector2Int.zero;
                return;
            }

            if (_input.WasPressedThisFrame(_controls.Submit)) _menus.Submit();

            // WASD also navigate, except on a text field: there they are letters being typed. The arrows always work.
            var value = _input.ReadVector2(_controls.Navigate);
            if (!OnTextField()) value += _input.ReadVector2(_controls.NavigateLetters);
            var direction = Direction(value);
            if (direction == Vector2Int.zero)
            {
                _held = Vector2Int.zero;
                return;
            }

            float now = _time.Time;
            if (direction == _held && now < _nextRepeat) return;

            _nextRepeat = now + (direction == _held ? RepeatIntervalSeconds : RepeatDelaySeconds);
            _held = direction;
            if (direction.y != 0) _menus.MoveFocus(-direction.y);
            else _menus.Adjust(direction.x);
        }

        private bool OnTextField() =>
            _menus.Current is { Focused: >= 0 } menu && menu.Items[menu.Focused].Kind == MenuItemKind.TextField;

        /// <summary>One of the four directions (or none) for a navigate value: the stronger axis, past a dead zone.</summary>
        public static Vector2Int Direction(Vector2 value)
        {
            if (Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y)) < DeadZone) return Vector2Int.zero;
            return Mathf.Abs(value.y) >= Mathf.Abs(value.x)
                ? new Vector2Int(0, value.y > 0f ? 1 : -1)
                : new Vector2Int(value.x > 0f ? 1 : -1, 0);
        }
    }
}
