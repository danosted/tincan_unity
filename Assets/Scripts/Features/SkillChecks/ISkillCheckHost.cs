#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Ship;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Something on a ship that runs a skill check (the engine telegraph): its check, and the exchange with the player
    /// doing it. The server opens a check on one player's peer; that peer sends the grade back. While a challenge lights
    /// the station, every peer sees it lit.
    /// </summary>
    public interface ISkillCheckHost : IActor
    {
        IAirshipView? Ship { get; }
        SkillCheckDefinition? Definition { get; }

        /// <summary>Server: open the check on one player's peer (that peer lays it out: the band, the combo), harder by a level.</summary>
        void ServerOpen(ulong clientId, int checkId, int level);

        /// <summary>This peer: the check the server opened for this player (its number and level), once.</summary>
        bool TryTakeOpened(out int checkId, out int level);

        /// <summary>This peer: send the grade to the server.</summary>
        void SendGrade(int checkId, SkillCheckGrade grade);

        /// <summary>Server: the grades received since the last call.</summary>
        IReadOnlyList<SkillCheckResult> TakeResults();

        /// <summary>Server: light the station for a challenge (its title; seconds left to start it, or -1 while someone does it); null puts it out.</summary>
        void ServerShowLit(string? challenge, int secondsLeft);

        /// <summary>Every peer: the challenge lighting this station, or null.</summary>
        string? LitChallenge { get; }

        /// <summary>Every peer: seconds left to start it, or -1 while someone does it.</summary>
        int LitSecondsLeft { get; }
    }
}
