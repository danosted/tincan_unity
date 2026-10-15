#nullable enable
using System.Collections.Generic;
using TinCan.Core.Interaction;
using UnityEngine;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Socket tunables: what pressing E on a free socket does, how close a player must be to mount, and the loadouts the
    /// built-in designs come with.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipSocketsConfig", menuName = "TinCan/Ship/Ship Sockets Config")]
    public class ShipSocketsConfig : ScriptableObject
    {
        [Tooltip("IA_MountFitting: the interaction a free socket offers (MountFittingInteractionHandler).")]
        public InteractionDefinition? MountInteraction;
        [Tooltip("A mount request from further than this (metres from the socket) is refused.")]
        [Min(1f)] public float MaxReach = 4f;
        [Tooltip("The trigger players aim at on a free socket: its size, standing on the socket.")]
        public Vector3 TargetSize = new(1f, 1.8f, 1f);

        [Header("Loadouts")]
        [Tooltip("Read-only loadouts of the built-in designs (<design key>.fittings.json TextAssets under Assets/Settings/ShipSockets/).")]
        public List<TextAsset> BuiltInLoadouts = new();
    }
}
