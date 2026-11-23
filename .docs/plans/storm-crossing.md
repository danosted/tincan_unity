Status: Approved (2026-11-23: ST1, ST2, ST5 built)

# The storm: the first encounter zone, cured at its heart, and weather made of cloud, light and sound

## Why
The first encounter zone ([encounters.md](encounters.md)) is a storm. You sail into it, reach its heart, and cure it
with the ship's cannons while the rest of the crew keeps the ship flying and whole, and the sky clears. It is also a vertical
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

## Decisions (developer, 2026-11-23)
- **The storm doesn't wait for the shield.** Debris hits the ship's health, which it already has. The shield
  (`ship-shield.md`, with the spirit's Ward from `spirit-talents.md`) comes later as a buffer in front of that
  health, without changing the storm. Until then the crew's fourth job is repair.
- **Gusts and debris are left out for now** (developer, 2026-11-23). The heart and the cure come next; ST3 and ST4 wait.

## The run
- **The storm** is a disc of weather about 1.5 km across: a dark wall around it, rougher bands inside, and the
  **heart** at its centre, a glowing knot of storm in a ring of calmer air (the eye). From the calm sky it reads as a
  dark mass on the horizon.
- **Crossing the wall** starts the warning: the light drops, the wind rises and thunder rolls, and the HUD names it.
  Then the run starts.
- **On the way in:** low sight, a darker and taller cloud sea, and gusts that shove the ship sideways, harder the
  deeper you go. Debris rides the wind (rock, wreckage, ice) and hits the ship, while lightning flashes all around.
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
    - anyone free repairs what gets through;
    - and, once the shield is in (later), one player keeps the ward up at the cairn.
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
(`Core/Ship/IAirshipFlightForces.cs`), server only. The hook has no sideways term yet (momentum along the nose, a
vertical push, a nose drop), so ST3 adds one: a horizontal world velocity that blends in the way the vertical push
does. It is a generic term, so later weather and boons can use it too. Gusts come in waves rather than as a constant force, so the helm can
read and answer them, and they grow with depth and severity. A gust's direction and timing come from the zone's seed
and the synced time, so the rigging, sound and camera shake on every peer match the push. Only the ship moves; a gust
never throws a player off the deck by itself.

## Debris
Debris is sky hazards that move: spawned on the storm's upwind side, carried by the wind, despawned when they leave
it. They hurt the ship's health on contact (`SkyHazardConfig.ImpactEffect`; once the shield is in, it takes the hit
first) and break on a cannon hit, as hazards do now, and on a beam hit once there is one (`ray-cannons.md`, R2). Moving hazards are the "roaming comets" work in
[hazards-and-boons.md](hazards-and-boons.md) (H1); the storm's debris is the same thing, pushed by the storm's wind.

## Slices
Needs first: `encounters.md` E1 (the zone, built). ST4 needs moving hazards: the storm brings its own minimal slice
of them (spawn upwind, carried by the wind, gone when they leave), and H1's comets build on it later. ST5 needs the
beam: `ray-cannons.md` R1 comes first (developer, 2026-11-23; built the same day). The shield
(`ship-shield.md` SH1 to SH3, with `spirit-talents.md` T1 to T3) is not needed (2026-11-23).
- **ST1 The storm in a lab.** `Lab_Storm`, with sliders for depth and strength: clouds, light and sound from calm to
  the wall to the heart, and the cure as strength falling. Tuned by eye, with the look sweeps (`look-slice.md`). No
  gameplay. It can start now, as it needs none of the above.
- **ST2 The storm in the world.** The zone in `cheesed_scene`: the wall seen from afar, the warning on entering, depth
  driving the look on every peer.
- **ST3 Gusts (left out for now, 2026-11-23).** `Severity` on the zone. The sideways term on the flight forces, gust waves from the seed and the
  synced time by depth and severity, server only, and the matching cue on every peer (rigging, sound, a small camera
  shake). It needs nothing else.
- **ST4 Debris on the wind, against the ship's health (left out for now, 2026-11-23).** `Severity` on the zone, if not there yet. Spawned upwind, carried by the storm's wind, despawned when
  they leave. A hit costs the ship's health, and a cannon hit breaks them.
- **ST5 The heart and the cure.** `Severity` on the zone, if not there yet. The heart in the eye. Strength becomes replicated on the zone (it is per peer
  until now). Then the targets, the hits, strength falling, the heart's movement by severity, the storm coming apart,
  and the zone spent. Scenario `StormCure`.
- **ST6 Playtest and tune.** Size, severity steps, gust strength, debris, how dark it can get and still be readable,
  how long the cure takes.
- **Later: the shield.** A buffer in front of the ship's health (`ship-shield.md`). The storm does not change.

## Progress
- **2026-11-23, ST1 step 1: the lab with light, lightning and sound (built; EditMode 1729/1729; awaiting your eye).**
  - **The lab** (developer, 2026-11-23: in Play, with the real look): **TinCan > Dev > Storm > Build Lab** copies
    `cheesed_scene` to `Assets/Scenes/Lab/Lab_Storm.unity` (git-ignored build output) on `Profile_Lab_Storm` (the
    game's profile plus the storm) with its own volumetric cloud profile. Press Play, start a host, select
    `Assets/Settings/Storm/StormLab` and drag Depth and Strength in the Inspector (a panel on screen would fight the
    locked cursor).
  - **Light** (developer: a weather seam): Atmosphere blends a weather in after the day light and before a cloud's
    whiteout (`IAtmosphereWeather`, `WeatherLook`, `WeatherLookProcessor`), so the storm never fights the day cycle.
    The storm's look at full weight is on `StormConfig.Look`, a first guess from the palette: a dim, misty sun, slate
    and plum ambient, thick slate fog. Weight = depth x strength.
  - **Lightning** (mood only): strikes in seeded 1.5 s slots on the shared clock, more often deeper, none below a
    threshold; a double flicker of pale spirit-cyan in the ambient. The same on every peer once the storm is in the
    world.
  - **Sound** (developer: synthesized stand-ins): a storm wind, rain, the heart's deep double beat near the eye, and
    three thunderclaps, sooner after the flash the deeper the storm, through the Ambience group
    (`DevTools/Editor/StormSynth.cs`). Real clips go on `StormConfig` without code changes.
  - Played 2026-11-23 (captures `Logs/storm/depth-*.png`). The builder now puts the lab on the build list (Netcode stopped
    Play with a dialog without it) and loads its profile after opening the scene (the reference died, so every
    feature loaded).
- **2026-11-23, ST1 step 2: the clouds (built; EditMode 1729/1729; played, captures `Logs/storm/clouds-depth-*.png`).**
  - **The overcast closes in:** CloudBoundary's volumetric cloud director blends toward a weather's targets by its
    weight, in Play only (`ICloudWeather`, `CloudWeatherTarget`: density, shape, opacity variation, the light inside),
    so the scene's tuning is never written over. The storm's first guess is on `StormConfig.Clouds`. Wind speed is not
    blended: the clouds' place is time times speed, so changing it would jump them.
  - **The clouds darken:** the weather's `CloudShade` reaches the shared cloud look (`_TinCanWeatherShade` in
    `TinCanCloudLook.hlsl`), so the drawn sea and the painted sky darken toward slate and plum on their shadow sides
    too, not only where the sun lit them.
  - **Not in the lab:** a taller swell. The sea's swell is also the cloud flight's surface (`CloudSurfaceQuery`), so it
    waits for ST2, where every peer must agree on it.
  - **Next:** your eye on the look and the mix (StormConfig), then the encounter zone (E1).

- **2026-11-23, ST2 the storm in the world (built; EditMode green; play run pending).**
  - **It stands where its zone is, for everyone:** the storm's region (centre, radius, strength) is a shader global
    (`Shaders/Include/TinCanStorm.hlsl`). Over it the volumetric clouds close in (more coverage, the tallest type, to
    the top of the layer, denser) and the sea and the painted sky darken toward the storm's cloud shade, so the storm
    reads as a dark mass from afar and every peer sees it in the same place.
  - **Its shape across the disc:** rising steeply into the wall, holding through the bands, clearing in the eye
    (`TinCanStormProfile`, mirrored by `StormProcessor.Profile` for the light and air at the camera). The plan's
    "depth falls off again inside the eye" is this.
  - **Driven by the nearest encounter zone** (`IEncounterZones.Nearest`): its region, the camera's depth in it, and a
    strength that calms over 20 s while the zone rests and builds again when it returns. The storm is in the game's
    profile; the lab is now a plain copy of `cheesed_scene` with Storm Lab Controls (an injected scene view: sliders,
    or follow the zone), and Profile_Lab_Storm and the StormLab asset are gone.
  - **The warning on entering** is the zone's (encounters.md E1): the HUD line and the light, wind and thunder rising
    as the ship crosses the wall.
  - Scenario `StormInWorld`: the zone moved round the ship weighs on the light on every peer, and lifts when moved away.
  - **Next:** your eye on the storm in `cheesed_scene` (sail north 750 m), then ST5 the heart and the cure, after `ray-cannons.md` R1 (gusts and debris are left out for now).

- **2026-11-23, ST5 the heart and the cure (built with `ray-cannons.md` R2; EditMode 1714/1714; scenario `StormCure`
  play run pending).**
  - **Severity** is on the zone's config (`EncounterZoneConfig.Severity`, 1 to 3; the game's zone is 1).
  - **Nothing about the heart is sent but three numbers.** `StormHeartNetworkMediator` sits on the zone's object beside
    its mediator and replicates the storm's strength (1 raging, 0 cured), the heart's height and the last target slot
    hit. Where the heart and its target are comes from `StormHeartProcessor` on the shared clock and the storm's seed,
    so every peer sees them in the same place:
    - **The heart** is at the zone's centre at severity 1. At 2 it wanders a figure of eight 25 m out over 60 s, and at
      3 twice as far, twice as fast. It floats 10 m above the ship that sailed in (`HeartAboveShip`), set while the
      round warns, so the guns fire up at it, clear of the rails.
    - **Targets** come one at a time, in slots: up 6 s (4.8 at severity 2, 3.6 at 3), then a 1.5 s gap. Each slot's spot
      is on a ring 10 to 20 m round the heart, up to 5 m above or below it. A target hit once is spent for the rest of
      its slot.
  - **The cure** (`StormCureUseCase`, server): while the round warns, the heart rages again. While it runs, the target
    is in the beam's way (`ISpiritBeamTargets`, R2). A beam that passes within 2.5 m of it before anything solid hits
    it, and the storm's strength falls one share: 3 hits cure it at severity 1, two more for each step up. The last hit
    ends the round (the zone rests), and the storm comes apart on every peer as the shown strength follows the
    replicated one, at one share per 20 s (`StormIntensity`).
  - **The look** (`StormHeartPresenter`, every peer): the heart is a pale cyan glow that pulses and shrinks as the storm
    calms, and the target a warm gold one (`M_StormGlow`, URP Unlit, additive). The strength already darkens the
    light, clouds and sound, so each hit visibly calms it. Built by **TinCan > Dev > Storm > Build Heart**, which also
    puts the heart's mediator on the zone prefab.
  - **Scenario `StormCure`** (Galleon): the subject mans the aft cannon, and the heart goes 55 m off to starboard. The
    subject aims at the target its peer draws, three times; the storm is cured and the zone rests on every peer.
  - **Not yet:** a sound on a hit, the cure's own showpiece beyond the strength falling (walls falling, warm light back),
    and the eye's ring of cloud walls. Open questions 1 to 3 still stand.
  - **Next:** the `StormCure` play run (with `CannonShot`, `CannonMisfire` and `StormInWorld` again), then your
    eye on the heart in `cheesed_scene`.
## Tests
EditMode: the depth function (outside, the wall, the bands, the eye), the blend by depth and strength, the gust waves
(the same push from the same seed and time), the heart's path by severity (still at 1; the same path on every peer),
the target rules (appear, stay, fade, a hit counts once), and the run's rules (start, abandon, cure, spent). Scenario
`StormCure` in a test-range scene, solo and host + client: enter, see the warning, reach the heart, hit the targets,
and the strength falls to 0 on both peers. Look and sound by eye in the lab and in play, with a scene capture.

## Open questions (for the developer)
1. **Solo:** one player cannot steer, shoot and repair at once. Options: severity 1 storms are tuned to be soloable
   (the heart still, the targets slow, little debris); severity scales with the crew aboard; or the bigger storms are
   for crews only.
2. **Does the whole storm drift** across the world as well, so you meet it in different places, or does only the heart
   move?
3. **Hits that miss:** does a beam that misses a target and hits the heart itself do anything (feed the storm,
   nothing, a small cure)?
