Status: Draft (2026-11-01)

# The art pipeline: make the look a system, keep taste a quick choice

## Why
The developer's strength is systems, not painting or animating. So turn as much of the look as possible into data,
tools and checks that an agent can run, and shrink the remaining taste work into choices the developer makes in
seconds from a sheet of renders, rather than things they build by hand.

This plan does not set the look: [`ART_DIRECTION.md`](../ART_DIRECTION.md) does, and
[`look-slice.md`](look-slice.md) builds it. This plan is about the pipeline around them: how variants are made, how a
render is judged, and how assets and motion are made without hand-crafting. It started as an outside suggestion
aimed at a "Tears of the Kingdom look"; here it is fitted to what TinCan already decided (an autumn-gold palette, Ori's
air, Ghibli's light; Tears of the Kingdom only for the sky islands and the clouds).

## Today (verified 2026-11-01)
Most of the suggestion's first step already exists:
- **Most of the look is rendering.** Settled in ART_DIRECTION ("Light and shading"). One master shader,
  `Shaders/Stylized.shader` (`TinCan/Stylized`), with the light model in `Shaders/Include/TinCanLighting.hlsl` (bands,
  coloured shadows, rim, sky ambient) and the air in `Shaders/Include/TinCanAtmosphere.hlsl` (banded height fog tinted by
  the sun's angle). Islands and clouds share both includes.
- **The style lives in data, in three places:** the palette (`Art/Palette/palette.json`), the material library (a table
  in `DevTools/Editor/LookMaterials.cs`; band softness and band count are set per material here), and the look profile
  (`Settings/Look/Look_GoldenAfternoon`, a `LookProfile`: sun, ambient, fog bands, bloom), applied by the Atmosphere
  feature.
- **No painted textures.** Already the rule, by a different route than a palette atlas: assets name a palette role, the
  asset builder swaps in the library material, and variation comes from vertex colour (ART_DIRECTION, "Materials";
  "Budgets": no bitmap textures on ship parts).
- **A look-dev harness, partly.** **TinCan > Dev > Look > Contact Sheet** (the Corsair from four fixed views),
  **Lab Sheet** (test geometry, one question per tile, already a small sweep of band softness by band count) and
  **Build Cloud Lab** (`Scenes/Lab/Lab_Clouds.unity`, a fixed hero camera). They run from the Editor menu and write to
  `Logs/look/` (not in git). No baselines, no diff, no written rubric, no command-line entry.
- **Assets are scripts.** `.tools/blender/rotor_mast.py` builds the first Blender part from nothing (FBX, manifest,
  optional preview PNG). The mesh contract is in [`airship-parts-pack.md`](airship-parts-pack.md); nothing checks it yet.
  ART_DIRECTION proposes triangle budgets (part 3k, large prop 5k, small prop 1k).
- **Tests on the look:** `LookMaterialsTests` (ship parts use the library), `LookPaletteTests` (docs match the palette),
  `AtmosphereProcessorTests`, scenario `LookApplies` (`Test_Look`).
- **Waiting on the developer:** the look slice is parked after L2 on band softness (the library's 0.35 reads as smooth
  shading) and two or three bands. L3 (painted sky, grading, far blur) waits on that.

## What we take from the suggestion, and what we leave
| Suggestion | Here |
|---|---|
| One master toon shader and one post stack | Have it (`TinCan/Stylized`, the Atmosphere feature's volume). Keep going in look-slice. |
| One "style profile" holding every tunable value | Partly: split across palette, material library and `LookProfile`. See P1 for whether shading values move into the profile. |
| Painterly brush noise in the shader | Open: pillar 2 says detail goes in geometry and colour, not texture noise. Built as swappable surface styles and judged on a sweep (P1) before deciding; see "Open questions". |
| Palette atlas texture | Leave: palette roles and library materials do the same job, keep colours tuneable in one JSON file, and batch through the SRP batcher. |
| Foliage normals and wind | Later, with island grass and trees (no foliage exists yet). Wind sway in the material, as ART_DIRECTION's "Motion and life" already says for cloth. |
| Fixed scenes rendered headless to a contact sheet | Have three sheets; add a hero shot (P2) and a command-line entry (P4). Overcast, interior and night wait: only golden afternoon is decided. A character turntable waits for crew-animation S2. |
| Tiered gates: deterministic, regression, vision model, developer | Yes, as P3 (contract), P2 (baselines and diff), P5 (rubric); the developer's acceptance already ends every look slice. |
| Parameter sweeps; "B, but warmer" | Yes, P1. It is the fastest way to unblock the parked band choice. |
| One target shot before more content | Yes: the Corsair over the cloud sea, golden late afternoon (the hero time; not dusk), from the cloud lab's hero view. |
| Procedural animation | Yes, through the existing plans (see "Motion"). |
| Procedural modelling in Blender | Yes, continuing the `.tools/blender/` scripts (see "Assets"). |
| Learn from existing toon shaders and breakdowns | Yes, for technique only; see "References". |

## Slices
Each slice is small, ends with a sheet or a green suite, and asks the developer only for a pick.

### How visual approaches swap
We expect to go back and forth on shading choices (bands, painterly noise, later the sky and the clouds), so each
choice is a swappable part, wired the way the rest of the game is: data, a pure processor, and one presenter that
applies the result, with VContainer supplying the parts. The Atmosphere feature already works this way (`LookProfile` →
`AtmosphereProcessor` → `AtmospherePresenter` → `AtmosphereShaderGlobals`); this extends it rather than adding a second
path.
- **One profile, made of parts.** `LookProfile` keeps the air (`LookSettings`) and gains `Shading` (band softness, band
  count, rim, shadow lean, today set per material) and a `Surface` (the surface style asset, below). The installer
  registers the parts it finds, so the presenter receives them and never knows which approach is in play.
- **A surface style is a contract with one implementation per approach.** `ISurfaceStyle` (in the Atmosphere feature):
  it turns its own settings into a `SurfaceStyleFrame` (which shader keyword is on, and up to two vectors of values).
  First implementations: `FlatSurfaceStyle` (no keyword: today's look), `ColourDriftSurfaceStyle` (large, faint
  world-space warm/cool patches), `BrushEdgeSurfaceStyle` (a ragged, brush-like edge between lit and shadow bands).
  Each is a small ScriptableObject asset with its own values, so its strength is tuned in the inspector, and its
  evaluation is pure and tested.
- **The shader side mirrors it.** One global keyword per style, declared once in `Stylized.shader` and
  `SkyIsland.shader`; each style's HLSL in its own file under `Shaders/Include/Surface/`, called from one new hook
  function in `TinCanLighting.hlsl`. `TinCanBand(nDotL, wrap, softness, bands)` keeps its signature:
  `StylizedCloud.shader` (in progress in the cloud lab) calls it. Flat compiles to nothing, so the default look pays
  nothing.
- **Gated, with the material as fallback.** The shading globals carry an "on" flag, like `_TinCanAtmosphereOn`. Off
  (every scene without the Atmosphere feature: all test areas but `Test_Look`), shaders use the material's own values,
  because unset globals read 0 (softness 0 is a hard line, 0 bands is nothing). On, they use the profile's, unless the
  material overrides that value.
- **Material overrides live in the library table.** `LookMaterials.Build` rewrites each material from scratch, so an
  override set on a `.mat` would be wiped: the override switch and value are columns in the `LookMaterials` table.
  The cloud material keeps its own band values for now; clouds can follow the profile later. The surface style is
  world-wide, never per material: one painterly rule for everything.
- **Today's band rules hold.** The step starts at the terminator and its width is the softness in N·L; wrap stays 0
  (a wrapped band is cut into jaggies by the shadow map); ambient gives way to the sun on lit faces. A brush edge
  roughens that step; it does not move it.
- **No default changes without a pick.** Moving the shading into the profile keeps today's values (softness 0.35, two
  bands) until the developer picks from a sweep.
- **One applier, two callers.** A static `LookShaderGlobals` (beside `AtmosphereShaderGlobals`) writes shading and
  surface frames into globals and keywords. In play the injected presenter calls it each frame, so editing a profile
  or style asset during Play updates the view at once. In edit mode the look tools (contact sheet, lab sheet, sweep,
  and later the cloud lab's `CloudLab.ApplyAir`) have no container; each builds the same parts from the assets and
  calls the same applier. Edit mode and
  play can therefore never show different looks. Every edit-mode tool switches the globals and keywords off again in a
  `finally`, the way the sheets already call `AtmosphereShaderGlobals.SetEnabled(false)`, or the Scene view keeps the
  last variant. Sweep renders follow the sheets' rules: HDR targets, the first view rendered twice (the first render
  after a reload can come out blank).
- **Adding or dropping an approach** is one style class, one HLSL file, one keyword and one asset. The presenter, the
  sweep and the other styles do not change. A test fails when a style's keyword is not declared in the shaders.

### Built (2026-11-01), with P1
The swappable parts and P1 are built; awaiting the compile, the tests and the developer's picks. Where it differs from
the design above:
- **Object space, not world space.** Surface styles sample their pattern at the object-space position
  (`TinCanSurface.styleCoord`), so a pattern rides with a moving ship instead of swimming over it. Patterns do not
  line up across neighbouring parts; at these sizes that should not show (judge it on the sheet).
- **Per-value "Keep own" switches on the shader** (`_OwnBandSoftness`, `_OwnBands`, `_OwnRim`, `_OwnShadowFill`), set
  from `LookMaterials` columns. Stylized defaults to follow the look; SkyIsland defaults to keep its own (islands use
  softness 0.5 and rim 0.1, unlike ships), so islands look as before until they are opted in.
- **Apply needs no rebuild.** Shading is a global now, so applying a variant writes the profile and the next frame
  shows it; **Build Materials** is needed only when a library override changes.
- **Material property overrides in a variant are not built yet**: they arrive when the cloud sweep moves onto
  `LookSweep` (with the cloud lab session's agreement). Fog overrides likewise wait for a sweep that needs them.
- **Tiles carry their letter and values** in a small block font (`LookSheetText`), so the PNG alone is enough to pick.

Files: `Features/Atmosphere/` (`ShadingSettings`, `SurfaceStyle` with `ISurfaceStyle` / `SurfaceStyleFrame` /
`FlatSurfaceStyle`, `ColourDriftSurfaceStyle`, `BrushEdgeSurfaceStyle`, `LookShaderGlobals`; `LookProfile`, the
presenter and the installer changed), `Shaders/Include/TinCanLighting.hlsl` and `Include/Surface/` (`Noise`,
`ColourDrift`, `BrushEdge`), `Stylized.shader`, `SkyIsland.shader`, `DevTools/Editor/` (`LookSweep`, `LookSweepSheet`,
`LookSweepEditor`, `LookSheetGrid`, `LookSheetText`; `LookMaterials`, `LookContactSheet`, `LookLabSheet` changed), the
`LookApplies` probe, `LookShadingTests`.

### P1: the comparison tool (the sweep sheet; unblocks the look slice)
- **TinCan > Dev > Look > Sweeps** (Render All, Build Defaults; one sweep from its inspector or the Assets menu): renders one subject (a lab tile, the Corsair three-quarter view, or the hero shot)
  under four to nine variants on one sheet, each tile labelled A, B, C... with its values printed under it. Writes
  `Logs/look/sweep-<name>.png` and a small JSON of the variants beside it.
- A sweep is an asset (`LookSweep`): a base `LookProfile` plus a list of variants, each a set of overrides (a different
  surface style, a shading value, a fog value). "B, but warmer shadows" becomes B's overrides plus one change, and the
  next sheet is one command. Building a variant composes the same parts the game uses (see above), so what the sheet
  shows is what play shows.
- Three sheets already sweep by hand, each with its own copy of the grid code: the lab sheet (softness by bands on
  spheres), the cloud sheet's `cloud-sweep.png` (softness by shape blend on the cumulus, `CloudSheet.cs`) and the
  contact sheet. The sweep pulls that code into one shared sheet helper (tile grid, labels, HDR target, the double
  render, restoring the scene in a `finally`), and a variant can override either the profile or a material property
  on chosen renderers, so the cloud sweep can become a `LookSweep` asset later. Moving the existing sheets onto the
  helper comes after the sweep works, and `CloudSheet` only with the cloud lab session's agreement.
- **Apply variant** on a sweep writes the winner into the base profile (or its style asset); materials and atmosphere
  rebuild from there (**Build Materials**, **Build Atmosphere**). Old sweeps stay as assets, so going back to an earlier
  choice is rendering that sweep again and applying its other tile.
- First sweeps:
  1. **Bands** (`Sweep_Bands`): band softness (0.05, 0.1, 0.2, and today's 0.35) by two or three bands, on the Corsair
     and on side-lit shapes, so the parked look slice picks on the ship, not only on spheres.
  2. **Surface:** flat, colour drift (faint and stronger) and brush edge (faint and stronger), on the lab's canvas
     spheres, the Corsair and (once P2 exists) the hero shot. Answers the painterly-noise question.

### P2: the target shot and baselines
- **The hero shot**: built on the cloud lab's own sheet (`CloudSheet.cs`, rendering from the lab scene's `Lab Camera`;
  in progress), not a new renderer: add the Corsair in place of the stand-in and, after L3, the painted sky. All look
  work is judged on this frame first. Starts after the cloud lab work is done.
- **Baselines**: when the developer accepts a sheet, it is copied to `.docs/art/look/` (committed, small PNGs) with the
  date. ART_DIRECTION can then show the current look.
- **Diff**: after a render, compare each tile with its baseline (per-tile mean colour difference, with a tolerance) and
  write `Logs/look/<sheet>-diff.png` and a line per tile that moved. An agent that changed something unrelated to the
  look and sees a tile move says so in its summary.
- Advisory, not a test: GPU renders differ across machines and driver updates, and the EditMode suite must stay fast
  and green. Revisit if renders prove stable on this machine.

### P3: the asset contract, checked
EditMode tests over `Assets/Models/ShipParts/*/` and the part prefabs, so a broken asset fails the suite (this gate
blocks):
- A `manifest.json` beside each FBX; its moving parts and sockets exist as named children.
- Scale and axes: imported bounds fit the part's cells (contract rules 1 and 2), pivot as the manifest says.
- Triangle budget by size class, starting small: a ship part 1.5k, a large prop 3k, a small prop 500. Raised only
  when a real asset needs more and the developer agrees (ART_DIRECTION's budgets change to match).
- Materials: every renderer uses a library material (extends `LookMaterialsTests`); Blender material names map to
  palette roles.
- No mesh collider made from the render mesh (contract rule 5).
- The Blender side: each `.tools/blender/*.py` exits non-zero when its own output breaks the contract, so the script
  that builds a part also checks it before Unity sees it.

### P4: renders from the command line
- One entry that renders every sheet (contact, lab, hero, and a sweep by name) and runs the diff, so an agent can
  refresh the pictures without the developer clicking menus.
- It drives the shared Editor, so it holds the Editor lock (`Enter-EditorLock`). It never enters Play, so it is not a
  play run and needs no ask.
- A thin script of its own, `.tools/look.ps1`, so verify stays about gameplay.

### P5: the written rubric
- A short list per sheet in ART_DIRECTION, next to "Reviewing an asset": checks phrased so a model looking at the PNG
  can answer yes or no. For example: shadows are banded and lean plum or lilac, never grey; lit faces are warm; far
  pillars fade toward the mist colour, the 2 km one to a flat silhouette; every glow has a soft halo; nothing is pure
  black or white; the ship's silhouette reads at 30 m.
- An agent that changes the look reads the sheets against it and reports each answer. Advice only: it never blocks,
  and the developer's eye decides.

## Motion
Already planned elsewhere; this plan only holds the line that as much as possible is procedural:
- **Ship parts:** rotors spin, pennants and sails sway in the material, lanterns flicker (ART_DIRECTION, "Motion and
  life"). Needs its own plan ("part motion", out of scope in look-slice). Cosmetic and local to each peer, never
  replicated.
- **The ship itself:** a slow bob, roll and pitch from springs and noise, on a visual child only, never on the
  simulated body (players stand on the ship, and the hull's physics and replication must not move). Ties to the cloud
  sea's lift ([`cloud-sea.md`](cloud-sea.md)) later. Goes in the part motion plan.
- **Crewmates:** [`crew-animation.md`](crew-animation.md): Starter Assets locomotion first (S2), then procedural layers
  on the presentation state it already plans: lean from `LocalVelocity`, spine aim from `LookPitch`, hand and foot IK in
  S3 (needs the Animation Rigging package, asked when S3 starts). Spring bones for hair and cloth come after that.
- **Faces:** SDF face-shadow maps and custom face normals are the known technique for clean stylized face shading.
  Parked until characters have a model and the face style is decided (ART_DIRECTION: stylized-realistic, revisited at
  crew-animation's art step).

## Assets
- Every asset stays a script under `.tools/blender/` that builds it from nothing (`blender --background
  --factory-startup --python ...`), exports the FBX and manifest, renders a preview, and checks the contract (P3).
- Prefer parameterised generators over one-off scripts once a second asset of a kind appears: hull pieces and modular
  planks (widths varied for pillar 1), rocks and strata for sky islands, cloud masses for the cloud lab. An agent
  changes parameters and code far more reliably than it moves vertices.
- Geometry Nodes only where Python is clearly worse, and then still built and driven from the script, so the asset
  can be rebuilt headless.
- When two scripts start sharing code, pull a small helper next to them; not before.

## References
Reading open-source stylized URP shaders and published talks on stylized rendering is the fastest way to learn the
techniques, and gives agents patterns to work from. Read them for technique; write our own code. Copy nothing without
checking its licence first, and no asset, texture or silhouette is traced (ART_DIRECTION, "References").

## Relation to the look slice
[`look-slice.md`](look-slice.md) stays the plan for the look itself: L3 (the painted sky, grading and tonemapping in
`Look_GoldenAfternoon_Post`, the far-blur setting) is built there. This plan supplies the tools and the swappable
parts L3 uses; when L3 starts, its sky and grading become parts of the profile the same way.

## Working alongside the cloud lab (2026-11-01)
Stylized clouds are built in parallel (`CloudLab.cs`, `CloudShapes.cs`, `CloudSeaField.cs`, `CloudSheet.cs`,
`Shaders/StylizedCloud.shader`, `Materials/Clouds/`, and the cloud lab section of `look-slice.md`). The first round is
done (uncommitted, in the developer's review) and more rounds are likely, so any edit to those files is agreed with the
cloud lab session first. Later the cloud lab calls the shared applier and its sheet becomes the hero shot.
`LookContactSheet.AddSun` (now returns its light) and `LookLabSheet.Library` / `Shape` (now internal) are used by the
cloud lab: keep them as they are.

## Order
1. The swappable parts (shading in the profile, surface styles) and the P1 sweep; the developer picks bands and a
   surface style from it; the look slice resumes (L3: sky and grading, which become swappable parts the same way).
2. P2 hero shot; iterate on that one frame (clouds from the cloud lab, sky, grading) until it feels right. Accepted
   sheets become the first baselines.
3. P4 command-line renders and P5 rubric, so agents can refresh and self-check the look.
4. P3 asset contract, before the next Blender part lands.
5. Then content: parts from generators, the part motion plan, crew-animation S2.

## Verify
- EditMode tests for P3, for the sweep's override logic, for each surface style's frame, and that every style's
  keyword is declared in the TinCan shaders (pure, no rendering); the suite stays green (`verify.ps1 -UpTo Tests`).
- The scenario `LookApplies` (`Test_Look`) still passes once the presenter takes the new parts (asked first).
- Each sheet shown to the developer and accepted.
- No other play runs: nothing here changes gameplay or networking.

## Out of scope
Other times of day, interiors, the UI theme, characters' look, foliage.

## Decided (developer, 2026-11-01)
1. Shading values live in the `LookProfile`, with a per-material override (P1).
2. Command-line renders get their own `.tools/look.ps1` (P4).
3. Baselines are committed under `.docs/art/look/` (P2).
4. Triangle budgets start small: 1.5k / 3k / 500 (P3).

## Open questions for the developer
1. Painterly noise: flat colour (pillar 2 as written), a large, faint colour drift, or a brushy band edge? Judged on
   the surface sweep (P1) before deciding; whichever wins, the others stay as assets to switch back to.
