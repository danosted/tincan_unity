#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Tests.Fakes
{
    /// <summary>
    /// Builds targetable children under an object, for tests of how targetables are found and registered. The plain
    /// class matches the file name so the MonoBehaviour below can be added with AddComponent (see FakeShipDamage.cs).
    /// </summary>
    public static class FakeTargetables
    {
        public static FakeTargetableBehaviour[] AttachTo(GameObject parent, int count)
        {
            var targetables = new FakeTargetableBehaviour[count];
            for (int i = 0; i < count; i++)
            {
                var child = new GameObject($"Targetable_{i}");
                child.transform.SetParent(parent.transform, false);
                targetables[i] = child.AddComponent<FakeTargetableBehaviour>();
            }
            return targetables;
        }
    }

    public class FakeTargetableBehaviour : MonoBehaviour, ITargetable
    {
        public Vector3 AimPoint => transform.position;
        public bool IsTargetable => true;
        public IAbilityControllerBase? Controller => null;
    }
}
