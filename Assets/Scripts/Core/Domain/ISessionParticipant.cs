#nullable enable
namespace TinCan.Core.Domain
{
    /// <summary>
    /// Server: a feature that takes part in a play session (a voyage). The session calls every registered participant;
    /// it knows none of them. Register an implementation with <c>.As&lt;ISessionParticipant&gt;()</c>. A scene without a
    /// session never calls these, so features keep their default behaviour there.
    /// </summary>
    public interface ISessionParticipant
    {
        /// <summary>A new session starts: restore the start state (a full tank, a whole hull, a clear sky).</summary>
        void ResetForSession();

        /// <summary>The session's pressure starts (underway) or stops (briefing, ended): hazards, breakage.</summary>
        void SetSessionActive(bool active);
    }
}
