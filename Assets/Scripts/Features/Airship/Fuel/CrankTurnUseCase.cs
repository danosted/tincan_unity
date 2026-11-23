#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Core.Targeting;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>The input that turns a crank (Input_Primary), from Fuel's installer.</summary>
    public sealed class CrankInput
    {
        public CrankInput(GameplayInput? input) => Input = input;
        public GameplayInput? Input { get; }
    }

    /// <summary>
    /// Server, inside the movement step (where each input arrives once, in order): a press of Primary with empty hands,
    /// looking at a hand crank in Interact reach, is one turn. While the tank is below the crank's reserve
    /// (<see cref="FuelConfig.CrankMaxLevel"/>, from the tank's config) a turn adds
    /// <see cref="FuelConfig.CrankLitresPerTurn"/> (<see cref="CrankProcessor"/>), which restarts a stalled engine; at the
    /// reserve it does nothing.
    /// </summary>
    public sealed class CrankTurnUseCase : IHumanoidMotionContributor
    {
        private const string LogSource = "Fuel";

        private readonly INetworkService _network;
        private readonly ITargetingService _targeting;
        private readonly InteractionTargetingSettings _settings;
        private readonly GameplayInput? _input;
        private readonly IEventPublisher _events;
        private readonly InputPresses _presses = new();

        public CrankTurnUseCase(INetworkService network, ITargetingService targeting, InteractionTargetingSettings settings, CrankInput input,
            IEventPublisher events)
        {
            _network = network;
            _targeting = targeting;
            _settings = settings;
            _input = input.Input;
            _events = events;
        }

        public void Contribute(IHumanoidCharacterView character, in HumanoidInputState input, ref HumanoidMotionStep step)
        {
            if (!_network.IsServer || _input == null) return;
            int bit = _input.BitIndex;
            // Each input once, in order (InputPresses: a replay never presses twice).
            if (!_presses.Pressed(character.Id, input, bit)) return;

            if (EquipmentLocator.Resolve(character) is { IsEmptyHanded: false }) return;
            if (!_targeting.TryAcquire(new HumanoidTargeter(character), _settings.Targeting, InteractionTargetingSettings.OnlyInteractable, out var result) ||
                result.Target is not IFuelCrank crank || crank.Tank is not { Config: not null } tank)
            {
                return;
            }

            Turn(crank, tank);
        }

        /// <summary>One turn of <paramref name="crank"/> into <paramref name="tank"/>: true when it charged anything.</summary>
        public bool Turn(IFuelCrank crank, IFuelTank tank)
        {
            var config = tank.Config;
            if (config == null) return false;
            float litres = CrankProcessor.Charge(tank.Level, tank.Capacity, config.CrankMaxLevel, config.CrankLitresPerTurn);
            if (litres <= 0f)
            {
                _events.LogInfo(LogSource, "The crank only charges a low tank; mine crystals for more.");
                return false;
            }

            tank.Refill(litres);
            crank.ServerTurned();
            return true;
        }
    }
}
