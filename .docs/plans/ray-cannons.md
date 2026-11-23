Status: Approved (2026-11-23: R1 and R2 built)

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
- **What it does is up to the target.** The beam applies one effect, `GE_SpiritBeamHit`, to a collider target, and a
  target that is no collider answers it through `ISpiritBeamTargets` (as built in R2; a `Beam.Hit` tag was not needed):
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
- **R1 The beam.** The ray in place of the arc, the charge and cooldown, `GE_SpiritBeamHit`, misfire
  kept, and its look and cue. `CannonShot` and `CannonMisfire` follow; `GalleonSternCannon` is unchanged.
- **R2 Targets answer the beam.** A hazard (debris) breaks on the beam's hit; targets that are no collider answer it. The storm's heart targets come with the
  storm (`storm-crossing.md`, ST5).

## Progress
- **2026-11-23, R1 the beam (built; EditMode 1702/1702; `CannonShot` and `CannonMisfire` pass solo and host + client, 2026-11-23).**
  - **Defaults taken** (developer, 2026-11-23: R1 before the storm's heart, `storm-crossing.md` ST5): the beam is
    free, with a charge and a cooldown and no overheat (open question 1, kept simple). The code keeps `Cannon` (open
    question 2).
  - **The shot:** the press starts a 0.4 s charge (`CannonConfig.ChargeSeconds`) and the ability's cooldown
    (`GE_CannonReload`, 1.5 s, the charge included). Then the beam fires along the barrel's aim at that moment, so
    the gunner can still turn it while it charges. It is one sweep from the muzzle out to `BeamRange` (400 m), and
    it ends on the first target or solid thing. `BallisticArc`, `CannonballProcessor` and the inherited ship
    velocity are gone.
  - **Two events per shot** reach every peer, `Charging` and `Fired` (with its origin and end). The presenter
    grows a glow at this peer's muzzle and plays `cue_spirit_beam` (one clip: the hum over the charge, then the
    release). It then draws the beam from this peer's muzzle to the server's end, pale cyan to warm gold, thinning out
    over 0.35 s, with a bloom where it hit something (`M_SpiritBeam`, URP Particles/Unlit, additive). The gunner's
    aiming arc became a straight line.
  - **Hits:** `GE_CannonballHit` is `GE_SpiritBeamHit` and `TD_CannonballSweep` is `TD_SpiritBeamSweep`,
    renamed in place by the builder (the GUIDs stay). It is the same damage, so hazards still break and a misfire
    still hurts the ship. `Beam.Hit` is not made yet: effects have no tag of their own besides the granted tags
    and cues, so how a target answers the beam is R2's to design.
  - **Scenarios:** the `BallsShown` probe is `BeamsShown`, and `SpawnTargetOnArc` is `SpawnTargetOnBeam` (on
    the barrel's straight line).
  - **Next:** your eye on the beam, its glows and its sound in Play (the scenario captures come after the beam has
    faded, so they don't show it). Then R2, and with it how a target answers `Beam.Hit`.

- **2026-11-23, R2 targets answer the beam (built with `storm-crossing.md` ST5; EditMode 1714/1714).**
  - **No `Beam.Hit` tag.** A target answers the beam in one of two ways, and neither needs a change to the cannon:
    - **A collider target** answers through the hit effect on its controller. A hazard (debris) has health, so
      `GE_SpiritBeamHit` breaks it, as before (`CannonShot`).
    - **A target that is no collider** answers through `ISpiritBeamTargets`, which its feature registers (the
      flight-forces pattern). When a cannon fires, it asks every source how far along the beam its first target is.
      The nearest answer ends the beam there, if it is nearer than anything solid, and that source answers the hit:
      the storm's heart targets (`StormCureUseCase`). The beam is drawn to that point, so its bloom shows the hit.
  - EditMode: a beam target nearer than anything solid answers and ends the beam, and one behind something solid is
    not hit.

## Tests
EditMode: the beam sweep (hit, miss, out of range, own ship), the charge and cooldown, and each target's answer to
the beam. Scenarios `CannonShot` (a hazard breaks on the beam) and `CannonMisfire` (the beam into our own deck),
solo and host + client, in their test-range scenes.

## Open questions (for the developer)
1. **Does the beam cost energy?** Options:
   - Free, with a cooldown and an overheat when fired too fast. Recommended to start.
   - It draws from the same crystals as the shield and the engine, so firing, shielding and sailing compete for one
     stockpile.
2. **A name:** for example "spirit lance" or "ray cannon". The station and the gunner's role get the same name.
3. **Other uses to plan for:** a tractor beam that pulls things in, lighting beacons from afar, or cutting crystal
   from veins at range?
