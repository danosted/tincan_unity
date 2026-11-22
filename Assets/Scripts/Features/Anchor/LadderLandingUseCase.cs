#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// Server, after movement: a player who has climbed a dropped anchor's ladder to the top is set down on deck, past the
    /// rail, keeping what they carry. Apart from <see cref="LadderClimbUseCase"/>, which runs inside movement and so cannot
    /// use the respawn service (the movement use case itself).
    /// </summary>
    public sealed class LadderLandingUseCase : ISimulationTickable
    {
        private const string LogSource = "Anchor";

        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly IHumanoidRespawnService _respawn;
        private readonly IEventPublisher _events;
        private readonly List<IHumanoidCharacterView> _players = new();

        public LadderLandingUseCase(IActorRegistry actors, INetworkService network, IHumanoidRespawnService respawn, IEventPublisher events)
        {
            _actors = actors;
            _network = network;
            _respawn = respawn;
            _events = events;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            _players.Clear();
            _players.AddRange(_actors.GetActors<IHumanoidCharacterView>().Where(player => player.IsSimulating));
            foreach (var anchor in _actors.GetActors<IAnchor>())
            {
                if (!anchor.IsSimulating || !anchor.IsDown || anchor.Ladder is not { } ladder) continue;

                foreach (var player in _players)
                {
                    if (!LadderProcessor.HasClimbed(player.Movement.Transform.position, ladder)) continue;

                    _respawn.ResetCharacter(player, ladder.DeckPosition, ladder.DeckRotation);
                    _events.LogInfo(LogSource, "Climbed the ladder: aboard.");
                }
            }
        }
    }
}
