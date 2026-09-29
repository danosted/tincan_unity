#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Presentation Layer: sits on the net's held-visual prefab (the catching net item's <c>HeldVisual</c>) and swings
    /// it while its holder carries the swinging tag. The equipment mediator instantiates the prefab under the player's
    /// Visual, so the holder's ability controller is the nearest one above. Tags replicate to every peer, so this runs
    /// unchanged on clients.
    /// </summary>
    public class NetSwingVisualView : MonoBehaviour
    {
        [SerializeField] private GameplayTag? _swingingTag;
        [SerializeField] private float _swingAngle = 70f;
        [SerializeField] private float _speed = 14f;

        private Quaternion _restRotation = Quaternion.identity;
        private IAbilityControllerBase? _controller;

        private void Awake()
        {
            _restRotation = transform.localRotation;
            _controller = GetComponentInParent<IAbilityControllerBase>();
        }

        private void Update()
        {
            if (_controller == null || _swingingTag == null) return;

            bool swinging = _controller.HasTag(_swingingTag);
            var target = swinging ? _restRotation * Quaternion.Euler(-_swingAngle, 0f, 0f) : _restRotation;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Mathf.Clamp01(_speed * Time.deltaTime));
        }
    }
}
