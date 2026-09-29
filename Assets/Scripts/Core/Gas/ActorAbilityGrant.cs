#nullable enable
using System;

namespace TinCan.Core.Gas
{
    /// <summary>The kinds of actor a feature can hand starting abilities to.</summary>
    public enum ActorKind { Humanoid, Airship }

    /// <summary>
    /// An ability a feature gives every actor of one kind when it registers. This is the player's (and ship's) socket
    /// for abilities: a feature installer contributes grants through <c>FeatureInstaller.IExtension&lt;ActorAbilityGrant&gt;</c>
    /// and <see cref="ActorAbilityGrantUseCase"/> grants them, so the ability only exists in scenes whose profile loads
    /// the feature that makes it work. A prefab's own starting list is for core abilities (sprint).
    /// </summary>
    [Serializable]
    public sealed class ActorAbilityGrant
    {
        public ActorKind Actor;
        public AbilityDefinition? Ability;
    }
}
