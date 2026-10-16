Status: Approved (2026-10-16); S1 done; S2 and S3 built, their play runs pending

# Ship frames: draw the hull, place the rest

## Why
The developer wants huge airships, and hulls that look like ships rather than stacked blocks. The shipyard today
(`modular-airship-builder.md`) builds on a 1 m voxel grid. Every deck tile, wall and rail is a part, so the work and the
cost grow with the area: the Galleon is already about 450 parts, 261 of them deck tiles, and each part is its own
GameObject with its own colliders (`Features/ShipDesigns/ShipHullBuilder.cs`). A 100 m ship that way is thousands of
parts to place, send and draw, and every angle in the silhouette needs its own hand-made part (`hull.bow`).

Inspiration (a builder the developer has seen): each story of the ship is a polygon graph. Nodes are dragged and
merged, edges are beams, and each face is filled (planks) or left open. Open sides get railings, and stations go
anywhere on a floor. The plan below takes that idea **as a hybrid**: a *frame* graph owns the hull, and catalog parts
stay for the things that are really objects (helm, engines, envelopes, masts, stairs, decoration).

Size drives the rest. A 100 m deck is a few dozen nodes, not 2,000 tiles: small files, a small network payload,
few edits, and one generated mesh per story instead of one object per tile.

## What exists to build on
- **The design model and its rules** (`Features/ShipDesigns/`): plain-C# `ShipDesign`, edits as commands
  (`ShipDesignEditProcessor`, which gives undo/redo now and co-op building later), a server-run validator that
  returns a list of problems, JSON and binary encodings behind `IShipDesignCodec`, step-by-step migrations
  (`ShipDesignMigrator`, `ShipDesignFormat.CurrentVersion` = 1) with golden files, a canonical content hash, and
  `ShipDesignLimits`. The frame adds to all of these; it replaces none.
- **Peers build geometry from replicated data** (`ShipAssemblyUseCase`, `ShipHullAssembler`, as the sky islands do).
  Generated frame meshes fit the same path.
- **The ship body is kinematic** (`Core/Ship/AirshipControllerView.cs`), so large non-convex `MeshCollider`s are
  allowed on it. Players already ride the hull's colliders as a moving platform.
- **Sockets and fitting loadouts** (`Core/Ship/Sockets/`, `Features/ShipSockets/`, `ship-fitting-loadouts.md`):
  fittings mount in sockets that parts carry, keyed by `ShipSocketId` (part instance id, index). What is mounted lives
  in `<key>.fittings.json`, never in the design.
- **Stats** (`ShipStatsProcessor`): sums of per-part mass, lift, thrust and hull.
- **Shipyard input** (`Context_Shipyard`, `INPUT_MAP.md`): levels (PgUp/PgDn), place, delete mode, rotate, undo.

## The model

### The frame: a third list in the design, beside the parts
All of it is plain C# with integer coordinates, so the file stays exact, diffable and the same on every peer.

- **Stories:** an ordered list, each with a height in frame units. Story 0 is the lowest, and a story's elevation is
  the sum of the heights below it. The height is stored per story from the start, but the shipyard keeps every story
  at 3 m until there is a reason to change it.
- **Nodes:** `id`, `story`, `x`, `y`, `z`. `y` is the node's **drift**: its height above or below its story's
  floor plane, so decks can sag, rise towards the bow or step a little, instead of lying dead flat. A node always
  belongs to exactly one story, and its drift stays inside that story's band (`±MaxDrift`, see Validation).
  Positions are in **frame units of 10 cm**. The shipyard snaps drags to 0.5 m sideways and 0.1 m in height, and a
  modifier key gives the fine step.
- **Edges:** `id`, `a`, `b` (node ids). A *floor edge* joins two nodes of the same story. A *post* joins nodes on
  adjacent stories. **Posts are the only edges between stories**, so a floor never connects to another story's floor
  except through posts. Slanted posts give a tapered hull: a narrower outline on the story below leans the walls in,
  like a keel.
- **Faces:** `id`, an ordered loop of node ids, and a `fill`:
  - a *floor face*: all its nodes on one story; fill `Open` or `Planks`. With drift its corners need not be level.
    The face is triangulated in its top-down (x, z) outline by ear clipping that starts from the lowest node id, and
    each triangle takes its corners' heights. Every peer gets the same triangles, and the floor follows the nodes;
  - a *wall face*: a quad or triangle bounded by a floor edge on story *s*, the edge above it on *s+1* and the posts
    between them; fill `Open` or `Planks`. A non-flat quad is split along a fixed diagonal (the lower node ids), so
    every peer builds the same triangles.
- **Railings:** a flag on a floor edge. A railing runs along that edge at its story's floor. It is refused on an edge
  whose wall face above is planked. This is the "railing on a face without fill" of the inspiration: an open side gets
  a railing along its bottom edge.
- **Ids:** nodes, edges, faces and parts share one counter (today's `NextPartInstanceId`, renamed `nextId` in the
  file). Ids are never reused, so anything that later attaches to a frame element (damage, a mounted station) keeps
  its key across edits. The new names are decision 9.
- **Faces are explicit, not derived.** Deriving faces from the graph's cycles would give them new ids on every edit
  and lose their fill. The edit processor maintains faces instead. A face that is split keeps its id on the larger
  half, and two faces that are merged keep the lower id. This is the part of the plan most in need of tests.

### Parts on the frame
Parts keep their catalog, footprints, orientation, stats and sockets. Their position becomes frame units (`pos`)
instead of grid cells (`cell`), so a part can sit anywhere on a deck. The shipyard still snaps blocks to whole metres,
so voxel building keeps working.

### Format version 2
- `formatVersion` 2 adds `stories`, `nodes`, `edges` and `faces`, and replaces each part's `cell` with `pos`.
- **Migration v1 → v2:** `pos = cell × 10`, and the frame lists start empty. Starter and Galleon keep loading
  unchanged and fly the same. A v1 golden file must migrate, and the result must have the same stats and the same
  part poses. The hash changes with the format; that is expected and stated in the migration test.
- **Binary form:** a frame section (nodes about 6 bytes each, edges about 6, faces 2 bytes per corner plus 2), next to
  the part palette.
- **Limits** (`ShipDesignLimits`): max nodes, edges, faces, stories, nodes per face, and the extent in frame units.
  The extent is sized for the big-ship target (decision 10), and like today, the limits are checked before anything is
  built.

### Edits (new `ShipDesignEdit` kinds)
`AddStory`, `RemoveStory`, `AddNode`, `MoveNode`, `MergeNodes` (b into a: b's edges move onto a, duplicate edges
collapse, degenerate faces go), `SplitEdge` (inserts a node), `AddEdge` (closing a loop on a story offers a new face),
`RemoveEdge`, `AddFace`, `SetFaceFill`, `SetRailing`. `Place` and `Remove` for parts stay as they are. Undo and redo
come for free, as today.

### Validation (pure, run by the server, a list of problems)
- No dangling structure: every node has at least two edges (the inspiration's "minimum edges" rule).
- Faces: no self-intersecting loops, no faces below a minimum area or edge length, and no two filled floor faces
  overlapping on a story.
- Story bands:
  - **The drift band:** every node's drift is within `±MaxDrift` of its story's floor plane (proposed 0.8 m for 3 m
    stories).
  - **Headroom:** where a filled floor face of story *s+1* lies above a filled floor face of story *s*, the vertical
    gap between them is at least `MinHeadroom` everywhere (proposed 2.2 m). Both floors are flat triangles, so the gap
    is smallest at a node or where two triangle edges cross, seen from above. The check measures exactly those points;
    nothing is sampled.
  - **Floor gap:** the same rule from the other side. No node sits closer than `MinHeadroom` below a filled floor of
    the story above, and none closer than a slab's thickness above a filled floor of the story below.
- **Walkable floors:** no triangle of a filled floor face is steeper than `MaxFloorSlope`, which matches the player's
  walkable slope, so drift never turns a deck into a wall.
- Only posts join stories, and only adjacent ones. A wall face's corners span exactly two adjacent stories.
- Connectivity: the frame and the parts form one connected whole around the core part. A part connects when it rests
  on a filled face or touches a frame element or a connected part.
- Parts don't overlap each other (rotated bounds in frame units), and a part doesn't cut through a filled face.
- Everything stays within `ShipDesignLimits`.

## Building the hull (every peer)
- **`ShipFrameMeshProcessor`** (pure): frame in, plain mesh data out (vertices, triangles, UVs, per material).
  - Planks: each filled face becomes a slab 0.15 m thick. Floor slabs extrude straight down from their triangles
    and wall slabs inwards, so the deck top is exactly where the nodes are. UVs are projected in metres along the face's longest edge, so boards run
    lengthwise and keep their size on any face.
  - Beams: a square wooden prism per edge, with a post block at each node to cover the joints (no mitering in the
    first slices).
  - Railings: posts and a rail along each railing edge.
- **Assembly:** one mesh per story per material (planks, beams, railings) under `DesignedHull`, each with a matching
  non-convex `MeshCollider`. Very large stories are split into chunks once the big-ship pass shows the need.
  Diffing is per story: an edit rebuilds only the stories it touches.
- **Art:** wood plank and wood beam materials (the developer prefers wood to the iron beams of the inspiration),
  tiling in world metres.

## Stats
`ShipStatsProcessor` adds a frame term from `ShipFrameTuning`: mass per square metre of plank and per metre of beam
and railing, and hull per square metre of plank. Lift and thrust still come only from parts. The shipyard HUD's
Flight line shows the total, as today.

## Freeform stations: floors as surface sockets
Stations go anywhere on a floor without the builder knowing they exist (the 2026-10-06 rule still holds).
- *Core* (`Core/Ship/Sockets/`): a filled floor face is a **surface**. `IShipSockets` (or a sibling
  `IShipSurfaces`) also lists a ship's surfaces: face id and its ship-local triangles. A mount's height comes from
  the triangle under it.
- *ShipSockets:* looking at a free spot on a surface and pressing E opens the fitting menu, as a socket does today.
  The server checks that the point lies inside the face, that the fitting's footprint is clear of parts, railings and
  other fittings, and the reach.
- *Loadout format v2:* an entry is either a socket (as today) or a surface mount: face id, position in frame units,
  yaw. Removing the face despawns its fittings, as removing a part does now.
- Part sockets stay. The fixed spots on authored parts (a gun port, an engine's fuel intake) remain useful next to
  free floor placement.

## Shipyard
- **Frame mode and Parts mode** (one toggle). Parts mode is today's builder. Frame mode works on the current story
  (PgUp/PgDn as today, shown as the see-through level plane):
  - drag a node; drop it on another node to merge them; a modifier drag raises or lowers it within its band, and the
    ghost turns red where a band, headroom or slope rule would break;
  - click empty space to add a node joined to the selected one, and close a loop to create a face;
  - click an edge to split it;
  - click a face to toggle planks or open; the railing tool toggles a railing on an edge;
  - delete mode removes a node, edge or face.
- **Input:** new actions in `Context_Shipyard`, read by `ShipyardUseCase` like the others, never polled keys
  (`INPUT.md`). They are added through `InputAssetBuilder`, and `INPUT_MAP.md` is updated with them.
- **Camera:** the orbit and zoom range grow with the design's bounds, so big ships fit in view.

## Shape of the work
No new assembly: the frame is part of the design format, which ShipDesigns owns. Model, codec, migration, validator,
mesh processor and assembly go in `TinCan.Features.ShipDesigns`; the tools in `TinCan.Features.Shipyard`; surfaces in
`Core/Ship/Sockets/` and `TinCan.Features.ShipSockets`. Every processor and use case ships with EditMode tests:
- edits: merge, split, face maintenance and id stability;
- the validator;
- the mesh processor: counts, determinism (the same frame gives the same bytes) and UV scale;
- stats;
- codec round trips (JSON and binary);
- the v1 golden file migrating to v2.

## Slices
### S1: one deck from a frame
The model (stories, nodes, edges, faces with fill), edits, validator, format v2 with the migration and a v1 golden
file, the binary form, the mesh processor (floor faces and beams only) and assembly on every peer. A built-in design
`Raft.ship.json`: one story, an irregular 12-node deck outline, a helm, an engine and an envelope as parts.
Scenario `FrameDeck` (Test_Shipyard): the Raft flies; host and client report the same hash; the subject stands on a
generated deck far from any part and rides the ship. The shipyard still has no frame tools in this slice: the deck is
authored as JSON.

**Built (2026-10-16): EditMode 1137/1137; FrameDeck passes solo and host + client.**
- *Model* (`Features/ShipDesigns/`): `ShipFrame`, `ShipFrameNode` (story, x, drift, z), `ShipFrameEdge` (railing flag
  stored, not built yet), `ShipFrameFace` (fill), `ShipFrameUnits`; `ShipDesign.Frame`, with ids from one counter
  (`ShipDesign.IdFloor`, so new parts skip frame ids too).
- *Format v2*: `ShipFrameJson` writes the frame section only when a design has one; `ShipDesignMigrationV1` renames
  `nextPartInstanceId` to `nextId`; golden files `v1-golden` (migrates) and `v2-golden` (the test deck); Starter and
  Galleon are rewritten as v2. Network version 2 (`ShipDesignBinaryCodec`); the pinned hash test moved with it.
- *Edits*: `ShipFrameEdit` / `ShipFrameEditProcessor`, carried by `ShipDesignEdit.Frame`. The undo of any frame edit
  restores the whole frame before it; the id counter never goes back.
- *Validation*: `ShipFrameValidator` (bands, drift, dangling nodes, edges, faces, slope, plank overlap) and
  connectivity through the frame (`ShipFrameContact`: a part's cell rests on planks or meets a beam).
- *Hull*: `ShipFrameShape` (shared triangles), `ShipFrameMeshProcessor`, `ShipHullAssembler` builds one plank and one
  beam mesh per story with a mesh collider and rebuilds only stories whose fingerprint changed; the shipyard preview
  shows frames too (same assembler). Stats add plank and beam mass and hull.
- *Content*: `Raft.ship.json` (12-node outline as three plank faces, helm, engine on the stern deck, four masts, the
  envelope), written by **TinCan > Dev > Ship Designs > Build Frame Designs**, which also gives the config its frame
  tuning, materials (`M_ShipDeck`, `M_ShipHull`) and limits.
- *Scenario* `FrameDeck`: the raft flies, both peers build 14+ parts and 2 frame meshes, the subject stands on `Planks0`
  and walks aft across the seam between two plank faces. New probes `ShipFrameBuilt`, `SubjectStandsOn`.

**Deferred, and why:**
- *`pos` in place of `cell`* (decision 9) moves to S4 with format version 3, where parts first leave the grid. S1 only
  renamed `nextId`, the part that matters once frame and parts share ids.
- *Headroom* between stories comes with S3, when there is a second story to have headroom under.
- *Unknown fields inside the frame* are not kept through a load and save (the design's top-level ones are). Revisit if a
  later format adds per-node or per-face fields.

### S2: frame tools in the shipyard
Frame mode with drag, merge, split, add, fill toggle, delete and undo, plus the snap settings.
Scenario `FrameRoundTrip`: scripted frame edits, save, clear, load, the same hash, then launch, and the client sees the
same ship.

**Shipyard UI and camera (developer feedback, 2026-10-16):**
- *A building panel with the parts showing* replaces cycling parts with Q/E (an MVP stopgap). Click a part to select it,
  grouped by category. Frame mode shows its tools in the same panel.
- *A controls panel*: two columns, the key on the left and the action on the right, for the current mode. It is filled
  from the shipyard context's input actions, not typed text, so it stays true when bindings change. It replaces the
  one-line "Controls" HUD value.
- *A free-flying camera* replaces the orbit camera (developer, 2026-10-16: "it will feel familiar"), like the Unity
  Scene view: hold the right mouse button to look around with the mouse; WASD flies along the view, Q/E down and up,
  Shift faster, the wheel moves straight forward and back. Released, the cursor is free for placing and picking (the
  aim ray already starts at the camera, whatever its pose). Levels stay on PgUp/PgDn. The middle mouse button is
  dropped from the camera.
- *The camera starts framing parts and frame together* (today it centres on the parts only, `ShipyardStage`).
- WASD was never bound in the shipyard (checked 2026-10-16: not a regression; the context mutes movement below it).

**Built (2026-10-16): the FrameRoundTrip play run is pending.**
- *Input* (`Context_Shipyard`): Look/LookHold (right button only) replace Orbit/OrbitHold; Fly (WASD), Rise (Q/E),
  Fast (Shift), Mode (Tab), Fill (F) are new; NextPart/PreviousPart are gone. Level Up/Down raise and lower the
  selected node in frame mode. `IInputBindings.DescribeAction` gives an action's keys for the controls panel.
- *Camera*: `ShipyardFlyProcessor` (look, fly, rise, push, frame a ship); the use case owns the pose, the stage places
  it and locks the cursor while looking. A view of a design frames its parts and frame together
  (`ShipHullAssembler.TryGetBounds`).
- *Panels*: `ShipyardPanelPresenter` (pure) and `ShipyardPanelView` (UI Toolkit, right side): mode tabs, the parts
  with rendered pictures (`ShipyardThumbnails`) grouped by category, and the controls table for the mode. Clicks over a
  panel never build. The HUD keeps the design's title, flight and messages; its Controls line is gone.
- *Frame tools*: `ShipyardFramePickProcessor` (node, then edge, then face under the cursor; the floor plane) and
  `ShipyardFrameToolProcessor`. Click the floor to add a node (joined to the selected one), click nodes to join them,
  click an edge to split it, click two corners of a face to split the face; an edge that closes a loop planks it as a
  new face. Drag a node to move it (shown live, committed on release), drop it on another to merge. F planks or opens
  the face under the cursor; X then a click deletes a node, edge or face. Each gesture is one undo step
  (`ShipyardDocument.Commit`). A design without a frame gets its first story at the working level. Node markers, a
  hover outline and the pending edge are drawn by the stage.
- *Scenario* `FrameRoundTrip`: draw a square beside the starter's deck, save, new, load, launch; both peers build the
  frame. Scenario steps `ShipyardMode`, `ShipyardFrameClick`, `ShipyardFrameFill`; probe `ShipyardFrame`.
- *Not yet*: railings are still not built (S3); the frame tools work on story 0 only (more stories in S3).

### S3: stories, walls, railings
Several stories, slanted posts, wall faces, railings, the frame stats term, and stairs (the existing part) between
stories. Built-in `FrameGalleon`: the Galleon's layout redone as a frame, to compare against the voxel Galleon.
Scenario `FrameStories`: walk from the lower story up the stairs to a railed upper deck, on host and client.

**Built (2026-10-16): EditMode 1165/1165; the FrameStories play run is pending (all play runs at the end of the work,
developer's call).**
- *Walls*: a face whose corners span two adjacent stories is a wall (`ShipFrameShape.FaceShape.IsWall`, belonging to
  the lower story), triangulated in its own plane (`ShipFrameGeometry.TriangulateInPlane`) and built as a slab centred
  on it. Floors stay "seen from above".
- *Railings*: posts every 1.2 m and two rails, `ShipFrameTuning.RailHeight` (1 m) high, built with the beams so they
  have colliders.
- *Rules*: a wall spans exactly two adjacent stories; a railing only on a floor edge and not under a planked wall;
  headroom of at least `MinHeadroom` (2.2 m) between a plank floor and the underside of the one above, measured at
  corners and side crossings. The S1 rule "faces lie on one story" is replaced by these. The node-only floor-gap
  bullet from Validation is covered by the floor-to-floor check.
- *Shipyard*: PgUp/PgDn change the working story in frame mode (one above the top starts a new story with its first
  node); joining nodes on adjacent stories makes a post; an edge that closes a wall (a quad or a triangle between two
  stories) planks it. Alt + drag a node raises or lowers it (`HeightPixelsPerUnit`), replacing PgUp/PgDn for height.
  A quick right click deselects (developer, 2026-10-16).
- *Content*: `FrameGalleon.ship.json`: a 6 x 14 m hold under a 9 x 17 m main deck, nine slanted planked walls, the
  main deck as four faces round a 3 x 5 m hatch, railed all round and on three sides of the hatch, three stairs up
  through the hatch, helm, two engines, envelope on masts, two balloons.
- *Scenario* `FrameStories`: from the hold floor, walk up the stairs through the hatch onto the main deck.
- *Not done*: stairs are still a part on the grid (S4 frees parts from the grid).

**Developer feedback (2026-10-16), built:**
- *Selecting a node moved it.* A press on a node started a drag at once, measured on the working story's plane; a
  node off that plane (drifted, another story) or a click off its centre snapped elsewhere and jumped. Now a press and
  release that moves the mouse less than `DragThresholdPixels` (6) is a click (select), and a drag moves the node by
  the cursor's movement on the node's own height (`ShipyardUseCase.DragTarget`).
- *An edge between two corners of a face*: already a click on one corner then the other (it splits the face); the
  pending line now also shows when hovering the second node.
- *Extrude* (G, `ShipyardFrameToolProcessor.Extrude`, `ShipyardConfig.ExtrudeLength` = 2 m): grow the ship through
  what is under the cursor. A wall face pushes out, leaving floor below, deck above and walls beside it, and the old
  face opens; an outer floor edge grows a strip of deck; a floor face gets a story raised on it (walls round it, a
  floor on top). Railings move out with the edge, or come off where a raised wall now stands over them. One undo
  step each.

**Developer feedback (2026-10-16, second round), built:**
- *Open faces hard to see when hovered.* A hovered face also gets a see-through fill, clear of the planks on both
  sides (`ShipyardStage.ShowHoverFill`). Walls showed nothing at first: the fill was inside their centred planks, and
  the pick let a node or edge behind a wall win over the wall. Now the first face hit hides what is behind it
  (`ShipyardFramePickProcessor`).
- *A new node on each of two edges of a face did not split it.* Joining two nodes now splits a face they both corner
  (`ShipyardFrameToolProcessor.Connect`), however they were joined (a node, an edge or the floor).
- *Extruding filled the new faces.* They are open now (F fills them); the face pushed out keeps its fill.
- *Parts would not go on extruded decks.* The shipyard's snap counted only parts as "the ship"; it now also counts
  resting on a plank floor or meeting a beam (`ShipFrameContact`, the validator's rule).
- *Balloons.* They are out of proportion with frame ships: lift moves to a spirit in a cairn
  ([spirit-cairn.md](spirit-cairn.md)).

### S4: parts on the frame
Parts snap onto faces anywhere (frame-unit positions in the shipyard), with connectivity and overlap checked across
parts and frame.

### S5: freeform stations
Surfaces, the loadout format v2, and mounting on a floor in the world. Scenario `SurfaceMount`: mount a cannon on an
open stretch of deck on host and client; removing the face despawns it.

### S6: the big-ship pass
Build the target-size ship (decision 10) and measure it against `performance-budgets.md`: design bytes, frame time to
assemble, draw calls, collider cost, memory. Then chunk large stories if needed, add LOD for distant ships if needed,
and check the camera, the boarding pose and docking at sky islands at that scale.

## Decisions (developer, 2026-10-16)
1. **Hybrid, not replacement:** the frame owns the hull, catalog parts stay for objects, and voxel blocks remain as
   parts.
2. **Story height:** stored per story; the shipyard uses a fixed 3 m at first.
3. **Node freedom:** a node belongs to one story and may drift up or down within that story's band. Floors join only
   within a story, posts are the only edges between stories, and a node keeps headroom to the floors above and below.
   Posts may slant, so a narrower lower story gives a tapered hull.
4. **Band numbers:** `MaxDrift` 0.8 m, `MinHeadroom` 2.2 m, and `MaxFloorSlope` equal to the player's walkable slope,
   in a tuning asset rather than the file.
5. **Position unit:** 10 cm integers, with drags snapping to 0.5 m.
6. **Railings:** on floor edges, refused under a planked wall.
7. **Freeform stations:** floor faces become surface sockets in ShipSockets, and the loadout file moves to version 2.
8. **Damage on frames:** out of scope here. `ship-damage-repair.md` gets a follow-up once S3 lands (damage spots on
   faces, keyed by face id and position).
9. **Names in the file** (an implementation detail): `pos` replaces `cell`, and `nextId` replaces
   `nextPartInstanceId`.

10. **Target size: 120 m long, 30 m wide, 4 stories** (a working figure; the Galleon is about 18 × 13 m). This is the
    biggest ship we promise will run well. It sets `ShipDesignLimits` (how far from the origin a node may sit, and how
    many nodes and faces a design may have), and it is the ship S6 builds and measures against
    `performance-budgets.md`. Raising it later costs a limit and a test.

Still open, and not blocking: why huge. Walking distance for 2–4 crew, the camera and docking at islands all depend
on the answer.

## Risks
- **Face upkeep under merge and split** is the riskiest code. The mitigation is a pure processor with many small
  tests, written first in S1.
- **Generated art looks plain** next to authored parts. Budget an art pass after S3 (plank variation, beam caps,
  trim on railings).
- **Mesh colliders and the player's movement:** check that standing, walking and jumping on generated slabs on a
  moving kinematic ship behaves like the voxel deck, in S1's scenario.
- **Big designs over the network:** a large frame plus parts must fit the design `NetworkVariable` cleanly. Measure
  this in S6, and split the payload if needed.

## Docs to update when built
`CODE_MAP.md` (the ShipDesigns and Shipyard rows, plus "Where does X live": frames and their meshes),
`ARCHITECTURE.md` §7 (hulls are generated from a frame; parts sit on it), `INPUT_MAP.md` (the frame actions),
`NETWORK_TEST_HARNESS.md` (the new scenarios), `modular-airship-builder.md` (a link to this plan),
`ship-fitting-loadouts.md` (loadout format v2).
