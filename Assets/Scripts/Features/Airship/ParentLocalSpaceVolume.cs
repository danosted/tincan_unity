using UnityEngine;

namespace TinCan.Features.Airship
{
    [RequireComponent(typeof(BoxCollider))]
    public class ParentLocalSpaceVolume : MonoBehaviour
    {
        private BoxCollider _volume = null!;

        private void Awake()
        {
            _volume = GetComponent<BoxCollider>();
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition) - _volume.center;
            Vector3 halfSize = _volume.size * 0.5f;

            return Mathf.Abs(localPosition.x) <= halfSize.x &&
                   Mathf.Abs(localPosition.y) <= halfSize.y &&
                   Mathf.Abs(localPosition.z) <= halfSize.z;
        }
    }
}
