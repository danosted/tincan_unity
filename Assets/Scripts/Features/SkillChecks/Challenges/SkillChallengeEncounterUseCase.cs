#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>
    /// Application Layer, server only: encounters. While the voyage is underway, a random challenge from
    /// <see cref="SkillChallengesConfig"/> starts every interval (the first after the first delay); if one still runs,
    /// that turn is skipped. A new session drops the running challenge. Plan: hazards-and-boons.md (B4a).
    /// </summary>
    public sealed class SkillChallengeEncounterUseCase : ISimulationTickable, ISessionParticipant
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private readonly INetworkService _network;
        private readonly ITimeService _time;
        private readonly ISkillChallenges _challenges;
        private readonly SkillChallengesConfig _config;
        private readonly System.Random _random = new();
        private bool _active;
        private float _nextAt;

        public SkillChallengeEncounterUseCase(INetworkService network, ITimeService time, ISkillChallenges challenges, SkillChallengesConfig config)
        {
            _network = network;
            _time = time;
            _challenges = challenges;
            _config = config;
        }

        public void ResetForSession()
        {
            _active = false;
            if (_network.IsServer) _challenges.Cancel();
        }

        public void SetSessionActive(bool active)
        {
            if (active && !_active) _nextAt = _time.Time + _config.FirstDelay;
            _active = active;
        }

        public void Tick()
        {
            if (!_network.IsServer || !_active || _config.Challenges.Count == 0 || _time.Time < _nextAt) return;

            _nextAt = _time.Time + _config.Interval;
            if (_challenges.Running != null) return;

            var challenge = _config.Challenges[_random.Next(_config.Challenges.Count)];
            if (challenge != null) _challenges.TryStart(challenge);
        }
    }
}
