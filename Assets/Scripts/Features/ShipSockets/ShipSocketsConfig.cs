#nullable enable
using TinCan.Core.Interaction;
using UnityEngine;

namespace TinCan.Features.ShipSockets
{
    /// <summary>Socket tunables: what pressing E on a free socket does, and how close a player must be to mount.</summary>
    [CreateAssetMenu(fileName = "ShipSocketsConfig", menuName = "TinCan/Ship/Ship Sockets Config")]
    public class ShipSocketsConfig : ScriptableObject
    {
        [Tooltip("IA_MountFitting: the interaction a free socket offers (MountFittingInteractionHandler).")]
        public InteractionDefinition? MountInteraction;
        [Tooltip("A mount request from further than this (metres from the socket) is refused.")]
        [Min(1f)] public float MaxReach = 4f;
        [Tooltip("The trigger players aim at on a free socket: its size, standing on the socket.")]
        public Vector3 TargetSize = new(1f, 1.8f, 1f);
    }
}
