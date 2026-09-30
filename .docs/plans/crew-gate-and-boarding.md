Status: Done (2026-09-30)

# Crew gate and boarding

## Goal
Two session rules that a dedicated server needs, but that also hold on a listen host:

1. **A voyage only runs while there is a crew.** Today `VoyageUseCase` starts as soon as the ship and its state
   fixture exist (`VoyagePhase.Idle when _config.AutoStart`). On a dedicated server with nobody aboard it briefs, casts
   off, loses, and then waits on an end screen nobody can dismiss.
2. **A player who spawns while a ship exists boards it.** Today every player spawns at the player prefab's position
   plus 40 m (`NetworkPlayerSpawner.SpawnPlayer`), so a late joiner lands away from the ship.

Neither rule checks "is this a dedicated server". Both are written as rules about the crew, so the host (whose own
player is the crew) keeps its current behavior.

## Decisions
1. **Crew = player objects, read through the actor registry.** A crew member is a humanoid spawned with
   `SpawnAsPlayerObject`, meaning `NetworkObject.IsPlayerObject`. Ownership alone is the wrong filter: on a host, the
   host's player and the server-owned ship both have owner id 0. Bots (`-bot`) drive real player instances, so they count.
   - `IHumanoidActor` (`Core/Domain/ActorKinds.cs`) gains `bool IsPlayerCharacter { get; }`, so features stay free of NGO.
   - `HumanoidPlayer` implements it as `IsSpawned && NetworkObject.IsPlayerObject`.
   - A static helper `CrewQueries.CrewCount(IActorRegistry)` in `Core/Domain` counts them. `SkyHazardUseCase` already
     scales by `GetActors<IHumanoidActor>().Count()` and switches to the helper, so both features agree on who counts.
2. **The voyage waits for a crew and stands down without one.**
   - `VoyageConfig.MinCrew` (default 1). `Idle` + `AutoStart` begins only when `CrewCount >= MinCrew`. With the default,
     a host starts the moment its own player exists, as today.
   - Crew drops to 0 during `Briefing` or `Underway`: release the pressure (`SetSessionActive(false)`) and go back to
     `Idle`. The next player to join gets a fresh voyage (a full reset through `Begin`).
   - Crew drops to 0 on an end screen (`Arrived` / `Lost`): also back to `Idle`, so an empty server does not sit on
     "Lost".
   - Players who join during a crewed voyage join it in progress (unchanged).
   - A log line on each stand-down (`Voyage`: "crew left, standing down").
3. **The boarding point is a ship concept.** `IAirshipRespawnPoint` + `AirshipRespawnPoint` move from
   `Features/CloudBoundary/` to `Core/Ship/` (assembly `TinCan.Ship`). The pose resolution now in
   `CloudBoundaryUseCase.ResolveRespawnPose` moves to a pure `AirshipBoardingPose` helper in the same place: the ship's
   respawn point, else a fallback offset relative to the ship. CloudBoundary calls the shared helper and passes its
   `FallbackRespawnOffset`. Behavior is unchanged.
4. **A new feature places joiners on the ship.** `Features/Boarding/` (assembly `TinCan.Features.Boarding`, installer
   `BoardingFeatureInstaller`). `BoardingUseCase` is a server-only simulation tickable. It remembers which player
   characters it has already seen (by actor `Id`). When a new one appears and a simulating ship exists, it resets the
   character to the nearest ship's boarding pose through `IHumanoidRespawnService.ResetCharacter`, the same path as the
   cloud reset, so owner prediction is corrected the same way. If no ship exists yet, the character stays where it
   spawned and is not revisited. This covers a host's first spawn in scenes where the player appears before the ship.
   - The spawner itself is not changed. It lives in `TinCan.Network`, below any feature, and the rule belongs to the
     feature that can see ships.
5. **The boarding spot is a config offset, not a prefab edit.** `Airship_Prefab.prefab` is on the "Do NOT touch"
   list, so no `AirshipRespawnPoint` is added to it. `BoardingConfig` holds a ship-local offset, defaulting to the
   value of `CloudBoundaryConfig.FallbackRespawnOffset`, so both features land players on the same spot. An
   `AirshipRespawnPoint` placed on a ship later still wins over the offset, for both features.

## Pieces
- Core:
  - `IHumanoidActor.IsPlayerCharacter` and `CrewQueries` (`Core/Domain`).
  - `HumanoidPlayer.IsPlayerCharacter` (`Network/Infrastructure`).
  - `IAirshipRespawnPoint`, `AirshipRespawnPoint` and `AirshipBoardingPose` move into `Core/Ship`. Keep the component's
    script GUID when moving (move the `.cs` together with its `.meta`) so existing references survive.
- Voyage: `VoyageConfig.MinCrew`; `VoyageUseCase` gets the crew gate and the stand-down.
- SkyHazards: crew scaling uses `CrewQueries`.
- CloudBoundary: uses `AirshipBoardingPose`; `TinCan.Features.CloudBoundary` keeps its `TinCan.Ship` reference.
- Boarding: new assembly (`TinCan.Core.Domain`, `TinCan.Humanoid`, `TinCan.Ship`, `VContainer`), `BoardingUseCase`,
  `BoardingConfig` (+ asset in `Assets/Settings/Boarding/`), `BoardingFeatureInstaller` + its asset under
  `Assets/Resources/Installers/`, added to `Profile_FuelSandbox` and `Profile_Test_Voyage`.
- Docs: a row for Boarding in the `.docs/CODE_MAP.md` feature index; `IAirshipRespawnPoint`'s new home in "where does
  X live"; a line in `.docs/plans/voyage-session.md` pointing here for the crew gate.

## Tests
- EditMode:
  - `VoyageUseCaseTests`: does not begin with 0 crew; begins at `MinCrew`; stands down to `Idle` and releases the
    pressure when the crew leaves during `Briefing`, `Underway` and an end screen; begins fresh when a player
    rejoins.
  - `CrewQueriesTests`: counts only player characters.
  - `AirshipBoardingPoseTests`: uses the respawn point when present, else the fallback offset.
  - `BoardingUseCaseTests`: a new character with a ship is reset once; a character seen before is not reset again; no
    ship means no reset and no later reset; client side does nothing.
  - `CloudBoundaryUseCaseTests` stay green unchanged.
- Scenarios (one confirmed play run at the end, solo and host + client):
  - `VoyageLoop` still passes (host crew = 1 meets `MinCrew`).
  - New `LateJoinBoarding` in `Test_Voyage`: the host is aboard, the client joins late (`-joindelay`), and the scenario
    asserts that the client's character is within a few metres of the ship's boarding point.

## Build order
1. `IsPlayerCharacter` + `CrewQueries` + tests; SkyHazards switches to the helper.
2. Voyage crew gate + stand-down + tests.
3. Move the respawn point into `Core/Ship`, add `AirshipBoardingPose` + tests; CloudBoundary uses it.
4. Boarding feature + config + installer asset + tests; add it to the profiles.
5. Compile, run the EditMode tests, then ask for the play run (`VoyageLoop`, `LateJoinBoarding`).
6. Update the docs listed under Pieces.

## Progress
- 2026-09-30: steps 1–4 and 6 built. Compile and EditMode suite green (657/657). Assets created in the Editor:
  `Settings/Boarding/BoardingConfig`, `Resources/Installers/BoardingFeatureInstaller`, added to both profiles.
  `AirshipRespawnPoint` now has its own file (Unity only attaches a MonoBehaviour from a file named after it).
  Scenario `LateJoinBoarding` written (`DevTools/Scenarios/BoardingScenarioLibrary.cs`, probe `SubjectAboard`).
- 2026-09-30: play run green: `VoyageLoop` and `LateJoinBoarding`, solo and host + client. The first run showed the
  probe passing on the host before the client was moved: in the test range the spawn point is straight above the
  ship's origin, so the level distance was 0 either way. `SubjectAboard` now also rejects a player above the
  boarding point; both peers then read the boarded pose (0.0 and 0.1 m).
- Boarding also moves the host's own player when the ship exists before it (the ship spawns on server start), so
  every player starts on deck, not only late joiners.

## Risks and open questions
- **Scenarios that expect an immediate voyage on the host.** Scenario setup may start before the host player registers
  as an actor. With `MinCrew = 1` the voyage then starts a few frames later, not at once. Check `VoyageLoop`'s first
  wait.
- **Stand-down vs. a brief disconnect.** Going back to `Idle` the instant the last player drops discards the voyage. A
  grace period (e.g. 10 s) could come later if reconnects matter; not in this slice.
- **Where the boarding point sits.** The cloud reset's offset is the starting value. It must be on the deck, clear of
  stations and racks; confirm in a playtest and tune `BoardingConfig`.
- **Joining mid-voyage while the ship moves at speed.** The reset puts the character on a moving deck; the cloud reset
  does the same today, so it should hold, but it needs a human playtest at full throttle.
- **Out of scope:** the `-server` flag, headless presentation guards and the container build:
  [`dedicated-server-container.md`](dedicated-server-container.md).
