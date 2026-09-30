#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// What a discrete action means in a context: "go back in the menu", "leave the vehicle". Each command is its own
    /// subclass, so exactly one <see cref="InputCommandHandler{TCommand}"/> can claim it by type. Contexts route actions
    /// to command assets (<see cref="InputRoute"/>); nothing polls a key for these.
    /// </summary>
    public abstract class InputCommand : ScriptableObject
    {
        [TextArea, SerializeField] private string _description = string.Empty;

        public string Description => _description;
    }
}
