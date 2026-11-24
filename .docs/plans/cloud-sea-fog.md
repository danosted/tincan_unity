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
