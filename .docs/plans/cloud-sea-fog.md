Status: Draft

# Cloud sea as fog: airy, alive, sailable

Follows [cloud-sea.md](cloud-sea.md) (the drawn sea). The drawn sea's soft tops are 12 stacked see-through layers
(`Shaders/CloudSeaSwell.shader`, `CloudSeaPresenter`). Seen low and across, they read as contour lines, and where the
sea rises over an island each layer cuts the slope in a line. This plan turns the tops into fog and gives it the cues
water gets from its shaders: you see into it, light passes through it, shadows fall on it, it moves at several speeds,
and a ship leaves a wake in it.

## Done so far (2026-11-24, uncommitted, judged in play by the developer)

- **Fog over ground.** Every layer thins where opaque ground lies just behind it (`FogOverGround`, the camera depth
  texture; `_FogDepth`, metres of cloud until solid). Kept: "the fog on the island looks way better".
  Needs the depth texture on (`_MainURPAsset`) and the depth copy after opaques (`_MainURPRenderer`); with the copy
  after transparents the sea read its own surface as ground and washed out.
- Tried and dropped: a per-pixel jitter of each layer's cut (grain, the contours stayed); softer fluff, more and
  thinner layers (no help). The layers draw the contours (Shell Opacity 0 removes them).

## F1 - The fog pass (in A/B now)

A temporary switch on `M_CloudSeaSwell.mat`, **Sea Look**: Layers (today), Metre Edges (each layer edge fades over
Edge Metres of real distance), Fog Pass (no layers; the surface adds the fog a view ray crosses above it). The
developer liked the Fog Pass up close but missed air and cloud, so the Fog Pass now also has:

1. **Into it**: three deeper samples of the fog along the view ray slide against the top as the camera moves.
2. **Light through it**: thin fog bright and sunlit through, thick cores darker.
3. **Shadows on it**: the main light's shadows (ship, islands) and the volumetric clouds' shadows (they arrive as the
   main light's cookie).
4. **Several speeds**: the fog body drifts with `_FogWind`, deeper fog slower, fine wisps streak along the wind faster.

Done when the developer picks a look. Then the losing look, the switch and the unused properties go, and the setting
names are final.

## F3 - The web of rifts (developer, 2026-11-24: "make the cloud sea into large islands", real, visuals first)

Surface detail alone kept reading as one blanket (a sheet, then stains, then sand dunes). Real seas of cloud read as
cloud by their large shape: wide continuous fields broken by long channels you look down into. So the sea is cut into
cells, like the plates of a cracked crust; their ragged borders are rifts, each opening and closing on its own slow
cycle, so neighbouring cells join into larger cloud islands and the web changes over many minutes.

- **Not the hole system.** Holes are a few server objects (at most 16) living a minute or two. The web is a pattern
  from the session's seed (the swell's seed offset) and the shared clock, like the swell: every peer computes the same
  one, nothing replicates.
- **Step 1, drawn only (built 2026-11-24).** `TinCanCloudWebCutAt` in `Shaders/Include/TinCanCloudSea.hlsl`, cutting
  the drawn sea like a hole. The developer: "looks good, make them real", and asked for the lower wall to curl in.
- **Step 2, real (built 2026-11-24).** `CloudSeaFormula.WebCut` (the same hashes as the shader, no sin in them), a web
  case in `CloudSeaFormulaParityTests`, and `CloudSurfaceQuery` cuts the game's surface by it: a ship falls into a rift
  as into a hole. The settings (`CloudWeb`) live on `CloudSeaConfig`, not the boundary's config, so only profiles with
  the Cloud Sea feature get rifts (`Profile_Galleon`, `Profile_FuelSandbox`, `Profile_Test_Look`); the presenter hands
  them to the shader each frame (`_TinCanCloudWeb`, `_TinCanCloudWebMotion`), and `TinCanCloudHoleCut` includes the web,
  so every reader of the sea agrees. Scenario `CloudWebReal` (`Test_Look`): near the ship, the surface in a rift is
  down by the hole depth and on a cloud island at the swell, on every peer.
- **The curl (visual only).** Down a hole's or rift's wall the drawn surface tucks back under its own edge (`_Curl` on
  the sea material, metres), so the lower wall curls in instead of standing like a cliff.
- **Ambient holes stay**, back to 4, as smaller breaks inside the cells.
- The Fog Pass's bump shading made the puffs read as sand dunes; it is down to a hint (`Fog Shape`).

## F4 - The mist near the camera (developer, 2026-11-24: "mist along the cloud sea edges instead of the polygon like surface")

Every look so far is drawn on surfaces (layers, Metre Edges, the Fog Pass). From afar a surface passes for volume; up
close its geometry shows: contour lines, cut slopes, the hard outline of a rift's wall. Only a volume reads as mist.
So near the camera the sea becomes a volume, and the layered surface stays for the distance, where it looks good.

- **The mist is the sea's own shape, softened.** The sea is a height field (`TinCanCloudSea.hlsl`: swell, holes, web).
  The mist's density at a point comes from how far it lies below that top: a soft band from `Above` metres over it to
  solid `Below` metres under it. Down a rift's wall the band follows the cut, so the edge is a gradient instead of a
  line, with no edge finding. A thinner tail (`Spill`) hangs from the rim down into rifts and holes. Heaps and a drifting
  3D detail noise break the top. Visual only and per peer, like the rest of the drawn sea.
- **Look and quality are separate.** The look (shape, softness, colour, density) is one value for everyone, on the
  mist's material `M_CloudSeaMist`; the sea's colours and light come from the sea's own look globals
  (`TinCanCloudLookFromGlobals`). A quality tier changes only how well that same mist is sampled, never where it is, so
  a rift's edge reads the same on every machine. Gameplay keeps the hard cut (`CloudSurfaceQuery`).
- **Quality is compute, view distance is reach (developer, 2026-11-24).** A tier says how finely the mist is sampled,
  never how far it reaches. `CloudMistQuality` holds four tiers (Low, Medium, High, Ultra): resolution, step length
  (metres per step along a ray), a cap on steps per ray, light steps, detail octaves, ship shadows. Picked by the
  player setting `graphics.clouds` (Auto, Off, Low, Medium, High, Ultra), on the Settings menu's Graphics page beside
  the view distance; Auto follows Unity's quality level through the asset's mapping.
- **View distance** (`graphics.view_distance`: Near 700, Normal 1000, Far 1500, Very Far 2500 m, core
  `ViewDistance`) sets the local view camera's far plane; the air already fades to it, and the drawn sea's grid grows to
  reach it (`CloudSeaPresenter.SurfaceGridSize`), so Very Far costs a larger grid only when picked. The mist reaches a
  share of it (`CloudMistQuality` reach share, 0.6) and fades over its last part; the drawn sea carries the rest, where
  mist would read as flat haze. Each ray takes as many steps as its path through the mist needs at the tier's step
  length, up to the tier's cap, so a longer view distance costs more steps and a short path (looking down) costs few;
  past the cap the steps stretch.
- **The pass.** `CloudMistRendererFeature` on `_MainURPRenderer`, after transparents: a raymarch at the tier's
  resolution, only through the slab the mist can occupy and only up to its reach, with steps finer near the eye, then a
  depth-aware upsample onto the screen. Shading inputs (lit, hollow, thin) are gathered along the ray and shaded once
  with the cloud look. Opaques stop the ray through the depth texture; transparents (wisps, beams) are not inside the
  mist, a known limit.

Steps:

1. **The look (done 2026-11-24, judged on High; the developer: "it looks great").** The pass marches the formula
   directly; the upsample blends a 4x4 patch of the march so its grain evens out. Switch the tier with the quality
   level; the sea material's Sea Look **Off** hides the layered sea, to see the mist alone.
1b. **Danger and shadows (built 2026-11-25, in review; the developer: holes and rifts are "dangerous" and too hard to
   spot, and the ship's and islands' shadows should fall on the mist).** Where the ray first meets the mist, the top's
   normal (from the same height field, `MistTop`) is lit by the banded sun with a rim, as on the drawn sea, so a hole's
   or rift's wall shows its curve: the sunward wall lights, the far one shades, the crest of the drop gets a rim (`Form`
   on the material; no rim light, which pulled the mist to the sky's colour and made it read see-through). Form fades
   out as the sun nears the horizon: lit from under it, the swells' backs banded into sand dunes at night (the
   developer, 2026-11-25: "I like it during the day very much"). **Depth Darken** darkens the mist by how far under
   the sea's surface it is seen (the swell's surface, so a swell's trough stays light; full at **Depth Range** metres),
   so a hole or rift reads as a drop at night too, without the sun. The sun's shadow is read at that point too, crisp on the top, and darkens the mist after its
   shading by **Shadow Darken** (the light's own shadow strength, 0.4 on the sunset light, divided out): read through
   the lit term it came out a few percent dark, which in play looked like no shadow at all. **Shadow Debug** on the
   material (temporary) paints the top red where it is in shadow. The day's sun peaks at 12°, so shadows fall about
   five times their caster's height away, long and thin. The game's shadow distance was 50 m,
   too short to reach the mist, so each tier has a **Shadow Range** (Low and Medium 0, High 400, Ultra 600 m):
   `CloudMistPresenter` stretches the URP asset's shadows to it at runtime in four cascades, the first kept at the
   game's own 50 m so the deck stays sharp (`CloudMistShadowRange`), and puts them back on Off, a tier without range,
   or exit. The cost is the shadow casters drawn per cascade; to measure in step 3. Judge it only after the Game view's
   "Shaders ready" notice (`ShaderCompileNotice`): until then the Editor still draws the old mist.
2. **Hand-off to the layers.** The near surface fades out where the mist takes over (`_TinCanCloudMist`, one global), so
   both never draw the same cloud; tuned so the seam does not show as a moving ring.
   Measured 2026-11-25 (cheesed_scene, one fixed view at noon, 2075x968, the developer's high-end GPU; ms of GPU time
   per frame, the march / the shadow map): Low 0.26 / 0.01, Medium 0.42 / 0.01, High 1.7 / 0.72, Ultra 3.3 / 1.0;
   the whole frame about 7.4 with no mist. Two cuts, no visible loss: no sun shadow per step (the top's single sample
   carries the visible shadow) and no light steps on Ultra (High never had them). After: Medium 0.32, High 1.3,
   Ultra 1.8. The stretched shadow map (High 400 m, Ultra 600 m) stays the next largest cost. Note Auto picks the tier
   from Unity's quality level, which is the same on every machine, so a weaker PC gets Ultra too: choosing the level
   from the hardware, and a VSync/frame cap setting, are next (the developer, 2026-11-25: a weaker PC tore badly).
3. **Speed.** Bake the sea's cut height once per frame into a small texture around the camera, so a step reads a texture
   instead of the web and holes; measure each tier's GPU time in one fixed view (a sweep like the look sweeps), cut any
   knob step that costs over about twice for a difference you must hunt for. Then the tiers' numbers are final.
4. **Scenario.** `CloudMist` in `Test_Look`: the tier is picked and the pass draws on every peer, nothing on a server.

Open: the camera inside the mist and the cloud whiteout (`IViewSubmersion`) must agree on who draws it; temporal
accumulation (less grain, more smear) only if the steps alone stay grainy.

## F2 - The wake

A ship in or near the sea parts it: a trough along its path, bright torn wisps at the bow, slow swirls behind.
Visual only and per peer: the game's surface (`CloudSeaFormula`) does not change.

- **Trail (pure, tested).** `CloudWakeProcessor`: from each ship's position each frame, keeps a short trail of points
  (one every few metres, dropped after some seconds), each with its age and the ship's speed there, and how deep the
  hull sits in the sea (0 above it). Tests: spacing, ageing out, no trail above the sea, a stopped ship leaves none.
- **Presenter.** `CloudWakePresenter` (every peer, in `CloudBoundary`): reads the ships from `IActorRegistry`
  (`IAirshipView`, as `AmbientCloudHolesUseCase` does) and the surface from `CloudSurfaceQuery`, runs the processor,
  and hands the trails to the shader as globals (a fixed number of points; first ship first).
- **Shader.** The sea, near a trail: the surface dips into a trough that closes as the points age, the fog thins
  along it and its edges brighten and curl (the foam), and the fog's drift bends round the hull.
- **Bow wisps.** The existing wisps (`Wisps/`) also emit at the bow while the hull is in the sea and moving.
- **Settings** on `CloudSeaConfig`: trail length (s), point spacing (m), trough depth (m), trough width (m).
- **Scenario.** `CloudWake` in the cloud sea's test-range scene: the ship sinks into the sea and moves; every peer has
  a trail behind it; above the sea, none.

Open: one ship or all ships (default all, up to a small cap); whether the trough should also reach the game's surface
later (a ship could hide in its own wake), not in this plan.
