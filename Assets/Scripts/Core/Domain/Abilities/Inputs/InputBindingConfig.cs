using System;
using System.Collections.Generic;
using UnityEngine;
using TinCan.Core.Domain.Abilities.Inputs;

namespace TinCan.Core.Domain.Abilities
{
    [Serializable]
    public struct InputBinding
    {
        public GameplayInput InputType;
        public string UnityActionName;
    }

    [CreateAssetMenu(fileName = "InputBindingConfig", menuName = "TinCan/Abilities/Inputs/Binding Config")]
    public class InputBindingConfig : ScriptableObject
    {
        public List<InputBinding> Bindings;
    }
}
