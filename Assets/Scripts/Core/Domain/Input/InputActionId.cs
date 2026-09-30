#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// One player action (Jump, Fire, Cancel, ...) as a typed asset. Gameplay code and data (contexts, routes, ability
    /// inputs, scenarios) refer to actions only through these, never by name. The id points at the action in
    /// <c>Assets/Input/TinCanControls.inputactions</c>, which holds its default bindings; only the input system reads that.
    /// Built by TinCan > Dev > Input > Build Assets. Model: .docs/INPUT.md.
    /// </summary>
    [CreateAssetMenu(fileName = "Action_New", menuName = "TinCan/Input/Action Id")]
    public sealed class InputActionId : ScriptableObject
    {
        [Tooltip("The action's id in the input actions asset (set by the builder).")]
        [SerializeField] private string _actionId = string.Empty;
        [Tooltip("Map/Action in the input actions asset, for reading and logs only.")]
        [SerializeField] private string _path = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [TextArea, SerializeField] private string _description = string.Empty;
        [Tooltip("Shown in the Controls menu.")]
        [SerializeField] private bool _rebindable = true;

        public string ActionId => _actionId;
        public string Path => _path;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// <summary>What the action means to the player, shown in the Input Map report.</summary>
        public string Description => _description;

        public bool Rebindable => _rebindable;

        /// <summary>Builder and test helper.</summary>
        public void Configure(string actionId, string path, string displayName, string description, bool rebindable)
        {
            _actionId = actionId;
            _path = path;
            _displayName = displayName;
            _description = description;
            _rebindable = rebindable;
        }

        /// <summary>Test helper: an id not backed by an asset file (its action id is the path unless given).</summary>
        public static InputActionId Create(string path, string? actionId = null)
        {
            var id = CreateInstance<InputActionId>();
            id.name = path;
            id.Configure(actionId ?? path, path, path, string.Empty, true);
            return id;
        }

        public override string ToString() => string.IsNullOrEmpty(_path) ? name : _path;
    }
}
