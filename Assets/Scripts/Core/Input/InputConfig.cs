#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan.Core.Input
{
    /// <summary>
    /// The root of the input data (Assets/Input/InputConfig): the actions asset with the default bindings, every action
    /// id, every context, and the ability inputs in bit order. Built by TinCan > Dev > Input > Build Assets.
    /// Model: .docs/INPUT.md.
    /// </summary>
    [CreateAssetMenu(fileName = "InputConfig", menuName = "TinCan/Input/Input Config")]
    public sealed class InputConfig : ScriptableObject
    {
        [Tooltip("Assets/Input/TinCanControls.inputactions: the actions and their default bindings.")]
        public InputActionAsset? Actions;

        [Tooltip("One per action in the asset.")]
        public List<InputActionId> ActionIds = new();

        [Tooltip("Every context the game evaluates, including the ones features read.")]
        public List<InputContext> Contexts = new();

        [Tooltip("Ability inputs in bit order: an input's index here is its bit in the predicted input state, on every peer.")]
        public List<GameplayInput> GameplayInputs = new();
    }
}
