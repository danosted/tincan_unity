Status: Draft (2026-11-21)

# Peaceful mode: a persistent sky to explore, gather in, and grow the ship and its spirit

## Why
Getting 3-4 people together for the First Voyage playtest ([first-voyage.md](first-voyage.md)) is hard. The developer
wants to build the game they would play themselves, starting with its calm half. The game has two modes:

- **Peaceful progression:** explore, gather, craft, improve and decorate the airship.
- **Event mode:** a designed or procedural co-op challenge, intense and mechanical. This is what exists today: hazards,
  cannon, damage and repair, fuel pressure, gas squalls, skill checks. It is kept, behind its installers, for later.

This plan covers the peaceful mode.

## Decisions (developer, 2026-11-21)
- **Solo first.** One player can sail, gather and build alone. Co-op joins the host's world.
- **The ship and the spirit lead progression.** They are what gets upgraded, and the upgrades give bonuses to
  traversal, gathering, crew and (in event mode) combat, as passive and active abilities.
- **A persistent world.** One world you come back to, not a new voyage every session.
- **First slice: one calm evening** (below).
- **Materials are for living, not for stats** (developer, 2026-11-21). What you gather is used up: consumables,
  crafting and camping (firewood, tents). Making camp away from the ship and firing up the ship's hearth are part of
  the loop. Progression stays with the ship and the spirit.

## Decisions (developer, 2026-11-22)
- **`cheesed_scene` is the game.** It is the primary scene, the current state of the game, and it runs the peaceful
  mode (P0: a quiet world) so movement and the look can be tried with no event running. This replaces
  `scene_main` and `Profile_Main` in [main-game-state.md](main-game-state.md).
- **Fuel is gathered, not minigamed.** The crystal rack, jerry cans and the flying-can net leave the peaceful game.
  Fuel crystals are mined on the islands and fed to the engine: [fuel-gathering.md](fuel-gathering.md). Refining and
  better gathering are later upgrades.
- **Event mode is entered, not run into.** Challenges are zones in the same world, as in Guild Wars 2. You sail in,
  everyone agrees, the event unfolds there, and leaving the zone takes you out of it:
  [encounters.md](encounters.md). So the event features stay in the game, quiet outside zones.
- **An empty tank is work, not a dead end.** With no fuel, the crew moves the ship by hand-made power (a crank), slowly:
  [fuel-gathering.md](fuel-gathering.md).

## The loop
Sail, then spot something, then grapple onto it or skim it from the cloud. Gather, bring it back aboard, and craft at
the bench. Fit or decorate the ship, and feed the spirit. Then reach new places.

## What exists, and what it becomes
| Exists | Becomes |
|---|---|
| Sky islands (`Features/SkyIslands/`, obstacles, a new seed every voyage) | Places to find and harvest, laid from the world's seed |
| Grappling hook (`Features/GrappleHook/`) | How you get onto islands and pull things in |
| Fuel tank and drain (`Features/Airship/Fuel/`) | Kept; fed with mined crystals ([fuel-gathering.md](fuel-gathering.md)) |
| Crystal rack, jerry cans, net and flying cans (`Features/Airship/Fuel/`, `Minigame/`) | Not in this profile (event mode); maybe cloud-sea skimming later |
| Designed events, hazards, cannon, gas, skill checks | Kept, but they run only inside an encounter zone ([encounters.md](encounters.md)) |
| Ship sockets (`Features/ShipSockets/`, choose from a menu) | Where crafted parts and decorations go |
| Spirit cairn (one lift value, `ShipStatsTuning.SpiritLift`) | The spirit that levels up: lift caps ship size, unlocks give abilities |
| Cloud sea, day cycle, cloud flight, atmosphere | The world and its mood; the reward of sailing |
| Voyage (`Features/Voyage/`, A to B, win or lose) | Out of the game profile (it rerolls the islands); maybe a kind of zone event later |
| Ship design and fitting files (`FileShipDesignStore`, `FileShipFittingLoadoutStore`) | The pattern the world save follows |

## Persistence: the world save
The world is saved on the host as JSON files under `persistentDataPath/Worlds/<world>/` (files, not a database). It holds:
- the world seed, which lays out the islands and the sea;
- the time of day;
- the ship: its design, its fittings, and where it is;
- the hold, meaning what the ship carries;
- the spirit: its level, what it has been fed, and its unlocks;
- what has been harvested, so a harvested node stays empty until it grows back.

It saves on a timer and when the host leaves, and loads on host start. Clients get the state the way they get it
today, as replicated state; they never write the save. Each feature saves its own part through one save seam in the
core, the way installers contribute today, so no shared file grows a field per feature.

## Upgrades: one shape, two sources
An **upgrade** is data, shaped like an item: always-on effects (the passives), granted abilities (the actives), and
who receives them (the ship or each crew member). The GAS layer applies and revokes it the way equipment does today.
No new ability engine is needed.

- **The ship's progression is what is built onto it.** A crafted part or fitting carries upgrades; taking it off
  removes them. Sails give a cruising-speed passive; oars give an active burst of speed.
- **The spirit's progression is what it has learned.** You feed it what you gather. Its level sets lift (a hard cap on
  ship size, the existing `TooHeavy` rule), and it unlocks upgrades in a few themes.
- **They meet through tags.** Parts grant tags (`Ship.Has.Sails`), and spirit abilities require them
  (`ActivationRequiredTagsOnActor`): Gale needs sails.
- **Solo-friendly triggers.** Ship actives are used from the helm; crew actives from the player's own body.

Example upgrades:

| | Passive | Active |
|---|---|---|
| Traversal | more lift from diving, less glide drag, cheaper climbs | Gale (speed burst), Updraft (climb), Cloud Dive |
| Crew | longer grapple, softer falls | spirit glide when you fall overboard, double jump on deck |
| Gathering | net reach, more from each catch | Spirit Sense (resources on the chart) |
| Combat (event mode) | hull armour, cannon reload | spirit shield |

The ship needs more attributes for these to change than `MoveSpeed` and `TurnSpeed` (`Core/Gas/AirshipAttributeSet.cs`):
lift, dive gain, glide drag, climb cost. Cloud flight (`Features/CloudFlight/`, `Core/Ship/IAirshipFlightForces.cs`) is
where they would be read.

## First slice: one calm evening
The aim is to find out whether the loop is fun before building it out. Each step is its own feature with a test and a
scenario, per `AGENTS.md`.

- **P0 A quiet world in `cheesed_scene`** (changed 2026-11-22: one profile, not a second one). `cheesed_scene` keeps
  its one game profile, and the world in it is quiet:
  - The fuel minigame (crystal rack, jerry cans, flying cans, net) and the voyage leave the profile.
  - The event features stay in it, but nothing starts by itself: no auto-started events, no ambient hazards or random
    breakage outside zones (`encounters.md`, E0).
  - The Editor builders that wire `cheesed_scene` or treat `Profile_FuelSandbox` as the main profile
    (`ShipSocketsAssetBuilder`, the `*AssetBuilder`s listed in `main-game-state.md`) must not put the minigame or the
    voyage back on a rebuild.
  - The five scenarios that run in `cheesed_scene` move out of it (below), and the rule that scenarios run only in
    test-range scenes (`AGENTS.md`, developer 2026-11-22) gets its EditMode test: every `ScenarioCatalog` entry
    names a scene under `Assets/Scenes/Test/`.

  **The scenarios in `cheesed_scene`** move to a new test area, `Test_Galleon`: the Galleon built from its design and
  loadout files, with a `Profile_Test_Galleon` that loads only what these scenarios use.

  | Scenario | What it does | After P0 |
  |---|---|---|
  | `GalleonLeashClimb` | overboard, hang on the winch's leash, climb back | to `Test_Galleon`, unchanged |
  | `GalleonChartTable` | take the chart table and plot a waypoint; starts a voyage first to get a route | to `Test_Galleon`; the voyage step goes, and it plots without a destination |
  | `GalleonSternCannon` | take a stern cannon and stay aboard | to `Test_Galleon`, unchanged |
  | `GasSquallEvent` | starts the gas squall and checks every peer sees it | to `Test_Galleon`, started by command; the zone scenario (`encounters.md`) covers the in-world start |
  | `GalleonFuelLoop` | pour a jerry can, catch a can with the net | retired with the minigame; `CrystalRun` replaces it |
- **P1 The ship waits for you: the anchor** (developer, 2026-11-22). Today, with nobody at the helm, the engine winds
  down and in open air the cloud flight lets the ship sink to rest on the cloud sea, so it drifts away from a high
  island. The fix is a crew action, not a rule: drop the anchor and the ship holds where it is.
  - **The anchor** is a socket fitting (`ship.anchor`, a winch with a chain), mounted in the Galleon's loadout near the
    helm. Interact drops it or raises it; both take a moment, with the chain's rattle as the cue.
  - **Anchored**, the ship holds still at its position and height: no engine thrust and no cloud-flight forces, and
    the helm shows that it is anchored. Raising the anchor gives the ship back to the helm and the cloud.
  - **In the code:** the movement needs a hold that features can ask for. That is a small core hook beside
    `IAirshipFlightForces` (`Core/Ship/`), so the anchor (Features) never touches the movement itself.
  - **Tests:** EditMode for the anchor's state and for the movement holding while anchored; a scenario in
    `Test_Galleon`: drop the anchor, leave the helm, the ship holds for some seconds on both peers, raise it, steer.
  - Open: does the anchor hold anywhere (simplest; the spirit holds the chain), or only within reach of island rock
    (a mooring line)? Recommended: anywhere, for now.
- **P2 The world save.** Seed, time of day, ship (design, fittings, position) and hold, as above. Islands are laid
  from the world seed instead of the voyage's.
- **P3 Fuel from the islands** (was "the hold and one resource, gathered two ways"; changed 2026-11-22). Fuel
  crystals mined from veins on the islands and fed to the engine: [fuel-gathering.md](fuel-gathering.md), G1 to G3.
  The net skimming and the hold are not in the slice.
- **P4 The bench.** A crafting station (one of the existing stations) with one recipe: the resource becomes one part
  you can see, mounted in a socket, that carries one passive upgrade (for example a sail: cruising speed).
- **P5 The spirit grows.** Now [spirit-talents.md](spirit-talents.md): the spirit as ship state, its talent tree
  learned at the cairn, and its look growing with it.

Done when a solo session goes: cast off, gather, craft the sail and fit it, feed the spirit, quit, come back, and
everything is still there.

## Direction (developer, 2026-11-22)
The priority order. Each step is playable on its own, and each makes the next one worth having.

1. **A quiet world in `cheesed_scene`** (P0, with the scenarios moved out to `Test_Galleon` and the test-range rule
   enforced), then **the ship waits for you** (P1). Goal: sail, walk and judge the look with nothing going off.
2. **Fuel from the islands** ([fuel-gathering.md](fuel-gathering.md) G1 to G4, the crank included), with the crystal
   bin ([ship-shield.md](ship-shield.md) SH3). Goal: a reason to land, and an empty tank that is work, not a dead end.
3. **The storm lab** ([storm-crossing.md](storm-crossing.md) ST1), alongside 1 and 2: clouds, light and sound tuned
   by eye. It needs no gameplay, so it can start at any time.
4. **The encounter zone** ([encounters.md](encounters.md) E1): bounds, the warning cue, entering and leaving.
5. **Ray cannons** ([ray-cannons.md](ray-cannons.md) R1, R2).
6. **The spirit levels up and wards the ship** ([spirit-talents.md](spirit-talents.md) T1 to T3, `ship-shield.md`
   SH1, SH2).
7. **The storm in the world** (`storm-crossing.md` ST2 to ST5): gusts, debris, the heart and its cure, then tuning.
8. **Later:** the world save (P2), the bench (P4), the spirit's look pass (`spirit-talents.md` T4), refining, better
   gathering, more talent branches.

## Materials
Gathered things are spent, the way a camping trip spends them: wood burns, food is eaten, cloth becomes a tent. They
are carried in your hands, one at a time, like the crystal and the net (there is no inventory yet). The first set:

| Material | Where | Used for |
|---|---|---|
| Firewood | the canopies of island trees, which grow back in time | a campfire (tied into a bundle and set down, then fed), the ship's hearth |
| Tent roll | the basket beside the hearth | set down as a tent on flat island ground, packed up again to take along |
| Fuel crystal | veins in island rock and caves, which grow back in time | fed to the engine ([fuel-gathering.md](fuel-gathering.md)) |

Next candidates, in the same spirit: food (cooked at a fire), water, cloth and rope (to make tent rolls and repairs),
lantern oil. The ship and spirit progression and the world save are unchanged by this.

## Progress
- **2026-11-21, camping (built, awaiting play runs and playtest).** `Features/Camping/` and `Features/IslandCamps/`
  (feature index in `CODE_MAP.md`). Every main sky island gets a campsite: deadwood, a fire ring and a tent spot. A
  log from the deadwood lights the campfire for 3 minutes (up to 10). The hearth fitting (`camp.hearth`, mounted from
  a socket's menu, for example on the Galleon's free cabin-floor plate) burns the same way, and its basket hands out
  tent rolls. Camps last as long as their island stands; they are not saved. Models are primitives in the look's
  materials. Scenario `CampLoop` in `Test_Camping`.
- **2026-11-21, island life (built, awaiting play runs and playtest).** [island-life.md](island-life.md): trees that
  give firewood, boulders and bushes, landmasses, and cave rocks. Scenario `IslandForage` in `Test_Islands`.
- **2026-11-22, camps are made, not found (developer).** The pre-placed campsites and `Features/IslandCamps/` are
  gone, and so is the `Test_Camping` area. A log is tied into a campfire bundle (Primary); a bundle or tent roll is set
  down on flat island ground where a ghost shows it fits (Primary). `CampLoop` now runs this in `Test_Islands`.
- **2026-11-22, P0 a quiet world (built; EditMode 1674/1674; `GalleonLeashClimb`, `GalleonChartTable`,
  `GalleonSternCannon` and `GasSquallEvent` pass solo and host + client in `Test_Galleon`; awaiting a playtest in
  `cheesed_scene`).**
  - `Profile_Galleon` (cheesed_scene) is the game: built by `ShipSocketsAssetBuilder.GameProfile` from the sandbox's
    installers, without the voyage and the flying cans, with the new Encounters feature (`EncounterPressureUseCase`:
    the event features' pressure off, `encounters.md` E0).
  - The Galleon's loadout no longer mounts the crystal rack and the net rack. The receiver stays: until fuel
    gathering, Interact on it empty-handed refuels (`DebugFreeRefuel`).
  - `GalleonLeashClimb`, `GalleonChartTable` (now plots on an empty route), `GalleonSternCannon` and
    `GasSquallEvent` run in the new `Test_Galleon` (`Profile_Test_Galleon`: the core test range plus the game).
    `GalleonFuelLoop` is retired.
  - The rule is a test: `ScenarioSceneTests.EveryScenario_RunsInATestRangeScene`.
- **2026-11-22, P1 the anchor (built; EditMode 1680/1680; `AnchorHold` passes solo and host + client in
  `Test_Galleon`; awaiting a playtest).** The placeholder chain runs inboard across the deck (the quarterdeck plate
  faces in); it should hang over the rail.
  `Features/Anchor/` and the core hook `Core/Ship/IAirshipHold.cs` (feature index in `CODE_MAP.md`). The `ship.anchor`
  fitting is on the Galleon's quarterdeck, port stern plate; Interact drops or raises it at once (no delay or sound
  yet), and its chain shows while down. Held, the ship's speed, momentum and velocities are zeroed, so it starts from
  rest. It holds anywhere (the recommended default; the open question stands). Not built: the helm showing it is
  anchored. Known: the cloud flight keeps each ship's last fall rate, so a ship anchored mid-air may start falling at
  its old rate when raised; the cloud-flight session will handle that if it shows.
- **2026-11-22, after the first playtest (developer feedback, built; EditMode 1716/1716; `LadderClimb`, `AnchorHold`, `CrystalRun` and `GalleonLoadout` pass solo and host + client).**
  - **The engine was far too fast.** The Galleon is back to 15 m/s (acceleration 5), as before the cloud flight's
    doubling, as an override on `Galleon_Prefab` only (`Airship_Prefab` is not touched; the classic test ship keeps 30).
  - **No way back aboard without a grapple** (the Galleon has no grapple rack, and hands hold one thing, so a crystal
    and a hook never go together). Decided: **a rope ladder with the anchor.** While the anchor is down a 13 m ladder
    hangs over the port rail; forward climbs, back goes down, nothing hangs still, jump lets go, and hands may be full.
    At the top you are set down on deck. The ship holds still while anchored, so the climb predicts like walking
    (`LadderClimbUseCase`, `LadderLandingUseCase`). The anchor's plate now faces port, so the chain hangs over the side
    too (the inboard chain above is fixed). Scenario `LadderClimb`. Not yet: a ladder that reaches the ground below,
    whatever the height; climbing down onto it from the deck; catching someone who falls overboard near it.
  - **A loadout picker** was raised and set aside: it needs a second hand or an inventory first.
  - **Knowing what you carry:** the HUD names what you hold and what Interact does on the target in reach
    ([fuel-gathering.md](fuel-gathering.md), progress).

## Open questions (for the developer)
1. **Are actives limited?** Everything unlocked is usable at once (simplest), or the spirit has a few attunement
   slots to choose from (real choices, fewer buttons).
2. ~~How does the spirit grow?~~ **Answered (developer, 2026-11-22):** a talent tree, and its growth shows on the
   spirit: [spirit-talents.md](spirit-talents.md).
3. **Can unlocks be undone?** Freely, at a cost, or never.
4. **Saves:** one world, or named worlds to choose from?
5. **What does a lit fire give?** Today it is light and mood only. Candidates: a place to rest or sleep through the
   night, a respawn point, cooking, a spirit that likes a warm hearth.
6. **First Voyage:** mark it parked while this plan runs?

## Not in this plan
Spirit unlock themes, decoration beyond one part, more resources and recipes, event mode, and co-op ownership of what
the crew gathers. These get their own plans once the slice has been played.
