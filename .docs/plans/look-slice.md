Status: Draft (2026-11-01)

# The look: golden afternoon, first slice

## Why
[`ART_DIRECTION.md`](../ART_DIRECTION.md) says most of the feel comes from light, shading and air, not from models.
This slice builds that foundation once, so every later asset is judged against the real look instead of URP Lit
grey. The test piece is the Corsair (`Settings/ShipDesigns/Corsair.ship.json`) under a golden late-afternoon sky.

## Today (verified 2026-11-01)
- Unity 6000.4, URP 17.4. Renderer features on `Settings/_MainURPRenderer`: `VolumetricCloudsURP` and `TargetOutline`.
- Ship parts and the frame hull use URP Lit materials (`Materials/ShipParts/M_Ship*`). The frame hull has one plank
  material and one beam material (`ShipDesignsConfig.FramePlanks`, `FrameBeams`), so hull sides look like decks.
- `Shaders/SkyIsland.shader` already does wrapped diffuse with vertex-colour masks: the nearest thing to the target.
- Fog is on only in `cheesed_scene`, `cvg_airship_default` and `drm_cloud_environment`; the test scenes have none.
  `CloudEnvironmentView` (CloudBoundary) writes `RenderSettings` fog and dims the sun from the camera's cloud
  submersion, so it already owns the fog.
- The skybox in those three scenes is `FS002_Day` from the Fantasy Skybox pack, which is going
  ([`remove-old-third-party-content.md`](remove-old-third-party-content.md)).

## Decisions it builds on (ART_DIRECTION, "Decided")
Two soft bands with coloured shadows; no outlines on the world; golden late afternoon first; depth of field in play
only on far layers, with a setting to turn it off; the palette as it stands.

## Slices
Each slice ends with a contact sheet of the Corsair (L0's tool) the developer looks at before the next.

### L0: palette as data, and a contact sheet
- One palette source, `Assets/Art/Palette/palette.json` (role, hex), from which a script writes the doc swatches
  (`.docs/art/palette/`) and the Unity materials take their colours. The swatches stop being hand-kept.
- **TinCan > Dev > Look > Contact Sheet**: builds a design in edit mode off-screen (the shipyard preview builder, the
  way the Corsair renders were made) and renders fixed views (side, three-quarter, bow, deck at eye height) to
  `Logs/look/`. Agents and people review the same pictures.

### L1: the stylized lit shader and the material library
- `Shaders/Stylized.shader` (`TinCan/Stylized`), text HLSL, SRP batcher compatible, with shadow, depth and
  depth-normals passes: wrapped diffuse in two bands with one softness value; shadows lean toward the palette's
  shadow tint; a soft sky-coloured rim; ambient from the sky; a soft highlight band for metal (no reflections);
  vertex colour multiplies the base (wear, occlusion); additional lights (lanterns) shaded the same way; fog from L2.
- The ship materials move to it with palette colours: timber dark, timber warm, deck, brass, iron teal, canvas,
  lacquer red, spirit cyan (emissive).
- The frame hull gets a wall material: `ShipDesignsConfig` gains `FrameWalls` (timber dark), and
  `ShipFrameMeshProcessor` builds wall slabs into their own mesh (today they share the planks mesh). A small code
  change in ShipDesigns, with its tests.
- `SkyIsland.shader` takes the same lighting include, so islands and ships sit in one light.

### L2: air you can see
- A shared include, `Shaders/Include/TinCanAtmosphere.hlsl`: height fog in bands (densest at the cloud sea, thinning
  upward), tinted by the angle to the sun (gold toward it, plum and blue away), distance fading to the mist colour.
  Used by the stylized, island and cloud shaders, so everything fades the same way.
- One owner for the atmosphere: a new feature, `TinCan.Features.Atmosphere` (its own assembly and installer), with a
  `LookProfile` asset (light colour and angle, ambient, fog bands and tints, sky colours, grading, far blur) applied
  by one view. Cloud submersion stays a cloud concern: CloudBoundary hands its submersion amount to the atmosphere
  through a contract instead of writing `RenderSettings` itself.
- Glow halos: emissive materials plus wide, soft bloom (high threshold, large scatter), so lanterns and the spirit
  bleed into the air.

### L3: the sky and the grading
- `Shaders/PaintedSky.shader`: a skybox of palette bands (warm haze at the horizon to sky blue, mulberry bands at
  dusk), a sun disc with a large soft halo, no texture. Replaces `FS002_Day` in the three scenes.
- A volume profile `Settings/Look/Look_GoldenAfternoon`: neutral tonemapping, split toning (warm highlights, plum
  shadows), shadows-midtones-highlights, the wide bloom, depth of field (Gaussian, starting beyond the play space,
  folded into the mist), no vignette, lens dirt or chromatic aberration.
- A graphics setting, "Far blur" (on by default), through the Settings feature.

## Verify
- EditMode tests: every ship part's renderers use `TinCan/Stylized` with library materials; the palette JSON and the
  material colours agree; the frame wall mesh split (ShipDesigns tests).
- The contact sheet after every slice, accepted by the developer.
- One perf run (`perf.ps1`, see `PERFORMANCE.md`) after L2 and L3: the frame time stays within budget.
- The scenarios that fly a ship, solo and host + client, after L1 and L2 (asked first).

## Out of scope
Characters, the UI theme (it takes the palette later), part motion (rotors, cloth sway: a plan of its own), light
shafts (after L2, if the mist alone does not carry it), other times of day.

## Decided (2026-11-01)
- The atmosphere is its own feature, `Atmosphere`, fed by CloudBoundary's submersion (L2).
- Test scenes stay plain: no look profile, fog or grading there, so scenarios stay cheap and easy to read. The look
  goes on the play scenes (`cheesed_scene`, `cvg_airship_default`, `drm_cloud_environment`) and the contact sheet.
  Shared materials still change in test scenes (they use the same ship parts), which is fine.
