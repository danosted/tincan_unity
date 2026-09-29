#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Networking;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// Application Layer: routes a burst cue. <see cref="GameplayCueDispatchProcessor"/> decides; this plays it on this
    /// peer through <see cref="IGameplayCuePlayer"/> and sends it through the target's <see cref="IGameplayCueRelay"/>.
    /// Outside a session everything is local, so it just plays.
    /// </summary>
    public sealed class GameplayCueDispatcher : IGameplayCueDispatcher
    {
        private readonly INetworkService _network;
        private readonly GameplayCueDispatchProcessor _processor;
        private readonly IGameplayCuePlayer _player;

        public GameplayCueDispatcher(INetworkService network, GameplayCueDispatchProcessor processor, IGameplayCuePlayer player)
        {
            _network = network;
            _processor = processor;
            _player = player;
        }

        public void Execute(GameplayTag cue, IAbilityControllerBase target, GameplayEffectContext context)
        {
            if (cue == null || target == null) return;

            if (!_network.IsActive)
            {
                _player.Play(cue, target);
                return;
            }

            var relay = target as IGameplayCueRelay;
            var dispatch = _processor.Decide(_network.IsServer, _network.IsClient, relay is { IsOwnedLocally: true }, context);
            if (dispatch.PlayLocally) _player.Play(cue, target);
            if (dispatch.SendToClients) relay?.RelayCue(cue, dispatch.ExcludeOwner);
        }
    }
}
