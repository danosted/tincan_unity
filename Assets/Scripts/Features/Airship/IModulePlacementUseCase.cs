using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Features.Airship
{
    public interface IModulePlacementUseCase
    {
        void RequestPlacement(GameObject modulePrefab, Vector3 worldPosition, Quaternion worldRotation, IActor targetShip);
    }
}
