#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>
    /// Encounters: while a voyage is underway, the server starts one of <see cref="Challenges"/> at random every
    /// <see cref="Interval"/> seconds (the first after <see cref="FirstDelay"/>), unless one still runs.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillChallengesConfig", menuName = "TinCan/Skill Checks/Skill Challenges Config")]
    public class SkillChallengesConfig : ScriptableObject
    {
        [Tooltip("Seconds into the voyage before the first challenge.")]
        [Min(1f)] public float FirstDelay = 60f;
        [Tooltip("Seconds between challenges.")]
        [Min(5f)] public float Interval = 120f;
        public List<SkillChallengeDefinition> Challenges = new();
    }
}
