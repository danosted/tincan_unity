#nullable enable
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    public interface IHumanoidRespawnService
    {
        void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation);
    }
}
