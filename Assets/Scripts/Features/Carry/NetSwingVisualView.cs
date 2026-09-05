#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Carry
{
    /// <summary>
    /// Presentation Layer: swings the carried net visual while the player carries the swinging tag.
    /// Tags replicate to every peer, so this runs unchanged on clients.
    /// </summary>
    public class NetSwingVisualView : MonoBehaviour
    {
        private const string NetVisualName = "Carry_Net";

        [SerializeField] private GameplayTag? _swingingTag;
        [SerializeField] private float _swingAngle = 70f;
        [SerializeField] private float _speed = 14f;

        private Transform? _net;
        private Quaternion _restRotation = Quaternion.identity;
        private IAbilityControllerBase? _controller;

        private void Awake()
        {
            _net = transform.Find(NetVisualName);
            if (_net != null) _restRotation = _net.localRotation;
            _controller = GetComponent<IAbilityControllerBase>();
        }

        private void Update()
        {
            if (_net == null || _controller == null || _swingingTag == null) return;

            bool swinging = _controller.HasTag(_swingingTag);
            var target = swinging ? _restRotation * Quaternion.Euler(-_swingAngle, 0f, 0f) : _restRotation;
            _net.localRotation = Quaternion.Slerp(_net.localRotation, target, Mathf.Clamp01(_speed * Time.deltaTime));
        }
    }
}
