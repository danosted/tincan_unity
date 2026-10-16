#nullable enable
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Inside the movement step, server only: a leashed player hangs on their rope (<see cref="LeashProcessor"/>), dragged
    /// along with the ship. Holding forward or jump climbs (when the rules allow it), a reeling winch shortens the rope too,
    /// and reaching the point marks them arrived for <see cref="OverboardUseCase"/> to land on deck. It is also the rule that
    /// makes a leashed body server-driven: the owner sees the ship at another pose, so it would mispredict the rope.
    /// </summary>
    public sealed class LeashRopeUseCase : IHumanoidMotionContributor, IServerDrivenHumanoidRule
    {
        private readonly RespawnRules _rules;
        private readonly LeashStore _leashes;
        private readonly INetworkService _network;

        public LeashRopeUseCase(RespawnRules rules, LeashStore leashes, INetworkService network)
        {
            _rules = rules;
            _leashes = leashes;
            _network = network;
        }

        public bool IsServerDriven(IHumanoidCharacterView character) =>
            _rules.LeashedTag != null && character.HasTag(_rules.LeashedTag);

        public void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step)
        {
            if (!_network.IsServer || step.IsReplay || !_leashes.TryGet(character.Id, out var leash)) return;
            if (leash.Point.Anchor == null) return;

            bool climbing = input.MovementDirection.z > 0.5f || input.IsJumping;
            leash.Length = LeashProcessor.Shorten(leash.Length, climbing, _leashes.IsWinching(leash.Point), _rules, step.DeltaTime);

            var position = character.Movement.Transform.position;
            var anchor = leash.Point.Anchor.position;
            if (LeashProcessor.HasArrived(position, anchor, leash.Length, _rules.ArriveDistance))
            {
                leash.Arrived = true;
                step.Velocity = default;
                return;
            }

            step.Velocity = LeashProcessor.Constrain(position, step.Velocity, anchor, leash.Length, _rules.MaxDragSpeed, step.DeltaTime);
        }
    }
}
