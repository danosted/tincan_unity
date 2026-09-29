Status: Approved (2026-09-29)

# Next chapter: "First Voyage", a playable session to test fun

## Context
TinCan has four crew jobs: **pilot** (possessed airship), **gunner** (cannon station, sky hazards),
**repair** (random part breaks → `GE_HullBreach` fuel leak, repair tool) and **fuel runner** (flying cans, net,
jerry can → motor). They work, but they don't pressure each other:
- Hazards are harmless: cannon S2 ("hazards damage the ship") isn't built.
- Breakage comes from a random timer (`ShipBreakageUseCase` AutoBreak).
- There's nothing to win or lose. The designed-events plan was parked on "What is a session?".

You can't judge fun without stakes. This chapter adds no new jobs. It links the existing ones into one loop with a goal,
a way to lose and a quick restart, then gets 3-4 people playing it on a build.

**Decisions (developer, 2026-09-29):** a session is a **voyage from A to B**. Playtests have **3-4 players**
(one per job). The crew **loses when hull integrity hits 0**. The chapter also covers a **playtest build**, a
**feel/juice pass** and **end-of-run stats**.

The loop we're testing: a hazard drifts in → the gunner shoots it, or the pilot steers around it → if it hits, a part
breaks and hull integrity drops → the broken part leaks fuel and keeps draining hull → the repairer fixes it, and the
runner catches cans and pours to keep the ship flying → the ship reaches the destination (win) or the hull fails (lose)
→ end screen with stats → Restart.

## Chapter layout
The chapter is a roadmap. Each slice gets its own plan in `.docs/plans/` (with a `Status:` line) and its own
scenario, per `AGENTS.md`. This roadmap answers open question 1 in `.docs/plans/designed-events.md`.

### Milestone A: first playable voyage (then playtest)
**V0 Playtest build (independent; do it first or alongside).** Unblock the StandaloneWindows64 build. Blockers
recorded in `ship-tasks-prototype.md`:
- Visual Scripting `AotStubs.cs` needs Physics 2D. Enable the module, or remove Visual Scripting if nothing uses it.
- `Fantasy Skybox FREE` sample terrain fails at build time. Delete the sample scene folder.
- The Addressables "Debug Build Layout" modal blocks unattended builds.

The build should host or join through the existing menu (`CommandLineSessionBootstrap` handles `-autohost`/`-autojoin`).
Done when two builds on two machines join over LAN.

*Progress 2026-09-29:*
- The first two blockers were already gone: Visual Scripting was removed in `e85b78f`, and the skybox sample scenes
  were deleted.
- The Addressables dialog asks once per machine; the answer is stored under `Library/`. Answer **No**.
- `unity cmd build --target StandaloneWindows64 --outputPath Builds/Win64/TinCan.exe --confirm true` succeeded
  (0 errors). The output folder is gitignored.
- Open: the LAN join test on two machines (human).

**V1 Hazards hit the ship (built 2026-09-29).** Developer decision: **a hit lowers the ship's health.** It does not
break a part; breaking the nearest part raised too many questions about what that means for the game state. The
airship's existing `HealthAttributeSet` (`AirshipNetworkMediator`, 1000 max) is the hull.
- `SkyHazardUseCase`:
  - field hazards home on the ship (`HazardDriftProcessor`, `DriftSpeed`);
  - any hazard that touches it (`IShipContactQuery` → `PhysicsShipContactQuery`) applies `GE_HazardImpact` (−50 Health)
    to the ship's controller, counts `Hits`, publishes `SkyHazardHitShipEvent`, and despawns.
  - Targets placed with `SpawnAt` stay still, so `CannonShot` is unchanged.
- The hazard prefab has a `NetworkTransformMediator`, so clients see the drift.
- First tuning (`CannonAssetBuilder`): one hazard every 6 s, at most 4 alive, drifting at 4 m/s from 30–110 m to
  starboard. 20 unanswered hits sink the ship.
- Tests in `SkyHazardTests`; scenario `HazardStrike`.
- No `Features` assembly references another: SkyHazards applies the effect through GAS.

**V2 Hull display and the fail state.** The hull is the ship's `Attr_Health`; no new attribute.
- Done 2026-09-29: a screen HUD meter "Hull" for everyone (`ShipHealthHudPresenter` in the Damage feature; meters are
  new in the headless HUD, `UI_FRAMEWORK.md`). Developer's choice: on screen, not in the world.
- `HullFailedEvent` fires at 0 (it feeds V3's Lost phase).
- Open: should an unrepaired broken part also drain health? Today broken parts only leak fuel, which keeps repair
  tied to fuel instead of the hull.

**V3 Voyage session (new feature `TinCan.Features.Voyage`, installer in `Profile_FuelSandbox` plus a test profile).**
- `VoyageConfig`: route length, destination.
- A pure `VoyageProgressProcessor`: progress = distance covered toward the destination.
- `VoyageUseCase` (server): phases `Briefing → Underway → Arrived | Lost`. Win on arrival, lose on `HullFailedEvent`.
- `VoyageNetworkMediator` replicates the phase and progress, so late joiners and the HUD see them. This fixes the
  POC's "event state isn't replicated" limit for the session level.
- Pilot guidance: a destination beacon or compass and a progress line on the HUD.
- End screen through the menu framework (`Core/UI`, `MenuDefinition` + `IMenuCommand`), with a **Restart** command.
  The server resets the session in place: ship pose, fuel, supply, hull, sockets, hazards and cans.
- Scenario `VoyageWinLose`: force the progress → Arrived; force the hull to 0 → Lost; Restart → Underway with full
  values.

→ **Playtest A** with 3-4 people on V0 builds. Main question: do the jobs pull on each other, and is it tense or
boring? Record the findings in the chapter plan before starting milestone B. The findings reorder B.

### Milestone B: tune what the playtest reveals
**V4 End-of-run stats.** A server-side `VoyageStatsUseCase` counts from existing events on the event bus:
- time taken;
- hazards shot and hazard hits;
- parts broken and repaired;
- cans caught and fuel poured;
- lowest hull.

The mediator replicates them to the end screen. Build this first in B: it makes comparing tuning runs cheap.

**V5 Pacing by progress.** Replace the random breakage timer and flat hazard density with curves over voyage progress
in `VoyageConfig`: a calm start, a dangerous middle, a final push. Optionally, the parked `HullStress` designed event
fires in the middle leg. Scaling with crew size can wait until a 2-player playtest asks for it.

**V6 Feel/juice.**
- Gameplay cues (`Abilities/Cues/`, `GameplayCueNotify`) for cannon fire, hazard impact, part break, repair done,
  can caught, and hull low.
- Cannon S3: crosshair, reload line, owner-predicted muzzle flash.
- Screen shake on impact.

## Known issues, left for input contexts (next session)
Found 2026-09-29. The developer's decision is not to patch these symptoms. They go away with **input contexts**: data
mappings that GAS pushes while you occupy a station, like Unreal's Enhanced Input mapping contexts (Unity's Input
System action maps). The mouse would then drive the station's aim, sent in the predicted input, instead of the body's
look. Plan it in its own session.
- **The gunner's body turns with the aim:** `HumanoidMovementUseCase` turns the body to the look every tick.
- **The cannon's yaw wraps at 180°:** `CannonAimProcessor.BarrelAngles` clamps a signed angle, and the look itself is
  never limited.
- Input today: hardcoded keys in `UnityInputService` plus one global `InputBindingConfig`.

## Reuse (don't rebuild)
- `IShipBreakage` (`TryRestore`, `BrokenCount`) for Restart.
- `ISkyHazards` (`Hits`, `Destroyed`, `FieldEnabled`) for Restart, pacing and stats.
- The `FuelAttributeSet` + `FuelTankNetworkMediator` pattern for hull integrity.
- The menu/HUD framework (`MenuUseCase`, `IHudValues`) for the end screen, HUD progress and Restart.
- The event bus (`IEventPublisher`, `FuelEvents`, `JerryCanCaughtEvent`) as the source for stats.
- The DesignedEvents POC, for V5 only.

## Verification
- Per slice: compile clean, EditMode suite green, the slice's scenario passes `.\.tools\verify.ps1 -Scenario <Name>`
  solo and host + client. Ask before play runs, and batch them.
- Milestone A done: a 3-4 player LAN session on builds can win and lose a voyage and restart without relaunching.
  Late joiners see the right phase, progress and hull.
- `CODE_MAP.md` feature index rows for Voyage and hull integrity, in the same change.
