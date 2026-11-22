Status: Draft (2026-11-22)

# The storm: the first encounter zone, cured at its heart, and weather made of cloud, light and sound

## Why
The first encounter zone ([encounters.md](encounters.md)) is a storm. You sail into it, reach its heart, and cure it
with the ship's ray cannons while the rest of the crew keeps the shield up, and the sky clears. It is also a vertical
slice: the first time clouds, light and sound work together to make a place in the world feel different, with
gameplay on top. What it teaches becomes the weather in [cloud-sea.md](cloud-sea.md) (phase 5, "Weather as tide").

## Decisions (developer, 2026-11-22)
- **The run lives in the world.** It is a zone event, not a voyage: no new layout and no scene change.
- **No prompt.** Sailing into the storm wall sets off the warning cue; turning back before it ends means nothing
  starts (`encounters.md`).
- **It is a vertical slice of the visual systems:** clouds, lighting and sound, judged together.
- **Lab first.** The look and the sound are tuned in a lab before the storm goes in the world.
- **Lightning is mood** in this slice. It doesn't damage anything.
- **Gusts push the ship.** Steering through the storm is part of the challenge.
- **The storm has a heart that has to be cured**, and beyond the storm is open sky, not a place.
- **The cure is done with the ray cannons** ([ray-cannons.md](ray-cannons.md)): targets appear at random around the
  heart, and the gunners hit them.
- **Debris and other obstacles** hit the ship, and **the shield** ([ship-shield.md](ship-shield.md)) holds back only so
  much. The shield is the spirit's ward (a talent, [spirit-talents.md](spirit-talents.md)), and the crew keeps it up
  at the spirit's cairn by fusing stockpiled minerals or channelling the spirit, while others cure the heart.
- **Storms have a difficulty, and a harder storm's heart moves.** How much it moves depends on how hard the storm is.

## The run
- **The storm** is a disc of weather about 1.5 km across: a dark wall around it, rougher bands inside, and the
  **heart** at its centre, a glowing knot of storm in a ring of calmer air (the eye). From the calm sky it reads as a
  dark mass on the horizon.
- **Crossing the wall** starts the warning: the light drops, the wind rises and thunder rolls, and the HUD names it.
  Then the run starts.
- **On the way in:** low sight, a darker and taller cloud sea, and gusts that shove the ship sideways, harder the
  deeper you go. Debris rides the wind (rock, wreckage, ice) and hits the shield, while lightning flashes all around.
  Gas pockets drift in the bands (GasChallenge).
- **At the heart:**
  - **Targets** appear one or a few at a time at random spots around the heart, and each stays for a few seconds. A
    beam hit on a live target cures a share of the storm, and the storm's strength falls with each hit. A missed
    target fades and another comes.
  - **The storm fights back.** At the eye's edge the gusts are strongest and debris comes thickest, and in harder
    storms the heart moves.
  - **The crew's jobs:**
    - the helm holds the ship in range against the gusts;
    - the gunners hit the targets and shoot down debris;
    - one player keeps the ward up at the cairn, fusing crystals from the bin or channelling the spirit;
    - and anyone free repairs what gets through.
- **Cured:** the storm comes apart. The walls fall, the light comes back warm, and the sea settles to calm. This
  change is the slice's showpiece, so it is slow (tens of seconds) and seen from inside. The zone is then spent
  (`encounters.md`, open question 3: for a while, or for good).
- **Sailing out before it is cured:** the run is abandoned, and the storm rests and keeps raging.
- **If the ship breaks:** see `encounters.md`, open question 2.

## Difficulty
One number per storm, `Severity` (for example 1 to 3), rolled with the storm or set on it, and on the chart once seen.
It scales:
- **The heart's movement:** still at 1; at 2 it drifts slowly about the eye; at 3 it wanders faster and farther, so
  the helm has to chase it while the gunners track it.
- How long the targets stay up, and how many hits the cure needs.
- Gust strength and how often they come.
- How much debris comes, and how hard it hits.

The heart's path comes from the storm's seed and the synced time, so every peer sees it in the same place and the
beam hits what the gunner sees.

## The look and the sound (the vertical slice)
Two numbers drive all of it:
- **Storm depth** (per peer): 0 at the wall, 1 at the eye's edge, from the zone's bounds (`IEncounterZones`) and the
  viewer's position. It is presentation only, the same on every peer, and never replicated. The eye itself is calmer:
  depth falls off again inside it.
- **Storm strength** (replicated, on the zone): 1 while it rages, falling to 0 as it is cured. The look is depth times
  strength, so each hit visibly calms the storm and every peer sees the same thing.

What they drive:
- **Clouds:**
  - The sea: darker, a taller and faster swell, towers through the surface.
  - The clouds above: the overcast lid closes in.
  - The walls: tall cloud that reads from afar.
  - The eye: a ring of cloud walls around still air, with the heart glowing in it.

  This is a local `WeatherState` blend (cloud-sea phase 5 plans a global one). The storm is the first weather state,
  blended by depth and strength instead of by time, so phase 5 can build on it instead of beside it.
- **Light:** the sun dims and cools, the ambient goes to slate and plum (never grey, per `ART_DIRECTION.md` pillar 3),
  and fog closes in. The Atmosphere feature owns the look's light (and the day cycle drives it), so the storm modulates
  it through one seam rather than fighting it. Lightning is a flash in the air and in the deep, with a soft bloom per
  pillar 5, not hard neon. The heart and its targets glow; they are what the gunners look for in the dark.
- **Sound:** a wind bed that rises with depth, rain on deck and on the sails, creaking timber, and thunder that comes
  later the farther the flash is. The heart has its own deep pulse, and a cured hit rings out. Its channel is
  `Ambience` in the mixer (`audio-mixer.md`: the cloud-sea ambience bed is its "Later" item).
- **Motion** (pillar 4, "alive"): pennants and sails snap harder, lanterns swing, rain streaks.

## Gusts (gameplay)
A gust is a sideways push on the ship, through the flight-forces hook that cloud flight already uses
(`Core/Ship/IAirshipFlightForces.cs`), server only. Gusts come in waves rather than as a constant force, so the helm can
read and answer them, and they grow with depth and severity. A gust's direction and timing come from the zone's seed
and the synced time, so the rigging, sound and camera shake on every peer match the push. Only the ship moves; a gust
never throws a player off the deck by itself.

## Debris
Debris is sky hazards that move: spawned on the storm's upwind side, carried by the wind, despawned when they leave
it. They hurt the ship on contact (`SkyHazardConfig.ImpactEffect`, through the shield first) and break on a beam hit
(`ray-cannons.md`, R2). Moving hazards are the "roaming comets" work in
[hazards-and-boons.md](hazards-and-boons.md) (H1); the storm's debris is the same thing, pushed by the storm's wind.

## Slices
Needs first: `encounters.md` E1 (the zone), `ray-cannons.md` R1 and R2, `ship-shield.md` SH1 to SH3 (with the crystal
bin), and `spirit-talents.md` T1 to T3 (a spirit that can learn Ward).
- **ST1 The storm in a lab.** `Lab_Storm`, with sliders for depth and strength: clouds, light and sound from calm to
  the wall to the heart, and the cure as strength falling. Tuned by eye, with the look sweeps (`look-slice.md`). No
  gameplay. It can start now, as it needs none of the above.
- **ST2 The storm in the world.** The zone in `cheesed_scene`: the wall seen from afar, the warning on entering, depth
  driving the look on every peer.
- **ST3 Gusts and debris.** The push by depth and severity, and debris on the wind against the shield.
- **ST4 The heart and the cure.** Targets, the beam's hits, strength falling, the heart's movement by severity, the
  storm coming apart, the zone spent.
- **ST5 Playtest and tune.** Size, severity steps, gust strength, debris, how dark it can get and still be readable,
  how long the cure takes.

## Tests
EditMode: the depth function (outside, the wall, the bands, the eye), the blend by depth and strength, the gust waves
(the same push from the same seed and time), the heart's path by severity (still at 1; the same path on every peer),
the target rules (appear, stay, fade, a hit counts once), and the run's rules (start, abandon, cure, spent). Scenario
`StormCure` in a test-range scene, solo and host + client: enter, see the warning, reach the heart, hit the targets,
and the strength falls to 0 on both peers. Look and sound by eye in the lab and in play, with a scene capture.

## Open questions (for the developer)
1. **Solo:** one player cannot steer, shoot and shield at once. Options: severity 1 storms are tuned to be soloable
   (the heart still, the targets slow, little debris); severity scales with the crew aboard; or the bigger storms are
   for crews only.
2. **Does the whole storm drift** across the world as well, so you meet it in different places, or does only the heart
   move?
3. **Hits that miss:** does a beam that misses a target and hits the heart itself do anything (feed the storm,
   nothing, a small cure)?
