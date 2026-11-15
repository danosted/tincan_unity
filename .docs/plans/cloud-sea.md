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

It depends only on the world seed, the synced time (`ITimeService`), the active weather and the server's list of holes
(see "The drawn sea and its holes"). Every peer can then evaluate it without sending more than that list over the
network, and the server stays authoritative for what it does to ships.

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

### The drawn sea and its holes (developer, 2026-11-14)
The volumetric clouds look wrong under the moving day ([`day-cycle.md`](day-cycle.md)), and making every cloud a
3D mesh that looks painted from every side, under every light, is the hardest form of the problem. Each distance gets
its own cheap layer instead:

- **The sea is drawn like water, shaded like cloud.** A grid follows the camera, snapped to a fixed step so it never
  swims, lifted by a few large, slow waves (rolling swells, not chop). It is shaded with the clouds' soft gradient
  (cream lit tops, lilac troughs), fades toward the sky at grazing angles, and a slow scrolling noise adds the billowy
  detail. It replaces the volumetric layer as the sea (phase 4) and the lab's static sea C.
- **The server owns the holes.** A small replicated list (a `NetworkMediator`), each hole with a centre, a radius,
  a drift, and an open and close time on the synced clock. Peers never get a hole per frame: each one works out where
  a hole is and how open it is from the list and `ITimeService.Time`, so every peer sees the same hole at the same
  moment. The weather, places and designed events ask the server for holes.
- **The sea reacts to the holes.** The same short list goes to the shader as globals (fixed small maximum, about 16).
  Around a hole the sea sinks into it, thins and dissolves, with a raised, lumpy rim and a slow swirl. As a hole opens,
  drifts or closes, the sea flows to match. Nothing is baked, so the change is smooth. (This is the role of the
  render-target-driven, procedural environments in Unreal, done here as a formula both sides share.)
- **One formula, two readers.** The field query ([`CloudSurfaceQuery.cs`](../../Assets/Scripts/Features/CloudBoundary/CloudSurfaceQuery.cs),
  later the phase 1 field) and the sea shader compute height and "is this a hole" from the same list and the same
  low-frequency waves. Only visual detail (the noise, the rim lumps) is shader-only, and it never decides lift or a
  fall. A test checks the C# and the HLSL formulas against fixed points.
- **Clouds above float and drift, drawn the sea's way** (first planned as cards; see step 2). One lumpy surface each, drifting with the wind, fading
  at their outline and before the far plane. Kept few (transparent overdraw and
  sorting). Distant clouds are painted into the sky (`Shaders/PaintedSky.shader`), tinted by the day.
- **Flying through is a camera effect.** Small soft white wisps near the camera, streaking with speed, faded in by
  the depth `CloudSubmersionProcessor` already gives, on top of the existing whiteout fog. It is a particle effect, not
  a full-screen filter.

Order: the sea with holes first (biggest on screen, the only part that touches gameplay), then the clouds above, then the
wisps. Each is tried in the cloud lab, then in `cheesed_scene`.

**Step 1, the sea with holes (built 2026-11-14, not yet playtested):** went straight to the play scenes, because the
holes are server state and the lab has no network. `CloudSeaFeatureInstaller` (in `Profile_FuelSandbox`, so
`cheesed_scene`, and `Profile_Test_Look`) brings the CloudHolesState fixture, the ambient holes (a few at a time
near the ship, random sizes from 25 to 180 m, each opening, drifting and closing over a minute or two, until the weather
opens holes), and the sea drawn around the camera. The game's surface drops by `CloudBoundaryConfig.HoleDepth` (400 m)
inside a hole, so a player who falls in drops that far before the existing character reset (or the overboard choice)
takes them: bottomless, for now. Ships are not affected yet; they have no gravity (phase 2). The painted sky sinks into
a dark void below the horizon, which is what a hole shows. The shared `CloudBoundaryConfig` is now **Swell** (developer,
2026-11-14), so the game's surface and the drawn sea roll together in every scene that uses it: 20 m amplitude, a
1 km longest wave, 3 m/s (the config's height amplitude, 1 / height frequency, swell speed). Code map: the Cloud boundary row. Scenario
`CloudHoleOpens`.

Seen (developer, 2026-11-14): the movement is right, but the sea reads as solid ground, not cloud. Added, visual only:
**lumps** (billow noise heaps the surface into rounded mounds at two sizes, creases between them, cool hollows) and
**shells** (`CloudSeaConfig.Shells`, 12 by default, 1.5 m apart on a 1.2 km grid: see-through layers above the surface,
cut by one drifting fluff field at a rising threshold, so the tops are soft, fray and let the sun through). Set Shells to
0, 8 or 16 to compare, and watch the cost in the profiler. The volumetric clouds are off in `cheesed_scene` (its
`VolumetricCloudsVolume` is inactive) until the clouds above replace them. Committed cb5192e.

**Step 2, clouds above the sea: tried and removed (2026-11-15).** Two drawn versions were built and dropped before
they were committed. First, camera-facing cards, which read as puffy snowflakes and blinked at the edge of the range.
The blinking was the player camera's flat far plane (1000 m, `NetworkPlayer.prefab`) cutting clouds drawn out to
3.5 km; the sea's surface now fades out by real distance before 95% of the far plane (`CloudSeaPresenter.FadeOut`).
Second, one lumpy dome per cloud shaded with the sea's look, which the developer found awful. What stays from them:
the sea's light lives in `Shaders/Include/TinCanCloudLook.hlsl`. The clouds above are now the volumetric clouds,
painted with that look (below).

**Trying (developer's idea, 2026-11-15): the volumetric clouds as the high sky.** In `cheesed_scene` only, the
volumetric clouds are back on, but as a distant sky layer, not the local layer the ship flies through: Local Clouds
off, bottom 1.5 km above the camera, 1.2 km thick, erosion 0.25 and no micro erosion (softer, less photographic),
density 0.6, a 6 km coverage map. They use their own profile copy, `Settings/VolumetricCloudsSkyProfile`, so
`drm_cloud_environment` and `Lab_Clouds` keep `VolumetricCloudsTestProfile`. Tuned on the scene's
`CloudAtmosphereDirector`. To judge: whether they sit with the drawn sea in one style, how
they layer with the sea near the horizon, and how they look at dusk and at night.
Seen (developer, 2026-11-15): seams across the sky (the coverage map does not tile; it is cleared for the sky), clouds
too dense and uniform, and undersides almost black. The black is physical lighting before dawn: no sun and almost no
sky light. Chosen: shade them with the sea's look. The vendored shader
(`ThirdParty/VolumetricCloudsURP/VolumetricClouds/VolumetricClouds.hlsl`, the block marked "TinCan: the painted
look") now takes the march's sun term per unit of opacity through the sea's light bands and colours, the night shade
and the air, when the director's Painted Look is on and the cloud sea publishes its look
(`CloudSeaShaderGlobals.ApplyLook`). Elsewhere the clouds stay physical. Painted Light Gain on the director sets how
much of each cloud reads as lit. Keep that block when updating the vendored package.
Then (developer: better, but still too dense and uniform): the coverage generator's noise now repeats exactly per map,
so a map tiles with no seam, and **TinCan > Features > Generate Sky Coverage Map** writes
`Settings/CloudSkyCoverageMap.png` (about half open sky), tiled every 8 km over the sky. Density 0.3, Shape Factor
0.85, a 600 m layer. The old `CloudCoverageMap.png` is unchanged until someone regenerates it.
Then (developer: every cloud the same size, spread evenly; colour and light fine): the renderer cuts every cloud from
one noise at one scale, so the variety has to come from the map. The sky map now spreads coverage over a wide range
(about 29% open, 19% thick), varies the height limit (alpha, 0.3 to 1 of the layer: flat clouds beside towers) and
the cloud type by region. Shape Factor 0.6 lets coverage set each cloud's size; the layer is 1 km.
Then (developer: spread them in height, not on one plane; two volumetric systems at once?): the renderer reads one
`VolumetricClouds` setting from the volume stack, so two layers would mean rewriting it to run two passes; not done.
Instead a second TinCan block in the vendored `VolumetricCloudsUtilities.hlsl` ("TinCan: the height spread",
`_TinCanCloudSpread`, the director's Height Spread): a cloud fills only that fraction of the layer, ending at its
region's height limit, so clouds sit at different heights with their own bases. The layer is local again (parallax,
the ship can fly into it) from 250 m to 2.5 km above the sea, spread 0.3, step cap 60 m so the march reaches about
7.7 km.
Then (developer: weird blobs; the shapes were fine before, only the height range was too narrow): seen from close by
(local, 250 m) with 60 m steps the coarse shape noise reads as smeared blobs. Back to the sky layer and 30 m steps,
from 1 km to 2.6 km, spread 0.55: each cloud about 880 m tall, close to the 1 km clouds that looked right, with bases
from about 1 km to 1.7 km and tops from about 1.5 km to 2.6 km. Committed 6abdfc3 as the first stable version.
Then (developer: too solid; want a factor for how opaque each cloud is, adjustable): Density still sets the overall
opacity (0.15). New on the director, Opacity Variation (0.6): the sky map's green channel, now spread over 0..1, says how
opaque a region's clouds are, and the vendored block "TinCan: the opacity variation" scales their density by it (at 1
the faintest regions are clear air). While it is on, the extinction is a fixed 0.08 instead of the rain's. The
director's two variety knobs share the global `_TinCanCloudVariety` (x height tiers, y opacity variation).
Then (developer: a blob with a weird arch underneath): the height spread hung each cloud's base a fixed distance below
its region's height limit, which changes smoothly across the map, so bases curved into arches and tops into blobs.
Replaced by height tiers (vendored block "TinCan: the height tiers", the director's Height Tiers): the layer is a few
stacked bands, each a whole cloud layer with a flat floor that reads the map at its own offset, so low and high
clouds sit in different places with flat bases. Set: 2 tiers from 1 km to 2.8 km (900 m each), Density 0.15, Opacity
Variation 0.6.
Then (developer: the bands show as cut-off lines toward the horizon): the floors now rise and fall slowly across the
sky (the director's Height Jitter, global `_TinCanCloudVariety.z`): the map's alpha read at a fifth of its scale, so
over kilometres, flat under one cloud but different from cloud to cloud, by up to that many bands. The layer holds
tiers + jitter bands, so a lifted floor stays inside it. Set: 2 tiers, jitter 1, from 1 km to 3.4 km (800 m bands).
Committed ecf0328 (the sky version).
Trying (developer, 2026-11-15): the clouds local, so you feel submerged in them. Local on, from 30 m to 2.4 km, 3 tiers
(600 m bands, the lowest at ship height), erosion 0.6 and micro erosion on (detail up close, where the smooth sky
settings read as blobs), 192 steps of 20 m (about 3.8 km of cloud per ray; empty sky is crossed in double steps). To
judge: the look up close and inside a cloud, the frame time, and whether far clouds end in a visible line. Back to the
sky version: Local off, Bottom 1000, Range 2400, 2 tiers, erosion 0.25, micro off, 128 steps of 30 m.
Committed 4f04a08. Then (developer: likes it, but barely sees the clouds as local): the Galleon spawns at 40 m over a
sea at 0, and the lowest floors sat at 30 to 630 m. Now Bottom 10 m, 2 tiers, Height Jitter 0.3: bands about 1 km
thick, the lowest floors between 10 and 320 m, so low clouds rise from below the ship to well above it.

**Step 3, under the sea and the wisps (asked by the developer, 2026-11-15; built 2026-11-15, not yet playtested).** Being under the cloud sea did
not match what you see, and there was no sense of speed. Today:
- the submersion (fog, dimmed sun, muffled audio) is measured from the game's surface, the swell. The drawn heaps sit
  up to about 14 m above it and the shells up to about 18 m, so you are visibly in the cloud tops before the game
  counts you as submerged;
- under the sea the painted sky is not fogged, and the sea is drawn one-sided, so you look up at a clear sky.

What changes:
- **Submersion starts where the cloud is seen:** `CloudSubmersionConfig.SurfaceLift` (15 m) raises the line it is
  measured from, and it ramps over the same Transition Depth (25 m). This also moves the muffled audio and the dimmed
  sun; the cloud boundary, the holes and falling are unchanged (they read the surface itself).
- **Under means under:** the painted sky and the painted volumetric clouds fade to the air's colour as the whiteout
  rises (global `_TinCanWhiteout`, set by the atmosphere), and the sea draws its underside, so just under the surface
  you see a cloud ceiling, and deeper only fog.
- **Wisps:** small soft streaks around the local camera (one particle system, world space, so they stream past and
  stretch along the travel). Their intensity comes from speed and depth, depth weighing more: light streaks above the
  sea when moving, denser once under, strongest just before full whiteout, then they fade into the fog. A burst when
  crossing into the cloud. The rules are a pure `CloudWispsProcessor` with EditMode tests; `CloudWispsPresenter` draws
  them, `CloudWispsConfig` holds the knobs, `Shaders/CloudWisp.shader` shades them with the sea's look. Presentation
  only, never replicated. Judged in `cheesed_scene`: sailing high, skimming the surface, diving deep.
- Not in this step: wisps inside the floating volumetric clouds (the game does not know where those are).

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
4. **The visible sea follows the field.** The drawn sea ("The drawn sea and its holes") replaces the volumetric
   layer: its swell is the field's, the server's holes open, drift and close in it, and the C# and shader formulas are
   tested against each other. Then the clouds above and the fly-through wisps. Scenario: the server opens a hole and
   host and client agree on its state and on the surface height at its rim. Check the look by eye and with a scene
   capture.
5. **Weather.** `WeatherState` assets, a weather director (host decides, peers blend from synced time), the sea, clouds,
   sun and fog driven from it. Scenario: host and client report the same weather blend at the same time.
6. **Holes and currents in play.** Permanent wells and whirlpools as places, currents that move ships per band, the
   wake, the crew details. Stealth once an observer exists.

## Open questions (for the developer)
1. **What lies beneath the sea?** Decided for now (developer, 2026-11-14): nothing. It is bottomless, and falling
   through a hole is death. A ship or character below the sea's floor is lost; the view below a hole fades to a deep
   void colour. A lower layer or ground can come later.
2. **What pays for open-air lift:** the engine's fuel, a separate gas, or both (gas for holding, fuel for climbing)?
3. **How big is a hole?** Sizes vary; which sizes play well is part of designing the gameplay and still open
   (developer, 2026-11-14). The holes and the drawn sea must therefore work from a few ship lengths up to kilometres
   (the rim and swirl scale with the radius), and the sizes are set by whatever asks for a hole, never in the sea.
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
