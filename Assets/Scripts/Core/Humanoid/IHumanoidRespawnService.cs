#nullable enable
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    public interface IHumanoidRespawnService
    {
        void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation);
    }
}
