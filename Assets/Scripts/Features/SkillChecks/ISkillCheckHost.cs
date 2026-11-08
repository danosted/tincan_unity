#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Ship;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Something on a ship that runs a skill check (the engine telegraph): its check, and the exchange with the player
    /// doing it. The server opens a check on one player's peer; that peer sends the grade back.
    /// </summary>
    public interface ISkillCheckHost : IActor
    {
        IAirshipView? Ship { get; }
        SkillCheckDefinition? Definition { get; }

        /// <summary>Server: open the check on one player's peer (that peer lays it out: the band, the combo).</summary>
        void ServerOpen(ulong clientId, int checkId);

        /// <summary>This peer: the number of the check the server opened for this player, once.</summary>
        bool TryTakeOpened(out int checkId);

        /// <summary>This peer: send the grade to the server.</summary>
        void SendGrade(int checkId, SkillCheckGrade grade);

        /// <summary>Server: the grades received since the last call.</summary>
        IReadOnlyList<SkillCheckResult> TakeResults();
    }
}
