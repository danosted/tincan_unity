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
- **Tiers as data.** `CloudMistQuality` holds four tiers (Low, Medium, High, Ultra): resolution, steps, light steps,
  detail octaves, ship shadows, and how far the mist reaches. Picked by the player setting `graphics.clouds` (Auto, Off,
  Low, Medium, High, Ultra); Auto follows Unity's quality level through the asset's mapping. The setting shows in the
  menu once the Graphics page exists (player-settings S3); until then Auto applies, and switching the quality level
  in Project Settings switches the tier.
- **The pass.** `CloudMistRendererFeature` on `_MainURPRenderer`, after transparents: a raymarch at the tier's
  resolution, only through the slab the mist can occupy and only up to its reach, with steps finer near the eye, then a
  depth-aware upsample onto the screen. Shading inputs (lit, hollow, thin) are gathered along the ray and shaded once
  with the cloud look. Opaques stop the ray through the depth texture; transparents (wisps, beams) are not inside the
  mist, a known limit.

Steps:

1. **The look (done 2026-11-24, judged on High; the developer: "it looks great").** The pass marches the formula
   directly; the upsample blends a 4x4 patch of the march so its grain evens out. Switch the tier with the quality
   level; the sea material's Sea Look **Off** hides the layered sea, to see the mist alone.
2. **Hand-off to the layers.** The near surface fades out where the mist takes over (`_TinCanCloudMist`, one global), so
   both never draw the same cloud; tuned so the seam does not show as a moving ring.
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
