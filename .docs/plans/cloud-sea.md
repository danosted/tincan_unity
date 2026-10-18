Status: Draft

# Cloud sea: sailing on and in the clouds

## The fantasy
The world is a sea of cloud, and ships sail on it and in it. It is not water: a ship can sink into the cloud on
purpose, hide in it, cross holes in it, and climb out again. Four ideas carry the fantasy:

1. **Cloud lift is cheap, open air is expensive.** Cloud holds a ship up for free. Away from cloud (above it, or over a
   hole) the ship spends fuel or gas to stay up. Every route choice becomes a choice about lift.
2. **Depth is a gameplay axis.** The sea has bands, and each one trades sight, safety and stealth (maybe speed, through
   currents). A water ship lives on one plane; a cloud ship picks its depth.
3. **Weather is the tide.** Clear weather does not mean an empty sky: the sea thins and sinks, and holes open. Storms
   raise it, swell it and push towers through the surface. The weather changes the map you sail on.
4. **What you see is what holds you up.** The visible cloud and the cloud the simulation uses are the same thing.
   A hull never floats on nothing or sinks into a cloud that isn't drawn.

## What exists today
- **Cloud surface (gameplay):** `Features/CloudBoundary/CloudSurfaceQuery.cs` gives one height per (x, z) from Perlin
  noise (`CloudBoundaryConfig`: flat, rolling or layered; base altitude 0, amplitude 20). It has no thickness, holes or
  time.
- **Going under is failure:** `CloudBoundaryProcessor` / `CloudBoundaryUseCase` start an emergency when a ship sinks
  `EmergencyDepth` below the surface, and when it runs out the expiry handler takes over. Characters deeper than
  `CharacterResetDepth` are moved back onto their ship.
- **Submersion feel exists:** `CloudSubmersionProcessor` turns depth below the surface into whiteout, vapour, dimmer
  light and muffled audio, and `CloudEnvironmentView` applies it to fog and the sun (fog and sun only so far).
- **Ships have no buoyancy:** the airship's rigidbody has no gravity (`Core/Ship/AirshipControllerView.cs`), and
  altitude comes from pitching (`AirshipMovementProcessor`). Parts already have `Lift` and `Mass`
  (`Core/Ship/Parts/ShipPartStats.cs`), but lift is only a yes/no check (`ShipStats.CanLift`).
- **Fuel:** `Features/Airship/Fuel/` burns fuel with throttle and through leaks, and stalls the engine when the tank is
  empty.
- **Visual clouds:** the vendored volumetric renderer (`Assets/ThirdParty/VolumetricCloudsURP/`, ours to maintain),
  tuned by `CloudBoundary/Atmosphere/CloudAtmosphereDirector.cs`. Its layer runs from y 6 to y 406, so today the ship
  sails *under* a cloud ceiling, and the gameplay surface at y 0 has no visible cloud. A coverage/type map
  (`CloudCoverageMapGenerator`) already varies the clouds across the world. The old mesh sea and puffs
  (`CloudEnvironmentView`, legacy visuals) are off in every scene.

## Known issue: cloud seams at the start of a play session (shelved)
Straight horizontal and vertical seams show in the volumetric clouds, but only at the very start of a play session
(seen in `cheesed_scene` right after spawning; screenshot on 2026-10-15).

Tried on 2026-10-15 and reverted, because the seams stayed: a per-camera, persistent temporal history in
`VolumetricCloudsURP.cs` (the render graph path creates the history as a transient texture each frame, and binds it to
the material *after* the denoise pass that reads it), with the camera's own previous view-projection passed to the
denoise pass instead of URP's `_PrevViewProjMatrix` (only valid when a motion vector pass runs), and the blend
skipped on a camera's first frame or after a jump. Those are still real weaknesses in the vendored code, but they are
not what causes these seams.

Leads not yet checked, all things that only happen once per session:
- The Editor compiling shader variants asynchronously the first time the clouds draw, so some passes of the
  multi-pass composite run with a placeholder shader for a few frames.
- The runtime material copy, the cloud-type lookup texture or the noise textures being created on the first frame.
- `resetWindOnStart` and the first wind/time values (the synced `ITimeService` arriving after the first frames).
- The dynamic ambient probe not yet computed.

A quick split: set Temporal Accumulation Factor to 0 and see if the seams still appear; then try a build (no async
shader compilation) or turn off Editor > Asynchronous Shader Compilation.

## Design

### The cloud field: one source of truth
A single field answers "what is the cloud like here, now?" for the simulation, and the renderer draws the same field:
- the **surface height** (the top of the sea, with swell),
- the **density with depth** (thin at the top, solid further down),
- **holes** (where the field says "no cloud"),
- the **current** (horizontal drift at a depth).

It depends only on the world seed, the synced time (`ITimeService`) and the active weather. Every peer can then
evaluate it without sending it over the network, and the server stays authoritative for what it does to ships.

The field is sampled the same way on the CPU and the GPU: the large shapes (height, holes, swell) come from a few
low-frequency layers both sides compute or read from one shared texture. Fine detail (wisps, erosion) is visual only
and never decides lift.

### Lift and momentum: dive to climb
Steering stays as it is: the pilot pitches to climb and dive. There is no separate depth or trim control, so one
person can still fly the ship. What changes is that **speed and height become one resource you trade**, and the cloud
makes the trade cheap. Think of skydiving, or the dive-and-pull-up of gliding in the Batman games.

- **Diving gains speed, climbing spends it.** Pointing the nose down adds speed on top of what the engine gives;
  pointing it up turns speed back into height. Level flight at cruise speed costs only the engine's fuel, as today.
- **In cloud, the trade is generous.** Diving through cloud builds speed fast (the cloud carries you, like a
  current), and climbing inside cloud loses little speed. A ship can hold any depth in cloud for free.
- **Breaching.** A ship that pulls up hard out of a dive bursts through the surface with speed to spare and carries
  on rising into open air, then arcs back down: a long, floating jump. The deeper and longer the dive, the higher and
  farther the jump.
- **In open air, the trade is expensive.** Climbing bleeds speed quickly and nothing gives it back. When speed runs
  out, the ship sinks. Its parts' `Lift` (balloons, gas bags) slows that sink, and **burning fuel** (a boost) can hold
  or regain height for a while. A heavy ship with small balloons arcs short; a light one with big gas bags glides far.
- **Crossing a hole** becomes a move the pilot plans: dive along the rim to build speed, pull up at the edge, breach,
  and arc across. Too slow, or too wide a hole, and the ship needs a fuel burn, or it falls through.
- **The emergency changes meaning:** being in cloud is normal now. Danger comes from the deep band and from running
  out of speed and lift over a hole (falling through). `CloudBoundaryProcessor` turns into the band and fall rules
  below.

### Depth bands
Band edges are measured from the local surface, so they move with the swell.

| Band | Sight | Safety | Stealth | Speed |
| --- | --- | --- | --- | --- |
| **Above** (in open air over the sea) | Full: sun, horizon, other ships | Exposed to weather and attack; costs lift | Seen from far away | Free of drag |
| **Skimming** (hull in the tops, deck clear) | The deck sees out; below decks is blind | Comfortable, cheap lift | Hidden from below, partly from the side | A little cloud drag; surface currents |
| **Submerged** (deck in cloud) | Whiteout; lanterns, instruments, a lookout | Collisions you can't see coming; crew get lost | Invisible beyond a short range | Deeper currents, fast but they go where they go |
| **Deep** (far below the surface) | Dark | Pressure/icing/lightning damage over time; storm cores | Total | Strong currents |

Stealth needs something that looks for ships (enemy ships, beacons, creatures). None exists yet, so stealth is
designed now and built when the first observer exists.

### Holes, and what lies beneath
- A hole is open air inside the sea: no cloud lift. Crossing costs lift, and diving into one drops you a layer.
- Holes come from the weather (clear weather opens more and bigger ones) and from places (permanent wells, slow
  whirlpools, updraft chimneys at their rims).
- A ship that runs out of lift over a hole falls. What it falls into depends on what is beneath the sea. That is an
  open question (below); the plan works with any answer, and a lower cloud layer is the cheapest to start with.

### Weather as tide
A `WeatherState` asset holds what the weather sets:
- **Sea:** base height, swell height, swell length and speed, hole coverage and size, density gradient with depth.
- **Clouds above:** coverage, cloud types (the renderer's Sparse/Cloudy/Overcast/Stormy), density, erosion.
- **Light and colour:** sun colour and intensity, ambient tint, depth colour (white tops to blue/violet to near
  black), fog colour.
- **Wind and currents:** direction and strength per band.

A weather director blends from one state to the next over minutes. The host picks the next state and the moment it
starts, and every peer blends from synced time, so all players see the same sky and sail on the same sea. Example
states: Fair (white, low swell, a few holes), Overcast (grey lid, flat light, a high sea), Storm (dark sea, tall swell,
towers, lightning in the deep), Dusk (warm sun, violet depths), Fog bank (the sea rises over the deck).

The weather owns the sun. Scenes stop hard-coding it (today `CloudAtmosphereDirector` overwrites each scene's sun
with its own values).

### Crew experience
- Being submerged is something the crew does together: lanterns, a lookout calling out what's ahead, rope lines on
  deck, instruments at the helm (depth, lift, current).
- The cloud reacts to the ship: a wake carved in the tops that slowly closes, vapour pouring off the deck when it
  surfaces.
- A character who falls off the ship is fine in cloud (slowly sinking, can be reached with the grapple or a rope) and
  in real trouble over a hole. This replaces the instant reset.

## Phases
Every phase is one feature slice: an EditMode test for each processor and use case, a scenario that passes
`.\.tools\verify.ps1 -Scenario <Name>` (solo and host + client), and a row in the feature index in
`.docs/CODE_MAP.md`. Phases 1 to 3 are the core of the fantasy; 4 to 6 deepen it.

1. **Cloud field.** Replace `ICloudSurfaceQuery` with a field query (surface height, density at a point, hole,
   current), deterministic from seed, time and weather, in `CloudBoundary`. Keep a flat preset for test scenes.
   Scenario: two peers sample the same points at the same synced time and get the same answers.
2. **Lift and momentum.** A pure processor for the speed-height trade: pitch, cloud density at the hull and part
   lift give the change in speed and the sink rate, and a fuel boost holds height in open air. Steering input is
   unchanged. Core gets a hook the feature implements, the way `IAirshipTuning` works, so `TinCan.Ship` never
   references a feature. Scenario: a ship dives through cloud and gains speed, breaches and rises above the surface,
   loses speed and sinks over a hole, and holds height over the hole while boosting.
3. **Depth bands.** Band evaluation replaces the emergency; the deep band does damage over time; falling through a
   hole replaces the old expiry. Submersion feel per band (extend `CloudSubmersionProcessor`). Characters sink slowly
   in cloud instead of being reset. Scenario: a ship crosses all bands and every peer agrees on the band.
4. **The visible sea follows the field.** The volumetric layer becomes the sea: its top follows the field's height
   and swell, holes are cut where the field says, and density and colour change with depth (a per-height tint in the
   vendored shader). A thin, sparse upper layer stays for the sky. A first look can be had before phase 1 by moving
   the layer below the ship in `CloudAtmosphereDirector` (bottom about -300, top about 0). Check by eye and with a
   scene capture.
5. **Weather.** `WeatherState` assets, a weather director (host decides, peers blend from synced time), the sea, clouds,
   sun and fog driven from it. Scenario: host and client report the same weather blend at the same time.
6. **Holes and currents in play.** Permanent wells and whirlpools as places, currents that move ships per band, the
   wake, the crew details. Stealth once an observer exists.

## Open questions (for the developer)
1. **What lies beneath the sea?** The ground far below (a lost surface, ruins), a lower cloud layer, or a void.
   This sets the tone of the world and what falling through a hole means.
2. **What pays for open-air lift:** the engine's fuel, a separate gas, or both (gas for holding, fuel for climbing)?
3. **How big is a hole?** A few ship lengths (a jump you plan) or kilometres (a region with its own play)?
4. **How far can a breach carry a ship?** That sets how wide a hole can be crossed on momentum alone, and so how much
   holes are about skill versus fuel.

Decided: steering stays pitch-to-climb, with no separate depth or trim control; the cloud boosts the speed-height
trade instead (dive to gain speed, breach to climb).

## Risks
- **Visuals vs simulation drift.** If the GPU and the CPU sample the field differently, hulls float on nothing. Keep
  the gameplay field low-frequency and shared; never let visual detail decide lift.
- **Rendering cost.** Sailing inside the volume puts the camera in the densest, most expensive part of the raymarch.
  Budget it early (`.docs/plans/performance-budgets.md`), and keep whiteout fog doing the work when fully submerged.
- **Flight feel.** A speed-height trade can feel floaty, or punish a pilot who just wants to cruise. Level cruising
  must stay as easy as today; the trade should reward dives, not tax normal flight. Playtest phase 2 before building
  on it.
- **Networking.** Vertical motion now depends on the field. The server stays authoritative; clients only predict
  from the same field, so a mismatch shows as a correction, not a split world.
