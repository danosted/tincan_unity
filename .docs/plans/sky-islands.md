Status: Approved (2026-10-04)

# Sky islands: floating rock the pilot has to steer around

## Why
The sky is empty apart from clouds and hazards. Floating islands give the world its look: grassy tops, rock
hanging underneath like an upside-down cone, roots and waterfalls (concept art: the developer's 2026-10-04 references).
They also give the **pilot** a job in the First Voyage loop (`.docs/plans/first-voyage.md`): rock to fly around, which
costs hull when the ship hits it.

## Decisions (developer, 2026-10-04)
- **First slice: obstacles.** Islands are solid; hitting one damages the ship. Landing, docking and walking on islands
  come later.
- **Placed from a seed.** Every peer builds the same islands from the same seed. No network objects.
- **Procedural meshes**, with a stylized shader. Modelled art can replace or decorate them later.
- **A new layout every voyage.** The voyage replicates a layout seed with its start point; the islands rebuild from
  it. Without a voyage, the config's seed is used.
- **A core seam pushes the ship** out of rock: `IAirshipCollisionResponse` in `Core/Ship` (S2).
- **Islands stay still.** Moving islands wait for landing.

## Shape of the feature
`TinCan.Features.SkyIslands` in `Assets/Scripts/Features/SkyIslands/`, with its own installer
(`Resources/Installers/SkyIslandsFeatureInstaller`) and config (`Settings/SkyIslands/SkyIslandConfig`).
References: Core.Domain, Ship, VContainer (Gas from S2). It needs no Netcode reference.

### Layout: a grid of cells over the world, no state
- The world is split into square cells on the horizontal plane (`CellSize`, about 450 m). Each cell's contents
  depend only on a hash of (seed, cell x, cell z): at most one main island (a chance per cell), placed far enough
  inside its cell that it cannot reach a neighbour's, plus small satellite rocks around it. Each island has a
  position (its top within an altitude band around the ship's cruising height), radius, depth and a shape seed.
- Pure `SkyIslandLayoutProcessor` with its own integer hash and generator (no `System.Random`, no `Mathf` noise), so
  every peer and platform computes the same layout, a late joiner included. Nothing needs replicating.
- **Keep-outs.** Islands near the voyage's start (where the ship was when it began) and its destination are left
  out, so a voyage never starts or ends inside rock.
- **The seed and keep-outs come from `ISessionLayout`** (Core.Domain, an actor: `LayoutSeed`, `Origin`,
  `Destination`). The voyage state implements it: on Begin the server rolls a seed (`IRandomSource` stream
  "Voyage", so `-seed` runs repeat) and records the ship's position as the origin; the values replicate as network
  variables. Neither feature references the other. With no session layout (seed 0, or Voyage not loaded) the islands
  use `SkyIslandConfig.WorldSeed` and keep clear of the world origin.
- **Rebuilds are diffs.** Islands are identified by (seed, cell, index). A new seed replaces all of them; a moved
  destination only removes the ones now inside its keep-out.

### Streaming: only the cells near the ship exist
- `SkyIslandStreamingUseCase` (per frame, every peer) works out the wanted islands for the cells within
  `StreamRadius` of the first simulating ship, builds the missing ones (at most `MaxBuildsPerFrame` per frame) and
  removes the rest. No ship, no islands.
- Each island is a plain GameObject under a `SkyIslands` root, built by `SkyIslandBuilder` (behind `ISkyIslandBuilder`).
- **Every peer builds collision** (a low-detail version of the same mesh) so rays and, later, the server's impact
  check agree. Peers without graphics (`Application.isBatchMode`: the dedicated server, bots) skip the visual mesh.

### Mesh: `SkyIslandMeshProcessor` (pure, tested)
- **Top:** a disc whose edge is pushed in and out by noise, gently rolling, with a lip at the rim.
- **Underside:** rings that taper down to a point, with noise for crags.
- Vertex colours carry the material masks (grass, strata, AO), so one shader covers everything.
- The visual and collision meshes come from one function at two resolutions.

### Look: `Assets/Shaders/SkyIsland.shader` (URP, hand-written HLSL like `TargetOutline.shader`)
- Grass where the normal points up and the vertex says top, rock elsewhere, with horizontal strata in warm browns.
  Lit by the main light, with ambient and fog.

### Ship impact (S2): server, `AfterAirship`
- The ship's Rigidbody is kinematic (`Core/Ship/AirshipControllerView.cs`), so physics never stops it. After
  airship movement, `SkyIslandImpactUseCase`:
  1. finds island colliders that overlap the ship's solid colliders (`Physics.ComputePenetration` against convex
     island pieces; no triggers);
  2. pushes the ship out along the penetration and cancels its velocity into the rock, through
     `IAirshipCollisionResponse` (Core/Ship; the movement use case moves the ship at once);
  3. applies `SkyIslandConfig.ImpactEffect` (`GE_IslandImpact`, -Health) to the ship's controller, then waits
     `ImpactCooldown` seconds before it can damage it again;
  4. publishes `SkyIslandHitShipEvent`.
- Hazards: field spawn points inside rock are rejected (S3).

## Slices
1. **S1: islands in the sky.** `ISessionLayout` and the voyage's seed; layout, streaming, mesh, shader, collision on
   every peer (no impact yet). Tests: layout determinism, keep-outs, cell separation; mesh sanity; streaming diff.
   Scenario `IslandsPerVoyage` (`Test_Voyage`): islands stand around the ship on host and client, a voyage begins,
   and both rebuild from the new seed, clear of the ship.
2. **S2: impact.** The core seam, the impact use case, `GE_IslandImpact`. Scenario `IslandRam`: the ship is put
   against an island and driven in; its health drops and it ends outside the rock, on host and client.
3. **S3: in the voyage.** Hazard spawn rejection, density tuned for 3-4 player playtests, perf check against
   `.docs/perf/budgets.json` (`CrewLoad` with islands on).
4. **S4: art pass.** Hanging roots/vines (cards), waterfalls (scrolling-UV meshes plus a mist particle), small trees and
   houses as optional kit props, impostors for far islands.

## Open questions
- None yet; the S1 playtest decides density, sizes and colours.

## Risks
- **Float determinism across platforms** (the Linux dedicated server and Windows clients): the layout uses integer
  hashing; the mesh uses float noise that may differ in the last bits, so colliders can differ by millimetres at
  most. The server's collision is the one that counts.
- **Build cost** when cells stream in, worst on the server, which bakes collision. Spread over frames
  (`MaxBuildsPerFrame`); measure in S3.
- **Scale versus the far clip and fog:** islands hundreds of metres across need the camera far plane and fog
  distances checked against `CloudVisualProfile`.

## Progress (2026-10-04)
S1 and S2 built; both scenarios pass solo and host + client (`IslandsPerVoyage`, `IslandRam`).
- **Layout and streaming** as designed. Streaming measures its radius from the middle of the ship's cell, so the
  islands wanted do not change while the ship crosses a cell. `StreamRadius` is 1000 m: the player camera's far
  plane (`NetworkPlayer.prefab`) is 1000 m, so islands further out would never be drawn.
- **Every peer builds around any live ship**, not only a simulated one: only the server simulates the ship, and the
  first host + client run showed the client building nothing.
- **Collision is convex pieces**, not one mesh: `Physics.ComputePenetration` needs one convex side, and the ship has
  non-convex mesh colliders. Each piece is one wedge (`CollisionWedgeSegments`) of one slab between rings; the hull
  of a wedge fills its crags, so the rock is slightly fatter than it looks there.
- **The push is immediate** (`IAirshipCollisionResponse.Push` moves the ship in the same tick and removes its velocity
  into the surface, with a 0.3 bounce; head on, the engine's speed goes too). Touching (under 2 cm) is not inside.
- **Impact damage is fixed** (`GE_IslandImpact`, -80 of 1000 health, at most once per 1.5 s), not scaled by speed.
- Found on the way: a pooled `MeshCollider` given its mesh while its object is inactive comes back with no mesh;
  the builder activates the object first (`SkyIslandBuilderTests`).
- **Needs a human eye:** the look (the undersides are smooth cones; S4), density and sizes, the colours, and how a ram
  feels from the deck. In `IslandRam` the client's player saw one 93 cm correction when the ship was pushed 3 m in a
  tick; at cruising speed a push is at most about 0.5 m.

## Progress (2026-10-04, later)
- **S3, hazards:** field spawns ask `IWorldObstacleQuery` (Core.Domain; sky islands implement it) and skip spots inside
  rock, trying again 0.25 s later.
- **S3, cost:** the contact and obstacle queries make no physics query unless the builder knows an island within
  reach (`ISkyIslandBuilder.AnyNear`), so a ship in open sky costs nothing.
- **S4, shape (first pass):** undersides are lobed and craggy with ledges between the strata; up to three spires, the
  deepest one the apex; between spires the rock stays high and tucks in late. Roots, waterfalls and trees are still to
  come, and want the developer's art direction.
- **Perf (CrewLoad, 5 container runs, image built from the working tree on top of 1526b28):** every budget holds
  except server `physics_queries.p50`: 272 against a budget of 53 (median 42 of the last passing runs, all from
  1b62b8e). Frame, tick, main-thread and GC numbers are unchanged. Island colliders do not cause it: in the Editor,
  disabling all 1404 of them, or the whole islands root, leaves the query count unchanged, and the server never had
  the ship near an island. The suspects are the two commits since the last perf runs (142c78a, the helm station;
  1526b28, look-based interaction targeting, which adds rays and closest-point checks per player per tick). Open:
  run CrewLoad on 1526b28 alone to attribute it, then re-set the budget or fix the cause.
