#nullable enable
using TinCan.Core.Interaction;
using TinCan.Features.SkillChecks;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Presentation/Infrastructure Layer: the engine telegraph on deck (a fixture on the old hull, the
    /// boon.engine_telegraph fitting on designed ships). Interacting routes to <see cref="StartEngineBoostInteractionHandler"/>
    /// via IA_EngineBoost. Its check is the <see cref="SkillCheckHostNetworkMediator"/> beside it.
    /// </summary>
    public class EngineTelegraphNetworkMediator : NetworkBehaviour, IInteractionTarget, IEngineTelegraph
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;

        private ISkillCheckHost? _check;

        public InteractionDefinition Definition => _interactionDefinition!;

        public ISkillCheckHost? Check => _check ??= GetComponent<ISkillCheckHost>();
    }
}
