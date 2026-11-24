#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Settings;
using VContainer.Unity;

namespace TinCan.Core.Settings
{
    /// <summary>
    /// Each frame, the camera the local player sees through (their body's, the ship's, an occupied station's) draws as
    /// far as the player's view distance (<see cref="ViewDistance"/>). Without the setting loaded it draws at Normal.
    /// </summary>
    public sealed class ViewDistancePresenter : ITickable
    {
        private readonly ILocalViewCamera _view;
        private readonly IPlayerSettings _settings;

        public ViewDistancePresenter(ILocalViewCamera view, IPlayerSettings settings)
        {
            _view = view;
            _settings = settings;
        }

        public void Tick()
        {
            var camera = _view.Camera;
            if (camera == null) return;

            float metres = ViewDistance.Metres(_settings.Has(ViewDistance.Key) ? _settings.GetString(ViewDistance.Key) : null);
            if (camera.farClipPlane != metres) camera.farClipPlane = metres;
        }
    }
}
