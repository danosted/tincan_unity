#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Ship;

namespace TinCan.Features.SkillChecks.Tests
{
    /// <summary>A skill check host without NGO: records what the server opens and lights, hands out what a test queues.</summary>
    internal sealed class FakeSkillCheckHost : ISkillCheckHost
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public IAirshipView? Ship { get; set; }
        public SkillCheckDefinition? Definition { get; set; }
        public List<(ulong Client, int CheckId, int Level)> Opened { get; } = new();
        public List<(int CheckId, SkillCheckGrade Grade)> Sent { get; } = new();
        public List<SkillCheckResult> Incoming { get; } = new();
        public (int CheckId, int Level)? Pending;
        public string? LitChallenge { get; private set; }
        public int LitSecondsLeft { get; private set; }

        public void ServerOpen(ulong clientId, int checkId, int level) => Opened.Add((clientId, checkId, level));

        public bool TryTakeOpened(out int checkId, out int level)
        {
            (checkId, level) = Pending ?? (0, 0);
            bool had = Pending != null;
            Pending = null;
            return had;
        }

        public void SendGrade(int checkId, SkillCheckGrade grade) => Sent.Add((checkId, grade));

        public IReadOnlyList<SkillCheckResult> TakeResults()
        {
            var taken = Incoming.ToArray();
            Incoming.Clear();
            return taken;
        }

        public void ServerShowLit(string? challenge, int secondsLeft)
        {
            LitChallenge = challenge;
            LitSecondsLeft = challenge != null ? secondsLeft : 0;
        }

        /// <summary>The last check opened here, graded: as if its player sent it.</summary>
        public void Answer(SkillCheckGrade grade)
        {
            var (client, checkId, _) = Opened[^1];
            Incoming.Add(new SkillCheckResult(client, checkId, grade));
        }
    }
}
