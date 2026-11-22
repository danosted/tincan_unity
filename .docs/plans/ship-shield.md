Status: Draft (2026-11-22)

# Ship shield: the spirit's ward, which holds back only so much, and the crew keeps it up

## Why
In the storm ([storm-crossing.md](storm-crossing.md)), debris and other obstacles hit the ship while part of the crew
cures the heart. The ship's spirit can ward it, but the ward can only hold back so much. Keeping it up is a crew job,
so the storm has work for everyone, not just the gunners.

## Decisions (developer, 2026-11-22)
- The ship has a shield that takes the hits first, and it can only hold back so much.
- **The shield is the ship's spirit**, not a separate generator. Having a shield at all, and how much it holds, are
  talents in the spirit's tree ([spirit-talents.md](spirit-talents.md), the Ward branch). A ship whose spirit has not
  learned Ward has no shield.
- The crew keeps it up at the spirit's cairn in two ways:
  - **Fusing** stockpiled minerals into the spirit gives the ward a buffer.
  - **Channelling** the spirit by hand keeps it up.
- It is shown on the spirit as well as around the ship (`spirit-talents.md`: progression is visual).

## Today (verified 2026-11-22)
- The ship has health (1000 on the Galleon), and hits take it down through effects: `GE_CannonballHit`,
  `GE_HazardImpact`, `GE_IslandImpact`, `GE_GasPocketExplosion`.
- Breakable parts, repair (a skill check per pull) and the hull-breach leak are in `Airship/Damage/`.
- The cairn is a ship part with the spirit's lift as one tuning value (`ShipStatsTuning.SpiritLift`,
  `spirit-cairn.md`). Stations (`Stations/`) seat a player and grant abilities.
- There is no shield, and no store of crystals aboard.

## Shape
- **Shield points** (feature `Shield`, new assembly `TinCan.Features.Shield`):
  - they are a ship attribute, `Shield`, with a cap, `ShieldMax`, which is 0 until the spirit learns Ward;
  - hits that would hurt the ship take shield first, and only what is left over reaches health and the parts;
  - how the hit effects are split is to be checked in the GAS layer: one damage effect that both read, or a pre-step
    that moves the hit onto the shield;
  - it does not refill by itself inside a zone (open question 1), which is what makes it a job.

  Shield never references the spirit: the Ward talents are effects that raise `ShieldMax`, so the dependency runs one
  way.
- **At the cairn** (the spirit's feature, `spirit-talents.md`):
  - **Fuse** (anyone, anytime): carry a crystal from the bin to the cairn and hold Interact for a moment (the
    hold-to-work interaction from `fuel-gathering.md`). The spirit takes it in and the ward gains `FuseBuffer` points
    at once.
  - **Channel** (one player, seated): the cairn is a station. Occupy it and hold Primary, and while held the ward
    refills at `ChannelRate` per second. The channeller can do nothing else, and letting go stops the refill.
- **The stockpile** is a crystal bin aboard, a socket fitting (`stores.crystal_bin`) holding up to `BinSize` crystals.
  It feeds the engine and the ward. Check first whether the generic item rack (`ItemRackNetworkMediator`) can take
  items back in; then the bin is a rack prefab, not new code.
- **Read it at a glance:**
  - the ward is the spirit's light, spread over the ship as a soft bubble that flickers where it is hit and thins as
    it drains (pale cyan, pillar 5);
  - the spirit's Ward rings dim as the ward drains and flare when a crystal is fused;
  - and a bar on the HUD.
- **Out of zones** the ward sits full. Nothing hits it in the peaceful world.

## Slices
- **SH1 Shield points.** The attribute, `ShieldMax` 0 by default, hits taking shield first, the bubble and the HUD
  bar. Tested with a test effect that sets `ShieldMax`, so it does not wait on the talent tree.
- **SH2 Fuse and channel at the cairn.** The cairn as a station, the fuse interaction and the channel. Needs
  `spirit-talents.md` T3 for a real `ShieldMax`.
- **SH3 The crystal bin.** The stockpile fitting, mounted on the Galleon by the engine and the cairn.

## Tests
EditMode: the hit split (all shield, part shield and part health, no shield, `ShieldMax` 0), fusing (adds the
buffer, refuses at the cap, refuses other items, refuses with no Ward), channelling (refills while held, stops on
release, one channeller), and the bin (take, put, full, empty). Scenario `ShieldHold` in a test-range scene, solo and
host + client: with Ward learned, a hazard hits the ship and the ward takes it; fuse a crystal and it rises; channel
and it refills; with no ward left, a hit reaches health.

## Open questions (for the developer)
1. **Refill on its own:** never, or a slow trickle? Recommended: never inside a zone, and back to full once the ship
   is out of it, so the peaceful world never asks you to top it up.
2. **Fusing past the cap:** can a fused crystal push the ward over `ShieldMax` as an overcharge that bleeds off (a real
   "buffer"), or does it only refill up to the cap?
3. **One stockpile for everything:** the engine, the ward and maybe the ray cannons all spend the same crystals
   (`ray-cannons.md`, question 1). That makes mining matter, and makes the storm a test of how well you stocked up.
   Agree?
