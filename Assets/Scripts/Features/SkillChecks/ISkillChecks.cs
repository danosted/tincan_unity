#nullable enable
using TinCan.Core.Domain.Abilities;

namespace TinCan.Features.SkillChecks
{
    /// <summary>Server: opens skill checks and keeps their score.</summary>
    public interface ISkillChecks
    {
        /// <summary>
        /// Opens the host's check for this player; false (and a log line) while one is open or the host rests. Its grade's
        /// effect goes on <paramref name="target"/> when given (a broken part's repair), otherwise on the host's ship.
        /// </summary>
        bool TryOpen(ISkillCheckHost host, ulong clientId, IAbilityControllerBase? target = null);

        /// <summary>Checks that ended with this grade since the host started.</summary>
        int Count(SkillCheckGrade grade);
    }
}
