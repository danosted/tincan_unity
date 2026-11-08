#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>
    /// A kind of gate: what it looks like, how close the ship must come, and what passing through does (its actions, run
    /// once on the server; then the gate is used up). A boon gate is a definition whose action applies a GAS effect; a
    /// gate that opens a skill check or warps the ship would be another action.
    /// </summary>
    [CreateAssetMenu(fileName = "Gate_", menuName = "TinCan/Gates/Gate Definition")]
    public class GateDefinition : ScriptableObject
    {
        [Tooltip("Networked prefab: NetworkObject, EntityNetworkMediator, GateNetworkMediator, a child named Glow (hidden once used).")]
        public GameObject? Prefab;

        [Tooltip("The ship passes through when its solid body comes this close to the gate's middle (metres).")]
        [Min(0.5f)] public float Radius = 10f;

        [Tooltip("What passing through does, in order (server).")]
        [SerializeReference] public List<GateAction> Actions = new();
    }
}
