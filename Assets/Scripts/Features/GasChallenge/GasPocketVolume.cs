#nullable enable
using System;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// A gas pocket: a sphere that detonates the first time a ship touches it (<see cref="GasChallengeUseCase"/>). Placed by
    /// hand in a scene (server-local), or spawned networked by a gas field, where <see cref="GasPocketNetworkMediator"/>
    /// carries the detonation to every peer.
    /// </summary>
    public class GasPocketVolume : MonoBehaviour
    {
        public const string VisualChildName = "GasPocketVisuals";

        [SerializeField] private float _radius = 3f;
        [SerializeField] private GameplayEffectDefinition? _explosionEffect;
        [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        [SerializeField] private SphereCollider _triggerCollider;

        public Vector3 Center => transform.position;
        public float Radius => _radius;
        public GameplayEffectDefinition? ExplosionEffect => _explosionEffect;
        public bool HasDetonated { get; private set; }
        public SphereCollider TriggerCollider => _triggerCollider;

        /// <summary>Raised once, when the pocket detonates (the networked pocket replicates it).</summary>
        public event Action<GasPocketVolume>? Detonated;

        public void MarkDetonated()
        {
            if (HasDetonated) return;
            HasDetonated = true;
            Detonated?.Invoke(this);
        }

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

        private void OnDrawGizmos()
        {
            var radius = _triggerCollider != null ? _triggerCollider.radius : _radius;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawSphere(transform.position + (_triggerCollider != null ? _triggerCollider.center : Vector3.zero), radius);
        }
    }
}
