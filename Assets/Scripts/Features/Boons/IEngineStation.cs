#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.SkillChecks;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// An engine station (the engine telegraph, the boiler): press E on it to do its skill check. Keeps the handler
    /// testable without NGO.
    /// </summary>
    public interface IEngineStation : IInteractable
    {
        /// <summary>The check it runs (the host on the same part).</summary>
        ISkillCheckHost? Check { get; }
    }
}
