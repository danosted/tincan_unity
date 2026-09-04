#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    public class GasPocketVolume : MonoBehaviour
    {
        private const string VisualChildName = "GasPocketVisuals";

        [SerializeField] private float _radius = 3f;
        [SerializeField] private float _damagePerSecond = 10f;
        [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        [SerializeField] private SphereCollider _triggerCollider;

        public Vector3 Center => transform.position;
        public float Radius => _radius;
        public float DamagePerSecond => _damagePerSecond;
        public SphereCollider TriggerCollider => _triggerCollider;

        private void Reset()
        {
            EnsureTriggerCollider();
            SyncVisualScale();
        }

        private void Awake()
        {
            EnsureTriggerCollider();
            SyncVisualScale();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
            SyncVisualScale();
        }

        public void SetTriggerCollider(SphereCollider triggerCollider)
        {
            _triggerCollider = triggerCollider;
            if (_triggerCollider == null)
            {
                return;
            }

            _triggerCollider.isTrigger = true;
            _triggerCollider.radius = _radius;
            _triggerCollider.center = Vector3.zero;
            SyncVisualScale();
        }

        public void EnsureTriggerCollider()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<SphereCollider>();
            }

            if (_triggerCollider == null)
            {
                _triggerCollider = gameObject.AddComponent<SphereCollider>();
            }

            SetTriggerCollider(_triggerCollider);
        }

        public void SetRadius(float radius)
        {
            _radius = radius;
            if (_triggerCollider != null)
            {
                _triggerCollider.radius = _radius;
            }

            SyncVisualScale();
        }

        private void SyncVisualScale()
        {
            if (_triggerCollider == null)
            {
                return;
            }

            var visual = transform.Find(VisualChildName);
            if (visual == null)
            {
                return;
            }

            visual.localPosition = _triggerCollider.center;
            visual.localScale = Vector3.one * (_triggerCollider.radius * 2f);
        }

        public bool ContainsCollider(Collider shipCollider)
        {
            if (shipCollider == null)
            {
                return false;
            }

            var pocketCenter = transform.TransformPoint(_triggerCollider != null ? _triggerCollider.center : Vector3.zero);
            var closestPoint = shipCollider.ClosestPoint(pocketCenter);
            var distanceToClosestPoint = Vector3.Distance(closestPoint, pocketCenter);
            return distanceToClosestPoint <= _radius;
        }

        public GasPocketVolumeDefinition ToDefinition()
        {
            return new GasPocketVolumeDefinition(transform.position, _radius, _damagePerSecond);
        }

        private void OnDrawGizmos()
        {
            var radius = _triggerCollider != null ? _triggerCollider.radius : _radius;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawSphere(transform.position + (_triggerCollider != null ? _triggerCollider.center : Vector3.zero), radius);
        }
    }
}
