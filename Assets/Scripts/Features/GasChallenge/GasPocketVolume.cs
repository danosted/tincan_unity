#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    public class GasPocketVolume : MonoBehaviour
    {
        [SerializeField] private float _radius = 3f;
        [SerializeField] private float _damagePerSecond = 10f;
        [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.35f);

        public Vector3 Center => transform.position;
        public float Radius => _radius;
        public float DamagePerSecond => _damagePerSecond;

        public GasPocketVolumeDefinition ToDefinition()
        {
            return new GasPocketVolumeDefinition(transform.position, _radius, _damagePerSecond);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _gizmoColor;
            Gizmos.DrawSphere(transform.position, _radius);
        }
    }
}
