Status: Draft (2026-11-22)

# The spirit's talents: a tree the ship's spirit grows through, seen on the spirit itself

## Why
The ship's spirit leads progression ([peaceful-mode.md](peaceful-mode.md)): it lifts the ship today
([spirit-cairn.md](spirit-cairn.md)), and it should grow into what makes the ship more than a hull. The developer
chose a talent tree (2026-11-22), and its first branch gives the ship its shield ([ship-shield.md](ship-shield.md)). The
spirit's growth should show on the spirit itself: you can tell what a ship's spirit has learned by looking at it.

## Decisions (developer, 2026-11-22)
- **The spirit grows through a talent tree.**
- **The shield is the spirit's.** Whether a ship has a shield at all, and how much it holds, are talents in the
  spirit's tree.
- **Progression is visual.** Every talent changes how the spirit looks, and that matters as much as the numbers.
- **For starters, talents unlock by themselves as the spirit levels up** (developer, 2026-11-22): each level unlocks
  the next talent in a fixed order. There are no points to spend and no choices yet; those can come once the tree
  has been played.

## Shape
- **The spirit is ship state** (as `spirit-cairn.md` says), not the design's: its level and talents carry over every
  rebuild, and they are saved with the world (peaceful-mode P2).
- **Its level** is one replicated number on the ship. What raises it is kept simple, and picked in play (a dev
  command until then).
- **A talent** is data, in the upgrade shape `peaceful-mode.md` describes:
  - effects on the ship's attributes;
  - granted abilities;
  - the tags it gives (`Spirit.Ward`);
  - and ranks.

  The GAS layer applies and removes it as equipment does today.
- **The first branch is Ward:**
  - *Ward*: the ship has a shield. It raises `ShieldMax` above 0.
  - *Deep ward* (ranks): more `ShieldMax` per rank.

  Other branches come once Ward has been played.
- **Unlocking:** the server applies the talents for the spirit's level, in the tree's order. A level-up is a moment:
  a cue at the cairn, and the spirit's new mark appearing.
- **The spirit's look grows with it**, in soft light (`ART_DIRECTION.md` pillar 5) and slow motion (pillar 4):
  - each branch adds its own mark (Ward: rings of pale cyan light turning about it, one per rank);
  - its light is what the talents do in the world (the shield bubble is the Ward rings' light, spread over the ship).

  The look reads from the replicated talents, so every peer sees the same spirit.

## Open: a companion (discussed 2026-11-22, nothing decided)
A companion was explored with the developer in another session; no option was picked. Recorded here because the
recommended one is this spirit:
- **A. The ship's spirit as the companion** (recommended): one per ship, living in the cairn, server-driven
  (`ShipSpiritNetworkMediator`). It drifts between anchor points on deck and reacts to gameplay cues and designed
  events (damage, gas, boons, low fuel) with short barks. Its level is the ship's progression; its look is spirit cyan
  (`M_ShipSpirit`). It extends `spirit-cairn.md`'s "Later" items (tending the spirit, its look).
- **B. A pet per player** that follows its player: needs pathing on a moving deck, and has no tie to the ship.
- **C. An AI crewmate** that fills empty crew slots and works stations: needs task AI, which is costly. It could grow
  out of A later ("the spirit works the oars").

Questions for the developer: what it is for (mood, guiding, gameplay help, progression); one per ship or one per
player; does it act on the world or only react; and what character it is (fire demon, fairy, old sea spirit). Once
answered, it gets its own plan (`companion.md`), checked against `encounters.md`, `fuel-gathering.md` and
`storm-crossing.md` for hooks.

## Slices
- **T1 The spirit as state.** Level and talents on the ship, replicated, and a placeholder spirit in the cairn.
- **T2 The tree.** Talent data in a fixed order, unlocked by level, and the level-up cue.
- **T3 Ward.** Ward and Deep ward raising `ShieldMax` (`ship-shield.md`), and the Ward rings on the spirit.
- **T4 The look pass.** The spirit's form, glow and motion by branch, reviewed in a lab per `ART_DIRECTION.md`.

## Tests
EditMode: the talents for a level, applying and removing their effects, ranks, and the spirit surviving a rebuild.
Scenario `SpiritWard` in a test-range scene, solo and host + client: level the spirit, Ward unlocks, and the ship has a
shield and the rings show on both peers.
