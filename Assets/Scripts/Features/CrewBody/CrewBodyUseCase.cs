#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.CrewBody
{
    /// <summary>
    /// Use case: gives every humanoid the crew body as it registers, on every peer. The body goes under the humanoid's
    /// visual root (so it is drawn where the smoothing draws it, and first-person rigs hide it with the rest of the body)
    /// with its feet at the bottom of the CharacterController, and the placeholder capsule there is switched off.
    /// Without this feature the capsule stays. Plan: crew-animation.md (S2).
    /// </summary>
    public sealed class CrewBodyUseCase : IInitializable, IDisposable
    {
        /// <summary>Feet below the root when the humanoid has no CharacterController: half the 2 m player.</summary>
        private const float DefaultFeetHeight = -1f;

        private readonly IActorRegistry _actors;
        private readonly CrewBodyView _prefab;

        public CrewBodyUseCase(IActorRegistry actors, CrewBodyView prefab)
        {
            _actors = actors;
            _prefab = prefab;
        }

        public void Initialize()
        {
            _actors.OnActorRegistered += Attach;
            foreach (var actor in _actors.AllActors.ToList()) Attach(actor);
        }

        public void Dispose() => _actors.OnActorRegistered -= Attach;

        public void Attach(IActor actor)
        {
            if (actor is not IHumanoidActor || actor is not Component component) return;
            if (!component.TryGetComponent<IHumanoidVisualAnchor>(out var anchor) || anchor.VisualRoot is not { } root) return;
            if (root.GetComponentInChildren<CrewBodyView>(true) != null) return;

            foreach (var capsule in root.GetComponents<Renderer>()) capsule.enabled = false;

            var body = Object.Instantiate(_prefab, root, false);
            body.name = _prefab.name;
            float feet = component.TryGetComponent<CharacterController>(out var controller)
                ? controller.center.y - controller.height * 0.5f
                : DefaultFeetHeight;
            float run = 0f, sprint = 0f;
            if (component.TryGetComponent<IHumanoidMovementView>(out var movement))
            {
                run = movement.WalkSpeed;
                sprint = movement.WalkSpeed * movement.SprintMultiplier;
            }
            body.Attach(component.transform, feet, run, sprint);

            // Possession can come first: the local player's camera rig then took the body before it was here.
            if (component.TryGetComponent<LookView>(out var look)) look.RefreshBody();
        }
    }
}
