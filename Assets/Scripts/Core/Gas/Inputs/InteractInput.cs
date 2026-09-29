using UnityEngine;
using TinCan.Core.Domain.Abilities.Inputs;

namespace TinCan.Core.Gas.Inputs
{
    /// <summary>The Interact key as a predicted input bit: the server interacts at the tick the press was simulated.</summary>
    [CreateAssetMenu(fileName = "Input_Interact", menuName = "TinCan/Abilities/Inputs/Interact")]
    public class InteractInput : GameplayInput { }
}
