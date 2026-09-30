#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Features.Stations;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Application Layer, the gunner's own peer, every frame: while the Gunner context is live and this player mans a
    /// cannon, the context's Aim steers the barrel (<see cref="CannonAimProcessor.Steer"/>) instead of the body's look,
    /// which the context silences. The aim starts where the barrel points. It reaches the server as the station aim of
    /// the predicted input (<see cref="IHumanoidInputContributor"/>), and the local barrel shows it at once
    /// (<see cref="CannonShotPresenter"/> reads <see cref="LocalAim"/>).
    /// </summary>
    public sealed class GunnerAimUseCase : ITickable, IHumanoidInputContributor
    {
        private readonly IInputReader _input;
        private readonly GunnerInputContext _controls;
        private readonly IInputContexts _contexts;
        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly CannonConfig _config;
        private readonly CannonAimProcessor _aim;
        private Guid? _manned;
        private Vector2 _current;

        public GunnerAimUseCase(IInputReader input, GunnerInputContext controls, IInputContexts contexts, IActorRegistry actors,
            INetworkService network, CannonConfig config, CannonAimProcessor aim)
        {
            _input = input;
            _controls = controls;
            _contexts = contexts;
            _actors = actors;
            _network = network;
            _config = config;
            _aim = aim;
        }

        /// <summary>The cannon this player aims and its aim (x yaw, y elevation, degrees, base frame); null when not gunning.</summary>
        public (Guid Cannon, Vector2 Aim)? LocalAim => _manned is { } cannon ? (cannon, _current) : null;

        public void Tick()
        {
            var cannon = _contexts.IsActive(_controls) ? MannedCannon() : null;
            if (cannon == null)
            {
                _manned = null;
                return;
            }

            if (_manned != cannon.Id)
            {
                _manned = cannon.Id;
                _current = new Vector2(cannon.Yaw, cannon.Elevation);
            }

            _current = _aim.Steer(_current, _input.ReadVector2(_controls.Aim), _config.AimSensitivity, _config.AimLimits);
        }

        /// <summary>Scenario seam: points the barrel at an exact aim, as if the mouse had moved there.</summary>
        public bool TrySetAim(Vector2 aim)
        {
            if (_manned == null) return false;
            var (yaw, elevation) = _aim.ClampAim(aim, _config.AimLimits);
            _current = new Vector2(yaw, elevation);
            return true;
        }

        public void Contribute(IHumanoidCharacterView character, ref HumanoidInputState input)
        {
            if (_manned != null) input.StationAim = _current;
        }

        private ICannon? MannedCannon()
        {
            foreach (var cannon in _actors.GetActors<ICannon>())
            {
                if (cannon is IStation { OccupantClientId: { } occupant } && occupant == _network.LocalClientId) return cannon;
            }
            return null;
        }
    }
}
