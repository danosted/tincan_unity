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
    /// Local player: a HUD line saying what Interact does on the target in reach ("Interact: Mine crystal"), from its
    /// interaction's <see cref="InteractionDefinition.Prompt"/> or the target's own <see cref="IInteractionPrompt"/>.
    /// Gone when nothing is in reach or the target has no prompt. Presentation only.
    /// </summary>
    public sealed class InteractPromptPresenter : ITickable
    {
        public const string Key = "Interact";

        private readonly IActorRegistry _actors;
        private readonly IHudValues? _hud; // null where no HUD is loaded: nothing to show
        private string _shown = string.Empty;

        public InteractPromptPresenter(IActorRegistry actors, IHudValues? hud)
        {
            _actors = actors;
            _hud = hud;
        }

        [Inject]
        public InteractPromptPresenter(IActorRegistry actors, IObjectResolver resolver)
            : this(actors, resolver.TryResolve<IHudValues>(out var hud) ? hud : null)
        {
        }

        public void Tick() => Step(LocalTarget());

        /// <summary>One frame: the target in reach, or null.</summary>
        public void Step(IInteractionTarget? target)
        {
            string text = PromptOf(target);
            if (text == _shown) return;

            _shown = text;
            if (text.Length == 0) _hud?.Remove(Key);
            else _hud?.Set(Key, text);
        }

        /// <summary>What the HUD line says now; empty when it is not shown.</summary>
        public string Shown => _shown;

        private static string PromptOf(IInteractionTarget? target)
        {
            if (target == null) return string.Empty;
            if (target is IInteractionPrompt own && !string.IsNullOrEmpty(own.Prompt)) return own.Prompt;
            return target.Definition != null ? target.Definition.Prompt : string.Empty;
        }

        private IInteractionTarget? LocalTarget()
        {
            var player = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            if (player is not Component component || component == null) return null;
            return component.GetComponentInChildren<IInteractorView>()?.CurrentTarget as IInteractionTarget;
        }
    }
}
