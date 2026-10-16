Status: Done (2026-10-16, S1-S4 built at the developer's request; awaiting review. Still open: questions 2 and 4 (beacon); the rest were taken with the recommended option, see "Progress")

# Navigation: a chart, a plotted route and a spherical compass

## Idea
A chart shows points of interest (POIs: the voyage destination, treasure, islands, wrecks) and obstacles (islands,
hazard hotspots). A navigator at the chart table plots the ship's **route**: an ordered list of waypoints that ends at
a POI and bends around the obstacles. Everyone aboard shares the route. A **spherical compass** at the helm points at
the route's current waypoint, including up or down, and moves on to the next waypoint as the ship reaches each one.

This makes the navigator a crew job: they read the chart and plan, and the pilot flies the compass.

## What exists today
- `Features/Voyage/VoyageNetworkMediator.cs` already replicates one target, `Destination`, and
  `IVoyage.MoveDestination` changes it on the server.
- `VoyageBeaconPresenter` (a light pillar) and `VoyageHudPresenter` ("x km to go, port") are built locally on every
  peer from that replicated destination. That is the pattern the compass follows.
- `VoyageRouteProcessor.Bearing` flattens the direction onto the horizontal plane, so the HUD ignores height. The
  compass fills that gap.
- The ship pitches (`Core/Ship/AirshipMovementProcessor.cs`), so the compass works in the ship's own frame, not the
  world's. A sphere mounted on the ship handles that naturally.
- `Stations/` seats a player and runs abilities (cannon, helm). A chart table is another station.
- **Sky islands can be charted anywhere.** `SkyIslandLayoutProcessor` computes each island (position, radius, depth,
  top altitude) from the seed on every peer, without building it. The chart can draw islands far beyond
  `StreamRadius` at no network cost.
- **Hazards have no fixed places yet.** `SkyHazardConfig` spawns hazards in a box that travels with the ship (ahead
  of the bow), so the danger is the same whichever way the ship flies, and steering around it does nothing. Hotspots
  need a change in SkyHazards (question 6). `GasChallenge` gas pockets are placed volumes and could be charted as
  they are.

## Proposed shape
A new feature, `TinCan.Features.Navigation`, with its own installer. It has four parts, kept separate:

1. **Route: shared state, server-authoritative.** Each ship has one replicated route: an ordered list of waypoints
   plus the index of the active one. A waypoint is a point in the world (with an altitude), optionally tied to a POI
   id. Plotting, moving, inserting and clearing waypoints are server requests that check the requester sits at the
   chart table. A server-side rule can also set a route, e.g. Voyage plotting a single waypoint at its destination
   when a voyage begins. It lives on a `NavigationNetworkMediator` (a `NetworkList` of waypoints plus the active
   index).
2. **Waypoint progress: server.** Each tick, the active waypoint counts as reached when the ship comes within an
   arrival radius, **or** crosses the plane through the waypoint perpendicular to the leg. The plane test means a
   near miss still moves on, and the pilot never has to circle back to tag a point. The maths goes in a
   `RouteProgressProcessor` with EditMode tests.
3. **Chart content: sources other features contribute** through `FeatureInstaller.IExtension<...>` or a registry.
   Navigation references none of them:
   - **POIs:** Voyage registers its destination. Treasure, wrecks and so on come later. Each source sets its POIs
     as known from the start or discovered (question 5).
   - **Obstacles:** a footprint (circle or polygon) plus the band of altitude it blocks. Sky islands contribute
     theirs from the seeded layout (no network traffic). Hazard hotspots once they exist (question 6). Gas pockets
     possibly.
4. **Compass: presentation only, on every peer.** Each frame it transforms the direction to the active waypoint into
   the ship's frame (`ship.InverseTransformDirection(target - shipPosition)`) and points the arrow. The arrow is never
   replicated, same as the beacon. The maths goes in a `CompassProcessor` with EditMode tests.

With this split, Voyage stays the win/lose rule. The compass only reads the route, and a route can end at a side POI
that isn't the voyage destination.

## Compass design (developer, 2026-10-16)
The compass sits **at the helm**. It is a glass sphere holding three things:
- **A flat disc fixed to the ship.** It tilts and banks with the deck, so it shows the ship's own plane.
- **A 3D arrow at the disc's centre, pointing at the active waypoint.** Seen against the disc it shows where the
  waypoint is relative to the ship: left or right, above or below the bow.
- **A "water line": a circle that always lies level with the world**, like the surface of liquid in a half-filled
  globe. It stays horizontal as the ship pitches and banks.

How to read it:
- **On course** means the arrow points straight out over the bow and lies in the disc's plane. The ship flies along
  its nose and climbs by pitching (`Core/Ship/AirshipMovementProcessor.cs`), so this holds in every direction:
  left/right and up/down.
- **The arrow against the water line** shows whether the waypoint is above or below the ship in world terms.
- **The disc against the water line** shows the ship's own pitch and bank.

The water line also fixes the "needle seen end-on looks like a dot" problem. The arrow always has a level reference
next to it, so its tilt reads even when it points toward the pilot.

When the route moves to the next waypoint, the arrow swings to it. A short cue (a chime, or a flick of the arrow)
tells the pilot it happened. Otherwise a sudden swing reads as a fault.

**Remaining issue: height at long range.** A waypoint 100 m above the ship and 2 km away is about 3° up. A crisp
water line makes 3° visible, but subtle. If playtests show pilots don't climb until too late, choose one of these:
- Exaggerate the arrow's tilt from the altitude difference, clamped.
- Add a small "climb / dive" marker or the height difference in metres on the disc.

S1 ships the honest angle and decides after a playtest. Routes help here: a navigator can place a waypoint where the
climb should start.

## Chart design
- **Top-down, with altitude as a number.** The chart is a 2D map. Each waypoint carries an altitude the navigator
  sets (question 7). Each obstacle shows the altitude band it blocks, so the navigator can route over or under an
  island, not only around it.
- **What the chart shows:** the ship and its heading, the route as numbered legs (the active leg highlighted), POIs
  (only known or discovered ones), and obstacles.
- **Editing while underway:** the route can change at any time. That is the navigator's job when a hotspot appears or
  the pilot drifts off the line.
- **No pathfinding.** The chart never plots a route by itself; planning is the navigator's skill.

## Open questions (developer)
1. ~~Where does the compass live?~~ **At the helm** (developer, 2026-10-16).
2. **What does choosing a destination POI mean?** This only matters once the chart table exists (S2); S1 points at
   the voyage destination either way.
   - A heading aid only: Voyage keeps its own destination as the win condition, and side POIs are detours.
   - The navigator chooses where the voyage goes: ending the route at a POI moves the voyage's destination
     (`IVoyage.MoveDestination`).
3. ~~Honest or exaggerated elevation?~~ ~~Needle style?~~ **Answered by the compass design above:** honest angle in S1,
   revisited after a playtest.
4. ~~Does the HUD bearing line stay?~~ **Development only** (developer, 2026-10-16). S1 hides it outside dev builds
   or behind a dev toggle. **Still open: the beacon pillar.** Either keep it at the final destination, move it to the
   active waypoint, or drop it.
5. ~~Are POIs known from the start, or discovered?~~ **Configurable** (developer, 2026-10-16). Each POI source sets
   its POIs as known from the start or discovered. Still open for S3: how a POI gets discovered (for example by
   flying within a radius, or spotting it with a spyglass).
6. **What is a hotspot?** Today hazards spawn around the ship wherever it flies, so routing can't avoid them. Options:
   - **Hazard zones:** areas in the world (seeded like islands, or placed) where the hazard field is dense. Outside
     them it is thin or empty. Routing past a zone pays off, at the price of a longer flight (fuel, time). The
     recommended option. It is a change to SkyHazards, its own slice (S4).
   - **Moving hotspots:** storms or flocks that drift across the map. Livelier, but the navigator has to re-plot
     while underway. A later step on top of zones.
   - **Known or discovered?** Same choice as POIs: zones could appear on the chart only once spotted.
7. **How does the navigator set a waypoint's altitude?** Options: scroll while placing, a slider for the selected
   waypoint, or snap to a few altitude bands. Recommended: a slider (or scroll) with the obstacle bands drawn beside
   it.
8. **Do routes need a limit?** For example a maximum number of waypoints, or a cost per waypoint. Recommended: a
   generous cap (around 8) for network size and readability, no cost.

## Slices
Each slice ships with EditMode tests, a scenario (`add-scenario`) in a test-range scene, and a feature-index row in
`.docs/CODE_MAP.md`.

- **S1: route and compass.** `Navigation` assembly, the route state and mediator, the waypoint-progress rule,
  Voyage's destination as the only POI, a one-waypoint route set to it when a voyage begins, the compass at the
  helm (design above). The voyage HUD's bearing line becomes dev-only. Playtestable on its own. Scenario: host and
  client both see the arrow point at the waypoint; a test route of two waypoints advances when the ship reaches the
  first, and the arrow follows.
- **S2: chart table and plotting.** A chart-table station (`Stations/`), a `Context_Navigator` input context
  (`.docs/INPUT.md`), the chart UI: the ship, POIs, the route; placing, moving and deleting waypoints and setting
  their altitude. Server requests validate occupancy. Scenario: the client navigator plots a three-waypoint route and
  the host's compass follows it to the end.
- **S3: islands on the chart and more POIs.** Sky islands as obstacles (footprint and altitude band from the seeded
  layout) and as POIs; then treasure and wrecks. Per-POI discoverability; the discovered set is server state,
  replicated to the crew (question 5).
- **S4: hazard hotspots.** Hazard zones in SkyHazards (question 6), contributed to the chart as obstacles. The
  playtest question: does routing around a zone feel like a good trade against fuel and time?

## Not in scope
- Pathfinding or autopilot: the navigator plans, the pilot flies.
- Several ships with separate routes in one session: the route is per ship from the start, but only one ship is
  tested.

## Progress (2026-10-16)
All four slices are built. The pieces are listed in `.docs/CODE_MAP.md` (feature index, "Navigation"); this section
records what the plan did not settle.

**Decisions taken while building (review them):**
1. **Question 2: a heading aid.** Picking a point of interest ends the route there but does not move the voyage's
   destination: Voyage still wins only at its own destination. One line in `ChartNavigatorUseCase.EndAt` changes this.
2. **Question 4: the beacon stays** as it was (at the voyage's destination). The HUD bearing is development-only
   (`Debug.isDebugBuild`) in `VoyageHudPresenter`.
3. **Question 5, discovery:** by radius. Landmarks are the big sky islands (`SkyIslandConfig.LandmarkMinRadius`, 70 m)
   within `LandmarkCorridor` (1500 m) of the voyage's route, hidden until the ship comes within `LandmarkDiscoverRadius`
   (450 m). The session's destination starts known.
4. **Question 6, hotspots:** hazard zones, fixed per voyage (`SkyHazardConfig`, "Hazard zones"): one every 900 m along
   the route, 220-400 m wide, a 100-220 m band of altitude, up to 250 m to either side of the route and 40 m up or
   down. Inside a zone the field is whole; outside it keeps 35 % of its hazards (`OutsideZoneScale`). Zones are on by
   default and only exist during a voyage, so the cannon's test area is unchanged.
5. **Question 7, altitude:** hold Space (raise) or Shift (lower) on the picked waypoint, 60 m/s.
6. **Question 8, limit:** 8 waypoints (`NavigationConfig.MaxWaypoints`), no cost.
7. **The chart is diegetic:** a 3D chart on the table top, heading-up, centred on the ship, 3 km to the rim (scroll
   zooms 0.5-8 km). Pins and columns stand at their height above the ship. Every crewmate sees it; the navigator looks
   through the table's camera and moves a cursor with the mouse. A click near a leg bends that leg ahead of the
   active waypoint; a click on a pin picks it (hold to drag); a click on a point of interest ends the route there.
8. **Waypoints count as reached** within 60 m (3D) or once the ship has passed the plane through them square to the
   leg. The last one is never passed.
9. **Where the chart table stands:** on Airship_Prefab and the test ship, the mid deck to port at (-2.8, -3.49, -2)
   (please check against the airship's props). On the Galleon, the fitting `navigation.chart_table` on hold plate
   #166 (starboard, forward): a chart room below deck. The compass sits right of the wheel at every helm
   (`NavigationConfig.CompassLocalPosition`).

**Verified:** EditMode 1083/1083. Scenarios, solo and host + client (Lag100): `CompassCourse`, `ChartPlot`,
`ChartIslands`, `ChartHazards` (Test_Navigation), `GalleonChartTable` (cheesed_scene); and, because shared pieces
changed, a full `verify.ps1 -All`: every scenario passes solo and host + client (`ShipyardRoundTrip`'s client once failed
to connect within 45 s and passed on a rerun).

**Needs a human playtest:** whether the compass reads at a glance from the wheel (size, the 3° problem at range);
whether the chart is readable at 3 km with dozens of rocks; whether routing around a zone feels worth its fuel and
time; the table's spot on the airship.
