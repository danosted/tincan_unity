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

### L0: palette as data, and a contact sheet (done 2026-11-01)
Built as planned; the code is under `DevTools/Editor/Look*.cs` (CODE_MAP, "The look"). Materials start reading the
palette in L1. Until the look profile (L2) exists, the contact sheet lights the ship with a fixed late-afternoon sun
and a plain sky-coloured background, in a preview scene of its own.
- One palette source, `Assets/Art/Palette/palette.json` (role, hex), from which a script writes the doc swatches
  (`.docs/art/palette/`) and the Unity materials take their colours. The swatches stop being hand-kept.
- **TinCan > Dev > Look > Contact Sheet**: builds a design in edit mode off-screen (the shipyard preview builder, the
  way the Corsair renders were made) and renders fixed views (side, three-quarter, bow, deck at eye height) to
  `Logs/look/`. Agents and people review the same pictures.

### L1: the stylized lit shader and the material library (built 2026-11-01, awaiting the developer's look)
Built as planned: `Shaders/Stylized.shader`, the light model in `Shaders/Include/TinCanLighting.hlsl` (shared by
`SkyIsland.shader`), the library in `DevTools/Editor/LookMaterials.cs` (**TinCan > Dev > Look > Build Materials**), and
the wall mesh split (`ShipFrameStoryMesh.Walls`, `ShipDesignsConfig.FrameWalls`). Beams and railings took timber warm,
walls timber dark; the cairn's stone is rock ochre (the palette has no grey). The helm keeps the pirate pack's material
until its own model exists. The contact sheet compiles shaders synchronously, or its first views come out blank.
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

### L2: air you can see (built 2026-11-01, awaiting the developer's look in a play scene)
Built: `Shaders/Include/TinCanAtmosphere.hlsl` (in Stylized, SkyIsland, CloudSea, CloudPuff), the Atmosphere feature
(`Features/Atmosphere/`, in `Profile_Galleon` and `Profile_FuelSandbox`), the core contracts `IViewSubmersion` and
`IAtmosphereOwner` (`Core/Domain/Atmosphere/`), and `Settings/Look/Look_GoldenAfternoon` with its bloom volume
(**TinCan > Dev > Look > Build Atmosphere**). Along the way:
- Ambient gives way to the sun on lit faces (`TinCanLighting.hlsl`), so lit surfaces no longer wash out.
- The play scenes name their sun (Lighting > Sun Source): none did, so the cloud dimming never reached the light.
- The cloud tuning rig (`CloudAtmosphereDirector`) also wrote the sun every frame; it now leaves it to the owner.
- The player camera had post-processing off; the atmosphere turns it on for the local camera.
- Not covered: the volumetric clouds (third-party) and the skybox take no banded air; L3's painted sky matches the
  horizon to the fog.
- Verified: `AtmosphereProcessorTests` (cloud whiteout and dimming) and the scenario `LookApplies` in a new test area,
  `Test_Look` (Test_Core plus Atmosphere; decided 2026-11-01: the one test area with the look, so the rule that every
  slice ships a scenario holds and the other areas stay plain), solo and host + client. Flying into the cloud deck is
  not scripted: the boundary rescues a ship that deep. The test-range builder now names each scene's sun; only
  `Test_Look` is rebuilt so far.

Planned:
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

### The lab sheet (2026-11-01)
**TinCan > Dev > Look > Lab Sheet** renders the look on test geometry (`Logs/look/lab.png`), because the ship's flat
faces hide the shading. Its first renders found three faults, now fixed:
- **The band sat past the terminator.** Wrapped diffuse put the step at N.L 0.35, and the shadow map cut the wrapped
  part in jagged steps. The step now starts at the terminator, and its width is the softness in N.L. Wrap defaults to 0,
  and the islands no longer use it.
- **The fog hid everything beyond about 250 m.** It now gives about 25% at 250 m, 65% at 1 km and 85% (the cap) at 2 km.
- **Glows did not bloom in the sheets.** They rendered into a low-range target; they now use an HDR one.

A third, half-lit band is now an option (`_Bands` 3).

Open: the band softness (the library's 0.35 reads as smooth shading; 0.02 to 0.1 reads as cel steps), and two or
three bands.

### The cloud lab (2026-11-01)
The volumetric clouds look too realistic for the style. **TinCan > Dev > Look > Build Cloud Lab**
(`DevTools/Editor/CloudLab.cs`) writes `Scenes/Lab/Lab_Clouds.unity`, a bare scene for iterating on stylized clouds in
edit mode. It has the golden-afternoon look (sun, ambient, post, and the banded air in the shader globals while the
scene is open), a `Lab Camera` on the hero view (cruising height, the sun 45 degrees off ahead), a 30 m ship stand-in,
pillars at 250 m, 1 km and 2 km, the current volumetric clouds (inactive, to compare against), and `Clouds (new)`.
It has no lifetime scope and is not in the build list: nothing plays there. Unlike the play scenes it has a camera, so
the Game view shows the hero view. Rebuilding overwrites it. The scene and its meshes (`Art/Clouds/Lab/`) are
generated and kept out of git.

Decided (2026-11-01): the first technique is mesh clouds with a stylized shader (not a restyled volumetric renderer).
Built, first iteration:
- `Shaders/StylizedCloud.shader` (`TinCan/Environment/Stylized Cloud`): the world's bands (`TinCanBand`), lit by a
  normal blended from each puff's toward the whole cloud's shape, so a cloud shades as one mass; cream tops, lilac
  shadow sides, a darker base, a rim toward the sun, a silver lining when looking into the light; a depth pre-pass so
  only the outer silhouette fades and the puffs never sort against each other; the look's air.
- `DevTools/Editor/CloudShapes.cs`: seeded meshes, a heaped cumulus of merged puffs and the sea approaches below.
- `DevTools/Editor/CloudSeaField.cs`: the sea as a height field (swells, holes with a rounded shoulder, lumps), so
  every sea approach shares one layout.
- `Materials/Clouds/M_CloudCumulus`, `M_CloudSea` (puffs) and `M_CloudSeaSurface` (palette colours; made only when
  missing, so Inspector tuning survives a rebuild). A surface fades out toward its floor (`_BottomFade`), so hole
  rims dissolve instead of ending in a cut edge.
- **TinCan > Dev > Look > Cloud Sheets** (`DevTools/Editor/CloudSheet.cs`): `Logs/look/cloud-seas.png` (the sea
  approaches from the hero view and from 260 m) and `Logs/look/cloud-sweep.png` (the cumulus material, band softness
  by shape blend, lit from the front right).

The sea approaches, all in the lab under `Clouds (new)/Sea` (one active at a time):
- **A, puffs:** separate puffs, mostly sunk. Reads as bubble wrap from cruising height; near puffs are huge.
- **B, billow surface:** one surface with billow-noise lumps. The creases come out as sharp folds, like crumpled paper.
- **C, domed surface (the lab's default):** one surface of domes blended by a smooth maximum. Reads as one rolling
  surface, with soft hole rims.
- **D, C plus rim puffs:** heaped puffs where the sea breaks at a hole, for cauliflower edges.

Seen on the sheets (2026-11-01): C and D were the candidates; A and B are out. Decided (developer, 2026-11-01): C,
though not finished.

**The soft pass (2026-11-01)**, after a Tears of the Kingdom cloud the developer chose as the reference (kept locally in
`.docs/art/samples/`, out of git, by the References rule). What the reference does: a sky gradient behind, one soft
light gradient (no bands), warm grey shadows tinted by the sky, depth from soft creases between a few big lobes, small
lumps only on the top outline, faint mottling, and a low-resolution softness over all of it. Built:
- The lab sky, `Shaders/CloudLabSky.shader` (`TinCan/Dev/Cloud Lab Sky`, `Materials/Clouds/M_CloudLabSky`): a gradient
  from the look's own air at the horizon up to the palette's sky, with a soft sun halo. A stand-in for L3, lab only.
- The soft materials `M_CloudSoft` and `M_CloudSeaSoft`: band softness 0.9 and wrap 0.45 (one gradient), a warm grey
  shadow between the palette's cloud shadow and canvas, soft edges. The banded `M_CloudCumulus` and `M_CloudSeaSurface`
  stay as the "before".
- Creases (`_Crease`, baked into TEXCOORD0.y by `CloudShapes`: where puffs overlap, or where the sea surface sits below
  its neighbours) and mottling (`_Mottle`, object-space value noise); both default to 0.
- Crown puffs: small puffs on the upper faces of a cumulus' big ones.
- `Logs/look/cloud-steps.png` (Cloud Sheets): before, + sky, + soft light, + creases and mottling; hero view, close-up
  and high view. The sweep is now crease by shape blend on the soft cumulus.

Seen on the sheets: the sky and the soft light are the big steps and move toward the reference. Creases give the sea real
depth. On the cumulus they outline every puff (it reads as popcorn) at any strength, because a cumulus is 30-odd puffs
of similar size where the reference has three big lobes. Next for the cumulus: build it as a few lobes (each a cluster),
creases only between lobes. Not yet tried: the low-resolution softness (render the clouds small, blur, scale up).

Decided (developer, 2026-11-01): the "+ soft light" step is the look; the soft materials keep creases and mottling off,
and the steps sheet's last column shows them on a variant. ART_DIRECTION's cloud paragraph says so.

**Lobes (2026-11-01):** `CloudShapes.Cumulus` now builds a few lobes side by side (the middle ones tallest), each a
core puff plus a cluster shaded by its own lobe's ellipsoid, with creases only between lobes; the close-up now faces a
cumulus broadside. Seen: the lobes read as separate masses, and the creases between them give the fold the reference
has. But the side lobes come out as bald, smooth domes (the crown puffs landed on the tall lobe, and the lobe shape
dominates the light), and they sit in front of the tall one like separate balls rather than one cloud.

**Stale meshes (found 2026-11-01):** the lab used to copy a rebuilt mesh into its old asset (`EditorUtility.CopySerialized`),
and the copy kept drawing the old shape. Some earlier renders therefore mixed generations (the sea that "did not
change", and the cumulus in `Logs/look/cloud-seas-lobes-v1.png`, which mixes the dome and lobed versions). The lab now
writes every mesh as a new asset. The cumulus styles now sit side by side in the lab (`CloudLab.CumulusStyles`: the
dome from before the lobes, the first lobes, the heap) and on `Logs/look/cloud-cumulus.png`.

Seen (developer, 2026-11-01): column B of the steps sheet (the banded material, with the sky) shows the cumulus
contours clearly, and the developer likes that. To choose how crisp, `Logs/look/cloud-between.png` (Cloud Sheets)
blends the cumulus material from banded to soft (columns 0, 1/3, 2/3, 1). Its first version varied edge softness by
row and was lit from the front; the developer found it flat: the light was doing most of what they liked. The rows are
now the light: from the front, from the side, and ahead and to the side (as in the hero view). Seen: front light is flat
whatever the material; from the side or into the light the banded end gives a clear lit side, a lilac shadow side and
a bright rim, and the soft end washes that out. Since players look every way, the front-lit case needs its own answer.

**Parked (2026-11-01, developer).** Where it stands:
- Chosen: sea C (domed surface); clouds against a sky. Leaning: the cumulus toward the banded end (between column 1 and
  2 of the between sheet), not yet set as the material; the lab's default cumulus material is still the soft one.
- Open: the cumulus material pick; the front-lit case (crease strength, a sky-coloured outline rim); the heap's side
  lobe that hangs off like a ball; which cumulus style (dome, first lobes, heap) to keep.
- The developer's wish: soften the puffs' edges "with a wet brush" so they blend, with dark patches where they meet.
  That needs a screen-space step (clouds drawn on their own, filtered by blur or Kuwahara, composited back), which the
  developer has tried before and does not want now. Do not propose it again until they bring it up.
- Resume with **TinCan > Dev > Look > Build Cloud Lab**, then **Cloud Sheets**. The cumulus are close: chunky,
warm-topped, readable. The whole look is low in contrast (cream lit sides against lilac shadow sides of nearly the
same value), so band softness barely shows on the sweep; shape blend near 1 flattens a cumulus into one blob.

Not yet: casting shadows, flying through (a near fade or whiteout), motion (drift, slow billowing), the clouds in the
play scenes, and the gameplay cloud surface matching the drawn sea (`plans/cloud-sea.md`).

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
