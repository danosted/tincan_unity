#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.TargetOutline
{
    /// <summary>
    /// Presentation, local player only, after every frame's updates: outlines what Interact would act on. The target is
    /// the one the owner's prompt shows (<see cref="IInteractorView.CurrentTarget"/>, the same query the server runs), and
    /// only while Interact can be pressed (the Humanoid context is live: not at a station, not in a menu). Its own meshes
    /// join the outline rendering layer; a target without one shows nothing (make it a proper fixture instead).
    /// </summary>
    public sealed class TargetOutlinePresenter : ILateTickable
    {
        private readonly IActorRegistry _actors;
        private readonly IInputContexts _contexts;
        private readonly HumanoidInputContext _onFoot;
        private readonly TargetOutlineConfig _config;
        private readonly List<Renderer> _outlined = new();
        private Component? _target;

        public TargetOutlinePresenter(IActorRegistry actors, IInputContexts contexts, HumanoidInputContext onFoot, TargetOutlineConfig config)
        {
            _actors = actors;
            _contexts = contexts;
            _onFoot = onFoot;
            _config = config;
        }

        /// <summary>The target outlined now; null when none.</summary>
        public Component? Target => _target;

        public void LateTick() => Show(_contexts.IsActive(_onFoot) ? LocalTarget() : null);

        /// <summary>Outlines <paramref name="target"/>'s own meshes, and stops outlining the previous target.</summary>
        public void Show(Component? target)
        {
            if (target == null) target = null; // a destroyed target is no target
            if (ReferenceEquals(target, _target)) return;

            uint bit = _config.LayerBit;
            foreach (var renderer in _outlined)
            {
                if (renderer != null) renderer.renderingLayerMask &= ~bit;
            }
            _outlined.Clear();

            _target = target;
            if (target != null) OwnRenderers(target, _outlined);
            foreach (var renderer in _outlined) renderer.renderingLayerMask |= bit;
            _config.Highlighted = _outlined.Count;
        }

        /// <summary>
        /// The target's own meshes: mesh and skinned renderers in its hierarchy that belong to it, not to a nested target
        /// (a fixture on a ship is its own target). Lines, particles and the like are left out.
        /// </summary>
        public static void OwnRenderers(Component target, List<Renderer> into)
        {
            var owner = target.GetComponentInParent<ITargetable>();
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                if (!ReferenceEquals(renderer.GetComponentInParent<ITargetable>(), owner)) continue;
                into.Add(renderer);
            }
        }

        private Component? LocalTarget()
        {
            var player = _actors.GetLocalPlayerActor<IHumanoidCharacterView>() as Component;
            if (player == null) return null;
            var interactor = player.GetComponentInChildren<IInteractorView>();
            return interactor?.CurrentTarget as Component;
        }
    }
}
