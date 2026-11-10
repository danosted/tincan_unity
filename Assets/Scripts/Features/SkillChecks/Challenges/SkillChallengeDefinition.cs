#nullable enable
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>How a challenge's grade comes from its steps' grades (and a combinator's from its children's).</summary>
    public enum ChallengeGradeRule
    {
        /// <summary>The worst step's grade.</summary>
        Worst = 0,

        /// <summary>The steps' average, rounded half up (Perfect and Miss make a Good).</summary>
        Average = 1
    }

    /// <summary>
    /// A challenge: steps that light up the ship's stations (<see cref="Root"/>, a tree of <see cref="ChallengeStep"/>s),
    /// how long a lit station waits to be started, how the steps' grades make the challenge's, and what that grade gives
    /// the ship. Started by an encounter (<see cref="SkillChallengesConfig"/>). Plan: hazards-and-boons.md (B4a).
    /// </summary>
    [CreateAssetMenu(fileName = "CH_", menuName = "TinCan/Skill Checks/Skill Challenge")]
    public class SkillChallengeDefinition : ScriptableObject
    {
        [Tooltip("Shown on the HUD while it runs.")]
        public string Title = "Challenge";
        [Tooltip("Seconds a lit station waits for someone to start it; then the step is a Miss.")]
        [Min(3f)] public float StepSeconds = 25f;
        public ChallengeGradeRule GradeRule = ChallengeGradeRule.Worst;
        [SerializeReference] public ChallengeStep? Root;

        [Header("What it gives the ship")]
        [Tooltip("Put on the ship when it starts: an instant effect whose cue tells the crew (GE_Challenge_Start).")]
        public GameplayEffectDefinition? StartEffect;
        public GameplayEffectDefinition? GoodEffect;
        public GameplayEffectDefinition? PerfectEffect;

        public GameplayEffectDefinition? EffectFor(SkillCheckGrade grade) => grade switch
        {
            SkillCheckGrade.Perfect => PerfectEffect,
            SkillCheckGrade.Good => GoodEffect,
            _ => null
        };
    }
}
