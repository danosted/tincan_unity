#nullable enable
using TinCan.Core.Domain;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Presentation, every frame, on the local player's peer: brightens the marker of the broken part the repair tool
    /// points at (<see cref="ShipRepairUseCase.AimedTarget"/>), so the player sees what a press would repair before
    /// pulling the trigger. The marker gets its own colour back when the aim moves on or the part is repaired.
    /// </summary>
    public sealed class RepairAimHighlightPresenter : ITickable
    {
        private const string MarkerName = "Marker";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly ShipRepairUseCase _repair;
        private readonly ShipDamageConfig _config;
        private readonly MaterialPropertyBlock _block = new();
        private Transform? _marker;
        private Renderer? _renderer;

        public RepairAimHighlightPresenter(ShipRepairUseCase repair, ShipDamageConfig config)
        {
            _repair = repair;
            _config = config;
        }

        public void Tick()
        {
            var aimed = _repair.AimedTarget?.Transform;
            var marker = aimed != null ? aimed.FindDescendant(MarkerName) : null;
            if (marker == _marker) return;

            Release();
            Take(marker);
        }

        private void Take(Transform? marker)
        {
            _marker = marker;
            _renderer = _marker != null ? _marker.GetComponent<Renderer>() : null;
            var material = _renderer != null ? _renderer.sharedMaterial : null;
            if (_renderer == null || material == null) return;

            int colorId = material.HasProperty(BaseColorId) ? BaseColorId : ColorId;
            Color own = material.HasProperty(colorId) ? material.GetColor(colorId) : Color.white;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(colorId, Color.Lerp(own, Color.white, _config.AimHighlightBrightness));
            _renderer.SetPropertyBlock(_block);
        }

        private void Release()
        {
            if (_renderer != null) _renderer.SetPropertyBlock(null);
            _marker = null;
            _renderer = null;
        }
    }
}
