#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// A set of actions that mean something while a condition holds, like Unreal's input mapping contexts. The asset is
    /// the whole answer to "what listens to what" for its situation:
    /// <list type="bullet">
    /// <item><see cref="Activation"/>: when it is active (evaluated every frame from possession, tags and menus).</item>
    /// <item><see cref="Priority"/> and <see cref="Blocks"/>: which contexts it silences while active (the menu blocks
    /// gameplay; the gunner blocks walking and looking).</item>
    /// <item>Its actions: read every frame by the system that owns the context (typed slots on a subclass, such as
    /// <c>HumanoidInputContext.Move</c>) or packed into the predicted input as ability bits.</item>
    /// <item><see cref="Routes"/>: discrete actions turned into commands, consumed by the highest context that handles them.</item>
    /// </list>
    /// An action outside every active, unblocked context reads as released. Model: .docs/INPUT.md.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_New", menuName = "TinCan/Input/Context")]
    public class InputContext : ScriptableObject
    {
        [TextArea, SerializeField] private string _description = string.Empty;

        [Header("When")]
        [SerializeField] private InputContextActivation _activation;
        [SerializeField] private PossessedActorKind _possessedKind;
        [SerializeField] private GameplayTag? _tag;

        [Header("Precedence")]
        [Tooltip("Higher runs first: its routes see an action before lower contexts do.")]
        [SerializeField] private int _priority;
        [Tooltip("Silences every lower-priority context while this one is active (menus, rebinding).")]
        [SerializeField] private bool _blocksAllLower;
        [Tooltip("Contexts silenced while this one is active.")]
        [SerializeField] private List<InputContext> _blocks = new();

        [Header("What it listens to")]
        [Tooltip("Actions this context turns on besides its typed slots (for example ability inputs).")]
        [SerializeField] private List<InputActionId> _actions = new();
        [SerializeField] private List<InputRoute> _routes = new();

        public string Description => _description;
        public InputContextActivation Activation => _activation;
        public PossessedActorKind PossessedKind => _possessedKind;
        public GameplayTag? Tag => _tag;
        public int Priority => _priority;
        public bool BlocksAllLower => _blocksAllLower;
        public IReadOnlyList<InputContext> Blocks => _blocks;
        public IReadOnlyList<InputRoute> Routes => _routes;

        /// <summary>Every action this context turns on: its typed slots, its extra actions and its routed actions.</summary>
        public IEnumerable<InputActionId> Actions
        {
            get
            {
                foreach (var slot in Slots) if (slot != null) yield return slot;
                foreach (var action in _actions) if (action != null) yield return action;
                foreach (var route in _routes) if (route.Action != null) yield return route.Action;
            }
        }

        /// <summary>The typed actions a subclass exposes to the system that reads it.</summary>
        protected virtual IEnumerable<InputActionId?> Slots => System.Array.Empty<InputActionId?>();

        /// <summary>Builder and test helper.</summary>
        public void Configure(InputContextActivation activation, int priority, PossessedActorKind possessedKind = default,
            GameplayTag? tag = null, bool blocksAllLower = false)
        {
            _activation = activation;
            _priority = priority;
            _possessedKind = possessedKind;
            _tag = tag;
            _blocksAllLower = blocksAllLower;
        }

        /// <summary>Builder and test helper.</summary>
        public void SetContents(IEnumerable<InputContext> blocks, IEnumerable<InputActionId> actions, IEnumerable<InputRoute> routes, string? description = null)
        {
            _blocks = new List<InputContext>(blocks);
            _actions = new List<InputActionId>(actions);
            _routes = new List<InputRoute>(routes);
            if (description != null) _description = description;
        }
    }
}
