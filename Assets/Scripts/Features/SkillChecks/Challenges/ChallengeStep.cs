#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>
    /// One step of a <see cref="SkillChallengeDefinition"/>: a station's check, or a way of putting steps together.
    /// Plain data; <see cref="SkillChallengeRun"/> plays it. Plan: hazards-and-boons.md (B4a).
    /// </summary>
    [Serializable]
    public abstract class ChallengeStep
    {
    }

    /// <summary>A station lights up; whoever presses E on it in time does its check.</summary>
    [Serializable]
    public sealed class StationStep : ChallengeStep
    {
        [Tooltip("The station, by the check it runs (SC_EngineBoost is the engine telegraph). Skipped if the ship has none.")]
        public SkillCheckDefinition? Station;
    }

    /// <summary>The steps one after another.</summary>
    [Serializable]
    public sealed class SequenceStep : ChallengeStep
    {
        [SerializeReference] public List<ChallengeStep> Steps = new();
    }

    /// <summary>The steps one after another, each done by a different player than the step before (alone: the same one).</summary>
    [Serializable]
    public sealed class RelayStep : ChallengeStep
    {
        [SerializeReference] public List<ChallengeStep> Steps = new();
    }

    /// <summary>The steps all at once: several stations lit together.</summary>
    [Serializable]
    public sealed class ParallelStep : ChallengeStep
    {
        [SerializeReference] public List<ChallengeStep> Steps = new();
    }

    /// <summary>One step, then the next by its grade (none: the branch ends with the first step's grade).</summary>
    [Serializable]
    public sealed class BranchStep : ChallengeStep
    {
        [SerializeReference] public ChallengeStep? First;
        [SerializeReference] public ChallengeStep? OnPerfect;
        [SerializeReference] public ChallengeStep? OnGood;
        [SerializeReference] public ChallengeStep? OnMiss;
    }

    /// <summary>
    /// The same station round after round, harder each time, until a Miss or <see cref="PerfectAt"/> rounds. Later
    /// rounds open straight away for the player who did the last one. No round passed is a Miss, all of them a Perfect,
    /// any between a Good.
    /// </summary>
    [Serializable]
    public sealed class EscalateStep : ChallengeStep
    {
        public SkillCheckDefinition? Station;
        [Tooltip("Rounds to pass for a Perfect; the step ends there.")]
        [Range(2, 8)] public int PerfectAt = 3;
    }
}
