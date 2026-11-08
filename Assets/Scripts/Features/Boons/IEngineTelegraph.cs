#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.SkillChecks;

namespace TinCan.Features.Boons
{
    /// <summary>The engine telegraph: press E on it to do its skill check. Keeps the handler testable without NGO.</summary>
    public interface IEngineTelegraph : IInteractable
    {
        /// <summary>The check it runs (the host on the same part).</summary>
        ISkillCheckHost? Check { get; }
    }
}
