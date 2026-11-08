#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// A skill check (the pulsing gauge): how long it lasts, how fast and narrow it is, how long its host rests after,
    /// and what each grade gives the ship. A check finished while the ship carries <see cref="BonusTag"/> (a gate's boost
    /// window) also gives <see cref="BonusEffect"/> on a Good or Perfect.
    /// </summary>
    [CreateAssetMenu(fileName = "SC_", menuName = "TinCan/Skill Checks/Skill Check")]
    public class SkillCheckDefinition : ScriptableObject
    {
        [Tooltip("Shown over the gauge.")]
        public string Title = "Press in the band";

        [Header("The gauge")]
        [Tooltip("Seconds to press before the check is a Miss.")]
        [Min(0.5f)] public float Seconds = 4f;
        [Tooltip("Seconds for the needle to sweep there and back.")]
        [Min(0.2f)] public float Period = 1.6f;
        [Tooltip("Width of the Good part of the band (0 to 1 of the gauge).")]
        [Range(0.02f, 1f)] public float GoodWidth = 0.25f;
        [Tooltip("Width of the Perfect part, in the band's middle.")]
        [Range(0.01f, 1f)] public float PerfectWidth = 0.1f;

        [Header("After")]
        [Tooltip("Seconds the host rests after a check, whatever its grade, before anyone can start it again.")]
        [Min(0f)] public float Cooldown = 20f;

        [Header("What it gives the ship")]
        public GameplayEffectDefinition? GoodEffect;
        public GameplayEffectDefinition? PerfectEffect;
        [Tooltip("While the ship carries this tag, a Good or Perfect also gives the bonus effect (State.Ship.BoostWindow).")]
        public GameplayTag? BonusTag;
        public GameplayEffectDefinition? BonusEffect;

        public GameplayEffectDefinition? EffectFor(SkillCheckGrade grade) => grade switch
        {
            SkillCheckGrade.Perfect => PerfectEffect,
            SkillCheckGrade.Good => GoodEffect,
            _ => null
        };
    }
}
