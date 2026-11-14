#nullable enable
namespace TinCan.Core.Domain
{
    /// <summary>
    /// Every peer: how far along the current play session (a voyage) is, from the feature that runs it (Voyage). Read by
    /// features that pace themselves by the session without depending on it (the day cycle's voyage driver).
    /// Presentation-grade: each peer works it out from replicated state.
    /// </summary>
    public interface ISessionProgress
    {
        /// <summary>False when no session is under way (none begun, or idle); otherwise 0 at the start, 1 at the end.</summary>
        bool TryGetProgress(out float progress);
    }
}
