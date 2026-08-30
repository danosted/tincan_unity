using System.Collections;
using TinCan.Features.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace Assets.Scripts.Features.Airship
{
    public class AirshipDoor : NetworkBehaviour, IInteractionTarget
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

        public void ServerToggle()
        {
            if(!IsServer) return;
            _isOpen.Value = !_isOpen.Value;
        }

        public InteractionDefinition Definition => throw new System.NotImplementedException();

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