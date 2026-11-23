using System.Collections;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.PhysicalParts
{
    /// <summary>A ship's door: Interact opens or shuts it, through core's ToggleInteractionHandler (IA_ToggleDoor).</summary>
    public class AirshipDoor : NetworkBehaviour, IInteractionTarget, IToggleable
    {
        [SerializeField] private InteractionDefinition _interactionDefinition;
        [SerializeField] private Transform _hinge;
        [SerializeField] private float _openAngle = 110f;
        [SerializeField] private float _speed = 4f;

        private readonly NetworkVariable<bool> _isOpen = new(
            false,
            NetworkVariableReadPermission.Everyone, //Everyone will know if the door is open
            NetworkVariableWritePermission.Server //Only the server can tell players clients to open the door
            );

        public bool ServerToggle()
        {
            if (!IsServer) return _isOpen.Value;
            _isOpen.Value = !_isOpen.Value;
            return _isOpen.Value;
        }

        public InteractionDefinition Definition => _interactionDefinition;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            var target = Quaternion.Euler(0, _isOpen.Value ? _openAngle : 0, 0);
            _hinge.localRotation = Quaternion.Slerp(_hinge.localRotation, target, Time.deltaTime * _speed);
        }
    }
}
