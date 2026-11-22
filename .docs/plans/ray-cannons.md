Status: Draft (2026-11-22)

# Ray cannons: the ship's guns fire spirit energy, and what it does depends on what it hits

## Why
The cannons stop being cannons (developer, 2026-11-22). They become ray shooters of magical energy, which can be used
for a variety of things: shooting down debris, and, first of all, curing a raging storm's heart by hitting the
targets that appear around it ([storm-crossing.md](storm-crossing.md)). A beam of soft light also fits the look better
than iron balls ("Magic is soft light", `ART_DIRECTION.md` pillar 5).

## Decisions (developer, 2026-11-22)
- The cannons become ray shooters of magical energy.
- **Many uses, one beam.** Its first uses are curing the storm's heart at its targets and breaking debris. More come
  later.

## Today (verified 2026-11-22)
- `Weapons/Cannon/`: a station (`Stations/`) with a turret that turns fully (`CannonAimLimits.TurnsFully`), aimed
  through the Gunner input context (`GunnerAimUseCase`, `HumanoidInputState.StationAim`). It fires on Primary
  (`GA_FireCannon`).
- A shot is a ball on a ballistic arc (`BallisticArc`, `CannonballProcessor`). The server sweeps each tick's segment
  (`ITargetingService.TryAcquireSegment`) and applies `GE_CannonballHit`. A hit on the cannon's own ship is a misfire
  and hurts it the same.
- `CannonShotPresenter` draws the barrels, the aiming arc and cosmetic balls on every peer. `GCN_Hit` blinks whatever
  is hit and plays `cue_bang`.
- Scenarios: `CannonShot`, `CannonMisfire`, `GalleonSternCannon`.

## Shape
- **The beam** is a ray, not a ball. A shot is one sweep along the aim, out to `BeamRange`, on the tick it is fired,
  so there is no arc and no travel time to lead. `BallisticArc` and `CannonballProcessor` go.
- **What it does is up to the target.** The beam applies one effect, `GE_SpiritBeamHit`, carrying the tag `Beam.Hit`,
  and whatever it hits answers it with its own ability or effect:
  - a heart target counts a hit toward the cure;
  - debris breaks;
  - the ship's own hull still takes a misfire.

  So a new use of the beam is a new kind of target, never a change to the cannon.
- **Firing rhythm:** a short charge on Primary, then a cooldown, so shots are aimed, not sprayed. Whether the beam
  also costs energy is open question 1.
- **The look:** a beam of pale cyan and warm gold with a soft falloff, a glow at the muzzle, a bloom where it hits,
  and a hum. No hard neon. Its cue replaces the cannonball's bang.
- **Names:** the station, feature and assets get a new name (open question 2). Until then the code keeps `Cannon`, so
  the change stays small.

## Slices
- **R1 The beam.** The ray in place of the arc, the charge and cooldown, `GE_SpiritBeamHit` and `Beam.Hit`, misfire
  kept, and its look and cue. `CannonShot` and `CannonMisfire` follow; `GalleonSternCannon` is unchanged.
- **R2 Targets answer the beam.** A hazard (debris) breaks on `Beam.Hit`. The storm's heart targets come with the
  storm (`storm-crossing.md`, ST4).

## Tests
EditMode: the beam sweep (hit, miss, out of range, own ship), the charge and cooldown, and each target's answer to
`Beam.Hit`. Scenarios `CannonShot` (a hazard breaks on the beam) and `CannonMisfire` (the beam into our own deck),
solo and host + client, in their test-range scenes.

## Open questions (for the developer)
1. **Does the beam cost energy?** Options:
   - Free, with a cooldown and an overheat when fired too fast. Recommended to start.
   - It draws from the same crystals as the shield and the engine, so firing, shielding and sailing compete for one
     stockpile.
2. **A name:** for example "spirit lance" or "ray cannon". The station and the gunner's role get the same name.
3. **Other uses to plan for:** a tractor beam that pulls things in, lighting beacons from afar, or cutting crystal
   from veins at range?
