#nullable enable
using System;
using TinCan.Core.Domain;

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
                .Hold(1.2f, ActionNames.MoveForward).Wait(1f)
                .Hold(1.2f, ActionNames.MoveBackward).Wait(1f)
                .Hold(0.8f, ActionNames.MoveRight).Wait(1f)
                .Hold(0.8f, ActionNames.MoveLeft).Wait(1f)
                .Tap(ActionNames.Jump, 1.5f)
                .Hold(1f, ActionNames.MoveForward, ActionNames.Sprint).Wait(1f)
                .Hold(1f, ActionNames.MoveBackward, ActionNames.Sprint).Wait(1.5f))
            .Build();

        /// <summary>Host pilot: takes the helm and cruises, turns both ways and brakes, so the deck moves under the client (~58 s).</summary>
        public static readonly BotRoute Pilot = new BotRoute.Builder("Pilot")
            .Wait(6f, "wait for client")
            .Ship(BotShipCommand.Take, 0.5f)
            .Hold(10f, ActionNames.MoveForward)
            .Hold(8f, ActionNames.MoveForward, ActionNames.MoveRight)
            .Hold(8f, ActionNames.MoveForward, ActionNames.MoveLeft)
            .Hold(6f, ActionNames.MoveForward)
            .Hold(6f, ActionNames.MoveBackward)
            .Ship(BotShipCommand.Release, 0.5f)
            .Wait(4f, "coast")
            .Build();

        /// <summary>
        /// Host pilot that tilts the deck: pitches down, back, up and back (pitch is on Jump/Sprint at the helm),
        /// then banks through turns both ways (~40 s). Pair with a client on <see cref="Idle"/> to measure deck
        /// sliding.
        /// </summary>
        public static readonly BotRoute PilotTilt = new BotRoute.Builder("PilotTilt")
            .Wait(6f, "wait for client")
            .Ship(BotShipCommand.Take, 0.5f)
            .Hold(3f, ActionNames.MoveForward)
            .Hold(1f, ActionNames.Jump).Wait(3f, "pitched")
            .Hold(1f, ActionNames.Sprint).Wait(2f, "level")
            .Hold(1f, ActionNames.Sprint).Wait(3f, "pitched")
            .Hold(1f, ActionNames.Jump).Wait(2f, "level")
            .Hold(6f, ActionNames.MoveForward, ActionNames.MoveRight)
            .Hold(6f, ActionNames.MoveForward, ActionNames.MoveLeft)
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
