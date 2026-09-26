#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace TinCan.Core.Domain.Features
{
    /// <summary>
    /// One asset per feature, discovered from <c>Resources/Installers</c>. It owns the feature's config references,
    /// registers the feature's services in the container, and lists the networked prefabs the feature spawns at
    /// runtime. Adding a feature therefore touches no shared file: no field on ProjectLifetimeScope, no line in the
    /// network prefab list, no child in the airship prefab.
    /// </summary>
    public abstract class FeatureInstaller : ScriptableObject
    {
        /// <summary>Lower runs first; ties break on asset name. Use only when a feature depends on another's registrations.</summary>
        public virtual int Order => 0;

        /// <summary>Register services, use cases, handlers and commands. Runs during container configuration.</summary>
        public abstract void Install(IContainerBuilder builder);

        /// <summary>Prefabs with a NetworkObject that this feature instantiates itself; registered with NGO and the DI interceptor.</summary>
        public virtual IEnumerable<GameObject> NetworkedPrefabs => Enumerable.Empty<GameObject>();

        /// <summary>Hook after the container is built, for wiring that needs resolved services.</summary>
        public virtual void OnContainerBuilt(IObjectResolver container) { }

        /// <summary>
        /// Opt-in capability for an installer that contributes a domain-specific payload beyond the universal
        /// members above (e.g. <see cref="ShipFixtureDefinition"/>). The owning system's own catalog (e.g. a
        /// ship-fixture catalog) discovers contributors with
        /// <c>Installers.OfType&lt;FeatureInstaller.IExtension&lt;TContribution&gt;&gt;()</c> instead of every
        /// installer inheriting an unrelated virtual member.
        /// </summary>
        public interface IExtension<out TContribution>
        {
            IEnumerable<TContribution> Contributions { get; }
        }
    }
}
