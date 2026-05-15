using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Features.Airship
{
    public interface IBuildPlacementRequestor
    {
        void RequestPlacement(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip);
    }
}
