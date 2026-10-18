Status: Draft; the first step (a cairn part, the spirit's lift as one tuning value, balloons retired) was built with [frame-only-ships.md](frame-only-ships.md) on 2026-10-17

# Spirit cairn: a bound spirit lifts the ship, boat parts steer it

## Why

Balloons do not suit the ships the frame builder makes ([ship-frames.md](ship-frames.md)). They sit over the hull, block
the view, and make a big ship look out of proportion. There is a small and a big one, and each has to be placed by hand.
Airships in Howl's Moving Castle and Final Fantasy IX have no balloons: magic holds them up, and propellers, oars, sails
or wings move them. That suits sailing a cloud sea better: the ship is a boat, and a spirit (a fairy or demon bound in a
cairn) makes its boat parts work in the air.

## The idea

- **The cairn lifts the ship.** It is a station that holds a bound spirit. The spirit's level is how much mass the
  ship can carry; there is no other lift. Lift is a hard limit on how big a ship can be: a design heavier than its
  spirit can lift does not fly (the existing `TooHeavy` rule). That is the progression: upgrade the spirit, build a
  bigger ship.
- **Boat parts steer and drive it.** With the spirit's help, sails, oars, propellers and wings move the ship. They give
  thrust as parts do today; what sets them apart for now is how they look. Later each may bring its own trait to the
  movement:
  - sails: hoisted or furled to set the cruising speed;
  - oars: a speed boost, worked by the crew or by the spirit;
  - wings: help with climbing;
  - propellers: steady thrust.
- **No wind.** Sails do not depend on wind, and there is no wind system. It is hard to get right, and the game does not
  need it now.

## First step (small, fits the look)

This is mostly data, because lift and thrust are already part stats that get summed (`ShipStatsProcessor`).

1. **A cairn part:** a greybox station with some mass and no lift of its own. The lift is the spirit's level, a
   tuning value for now (see "Ship and design"). One cairn per design: a validator rule, like the helm's.
2. **Retire the balloon and envelope parts** from the catalog. The Starter, Galleon, Raft and Frame Galleon designs get a
   cairn instead. The `TooHeavy` message says "the spirit cannot lift ..." instead of "add balloons".
3. **Sails, oars, propellers and wings as greybox thrust parts.** Each has only a thrust value and a look, with no
   traits yet. Existing engines are kept or restyled as propellers.
4. **The shipyard shows the limit:** "mass 38 t of 45 t" against the cairn's lift.

## Later (not planned yet)

- Spirit levels and how a player gains them (the progression loop).
- The traits above: hoisting sails, oar boosts, wings helping a climb.
- Tending the spirit as a crew job.
- A glow on attuned parts.
- The cairn's look, and the spirit's.

## Ship and design

The **ship** is the entity: it keeps what it is and what it has earned while it flies, is damaged and is rebuilt. Its
**design** is how it is built right now, and the ship switches to a new design when its crew changes it in the shipyard.
This is how the code already works: the ship's `ShipDesignNetworkMediator` holds its current design, and the ship is
rebuilt from it when the design changes.

- **The spirit belongs to the ship**, not the design. Its level is ship state, and an upgrade carries over to every
  later design.
- **The design says where the cairn is:** a cairn part placed like any station. The design does not say how strong the
  spirit is.
- **Lift is checked against the ship.** A design is "too heavy" only for a given ship's spirit, so the shipyard checks it
  against the ship being built. The design validator keeps only the rules that do not depend on the ship: exactly one
  cairn, something that gives thrust.
- **First step:** every ship has one fixed spirit level (a tuning value). Storing a level per ship comes with
  progression.
