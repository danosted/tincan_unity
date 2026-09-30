#nullable enable
using System;

namespace TinCan.DevTools
{
    /// <summary>
    /// The named routes the bot can play. Moves are paired (forward then back) so the player ends near where it
    /// started and never walks off the deck. Routes are code so they are versioned and reviewable with the harness.
    /// </summary>
    public static class BotRoutes
    {
        /// <summary>Client test subject: starts, stops, jumps and sprints on the deck, three times over (~50 s).</summary>
        public static readonly BotRoute DeckWalk = new BotRoute.Builder("DeckWalk")
            .Wait(4f, "settle")
            .Repeat(3, cycle => cycle
                .Hold(1.2f, ScriptedAction.MoveForward).Wait(1f)
                .Hold(1.2f, ScriptedAction.MoveBackward).Wait(1f)
                .Hold(0.8f, ScriptedAction.MoveRight).Wait(1f)
                .Hold(0.8f, ScriptedAction.MoveLeft).Wait(1f)
                .Tap(ScriptedAction.Jump, 1.5f)
                .Hold(1f, ScriptedAction.MoveForward, ScriptedAction.Sprint).Wait(1f)
                .Hold(1f, ScriptedAction.MoveBackward, ScriptedAction.Sprint).Wait(1.5f))
            .Build();

        /// <summary>Host pilot: takes the helm and cruises, turns both ways and brakes, so the deck moves under the client (~58 s).</summary>
        public static readonly BotRoute Pilot = new BotRoute.Builder("Pilot")
            .Wait(6f, "wait for client")
            .Ship(BotShipCommand.Take, 0.5f)
            .Hold(10f, ScriptedAction.ShipThrottleUp)
            .Hold(8f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnRight)
            .Hold(8f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnLeft)
            .Hold(6f, ScriptedAction.ShipThrottleUp)
            .Hold(6f, ScriptedAction.ShipThrottleDown)
            .Ship(BotShipCommand.Release, 0.5f)
            .Wait(4f, "coast")
            .Build();

        /// <summary>
        /// Host pilot that tilts the deck: pitches up, back, down and back, then banks through turns both ways (~40 s).
        /// Pair with a client on <see cref="Idle"/> to measure deck sliding.
        /// </summary>
        public static readonly BotRoute PilotTilt = new BotRoute.Builder("PilotTilt")
            .Wait(6f, "wait for client")
            .Ship(BotShipCommand.Take, 0.5f)
            .Hold(3f, ScriptedAction.ShipThrottleUp)
            .Hold(1f, ScriptedAction.ShipPitchUp).Wait(3f, "pitched")
            .Hold(1f, ScriptedAction.ShipPitchDown).Wait(2f, "level")
            .Hold(1f, ScriptedAction.ShipPitchDown).Wait(3f, "pitched")
            .Hold(1f, ScriptedAction.ShipPitchUp).Wait(2f, "level")
            .Hold(6f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnRight)
            .Hold(6f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnLeft)
            .Ship(BotShipCommand.Release, 0.5f)
            .Wait(3f, "coast")
            .Build();

        /// <summary>Stands still; measures idle drift (deck sliding) and snaps.</summary>
        public static readonly BotRoute Idle = new BotRoute.Builder("Idle").Wait(45f, "idle").Build();

        private static readonly BotRoute[] All = { DeckWalk, Pilot, PilotTilt, Idle };

        public static bool TryGet(string? name, out BotRoute route)
        {
            foreach (var candidate in All)
            {
                if (!string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                route = candidate;
                return true;
            }

            route = Idle;
            return false;
        }

        public static string Names => string.Join(", ", Array.ConvertAll(All, r => r.Name));
    }
}
