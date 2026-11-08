#nullable enable
using TinCan.Core.Interaction;
using TinCan.Features.SkillChecks;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Presentation/Infrastructure Layer: an engine station on deck, a fixture on the old hull and a fitting on designed
    /// ships: the engine telegraph (boon.engine_telegraph, the pulsing gauge) and the boiler (boon.boiler, the button
    /// combo). Interacting routes to <see cref="StartEngineBoostInteractionHandler"/> via IA_EngineBoost. Its check is the
    /// <see cref="SkillCheckHostNetworkMediator"/> beside it, so which minigame it plays is that check's data.
    /// </summary>
    public class EngineStationNetworkMediator : NetworkBehaviour, IInteractionTarget, IEngineStation
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;

        private ISkillCheckHost? _check;

        public InteractionDefinition Definition => _interactionDefinition!;

        public ISkillCheckHost? Check => _check ??= GetComponent<ISkillCheckHost>();
    }
}
