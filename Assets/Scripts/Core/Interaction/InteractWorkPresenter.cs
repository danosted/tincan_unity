#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Humanoid;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// Local player: a HUD meter that fills while Interact is held on a target whose interaction is work
    /// (<see cref="InteractionDefinition.HoldSeconds"/> above 0), from the same input and target the server times
    /// (<see cref="InteractInputUseCase"/>). It empties when the key is let go or the target changes, as the server's
    /// work does. Presentation only.
    /// </summary>
    public sealed class InteractWorkPresenter : ITickable
    {
        public const string MeterKey = "Working";

        private readonly IActorRegistry _actors;
        private readonly InteractionTargetingSettings _settings;
        private readonly IHudValues? _hud; // null where no HUD is loaded: nothing to show
        private IInteractable? _target;
        private float _held;
        private bool _shown;

        public InteractWorkPresenter(IActorRegistry actors, InteractionTargetingSettings settings, IHudValues? hud)
        {
            _actors = actors;
            _settings = settings;
            _hud = hud;
        }

        [Inject]
        public InteractWorkPresenter(IActorRegistry actors, InteractionTargetingSettings settings, IObjectResolver resolver)
            : this(actors, settings, resolver.TryResolve<IHudValues>(out var hud) ? hud : null)
        {
        }

        public void Tick() => Step(LocalWork(out var player), player != null && IsHeld(player), Time.deltaTime);

        /// <summary>One frame: the work target in reach (or null), whether Interact is held, and the frame's time.</summary>
        public void Step(IInteractionTarget? target, bool held, float deltaTime)
        {
            float seconds = target?.Definition != null ? target.Definition.HoldSeconds : 0f;
            if (!held || target == null || seconds <= 0f || (!ReferenceEquals(target, _target) && _held > 0f))
            {
                // A new target starts over; the next held frame begins timing it.
                _target = held ? target : null;
                _held = 0f;
                Hide();
                return;
            }

            _target = target;
            _held += deltaTime;
            if (_held >= seconds)
            {
                // Done, as the server's work is: hidden until the key is let go, not started again.
                Hide();
                return;
            }

            _hud?.SetMeter(MeterKey, Mathf.Clamp01(_held / seconds));
            _shown = true;
        }

        /// <summary>Whether the meter is on the HUD now.</summary>
        public bool Showing => _shown;

        private void Hide()
        {
            if (!_shown) return;
            _hud?.RemoveMeter(MeterKey);
            _shown = false;
        }

        private IInteractionTarget? LocalWork(out IHumanoidCharacterView? player)
        {
            player = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            if (player is not Component component || component == null) return null;
            return component.GetComponentInChildren<IInteractorView>()?.CurrentTarget as IInteractionTarget;
        }

        private bool IsHeld(IHumanoidCharacterView player)
        {
            int bit = _settings.Input.BitIndex;
            return bit >= 0 && (player.InputState.ActiveInputMask & (1UL << bit)) != 0;
        }
    }
}
