#nullable enable

using UnityEngine;
using TinCan.Core.Domain.Abilities.Attributes;

namespace TinCan.Features.Abilities
{
    [CreateAssetMenu(fileName = "MaxHealth", menuName = "TinCan/Abilities/Attributes/MaxHealth")]
    public class MaxHealthAttribute : GameplayAttribute { }
}
