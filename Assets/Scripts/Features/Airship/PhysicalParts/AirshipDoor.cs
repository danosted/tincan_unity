using System.Collections;
using TinCan.Core.Domain.Abilities.Tags;
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


    // The Handle method is only invokes from inside the RequestInteractionServerRpc method, which is only called on the server. So this code will only run on the server.
    public class DoorInteractionHandler : IInteractionHandler
    {
        private readonly GameplayTag _handlerTag;
        public DoorInteractionHandler(GameplayTag handlerTag)
        {
            _handlerTag = handlerTag;
        }
        public GameplayTag Tag => _handlerTag;

        public void Handle(InteractionContext context)
        {
            if(context.Target is AirshipDoor door)
            {
                door.ServerToggle();
            }
        }
    }
}