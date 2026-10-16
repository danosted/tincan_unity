Status: Approved

# Overboard and rescue

## Why
Falling off the ship costs nothing today. `Features/CloudBoundary/CloudBoundaryUseCase.cs` (`ResetFallenCharacters`)
teleports a player who sinks `CharacterResetDepth` below the clouds straight back to the ship, keeping what they carry.
So nothing gives the grappling hook, a rope ladder or a crew rescue a purpose. This plan makes falling matter and gives
the crew a way to save each other. It follows `.docs/plans/grapple-crew-tow.md` (the options matrix, decided here) and
builds on `.docs/plans/grapple-hook.md`.

## Decisions (developer, 2026-10-15)
- **Falling overboard asks the player.** Respawn aboard and lose what you carry (option O1 in the crew-tow plan), or
  hang on a leash and get dragged behind the ship until the crew pulls you up.
- **Death and respawn rules are configuration, in one place.** What happens to carried items, how long a respawn waits,
  and which choices are offered all live in one asset that is easy to find. Which values to use is not decided:
  development uses an instant respawn, and live play may add a wait.
- **The leash point is a fixture.** A ship comes with a default one, and more can be added later (a socket fitting on
  designed ships).
- **While leashed you can climb your own rope**, as a setting a designer can switch off. This is the simplest version.
- **Towing by lock-on is not needed for now.** The winch (S3) reels in the leash, and the leash already takes the place
  of hooking a falling player.

## Design

### One place for "what happens when a player dies or respawns"
- A **Respawn** feature owns the rules. Its config, `Settings/Respawn/RespawnRules.asset`, has one entry per cause:
  - **Overboard** now; downed or killed and the session restart later.
  - **Carried item:** keep it, destroy it, or return it to where it came from (its rack).
  - **Respawn delay** in seconds (0 for development).
  - **Where** you respawn (the ship's boarding pose, `Core/Ship/AirshipBoardingPose.cs`).
  - **Which choices** the player gets (respawn now, wait for rescue).
- The installer references the rules asset, so a profile can use a different one (for example
  `RespawnRules_Dev` with instant respawn and `RespawnRules_Live` with a wait). Find it from a row in
  `.docs/CODE_MAP.md`, "Where does X live": "What happens when a player dies or respawns".
- Other respawns (`Features/Boarding/BoardingUseCase.cs`, the cloud reset fallback) keep using
  `Core/Humanoid/IHumanoidRespawnService.cs` to move the body. The rules decide everything around the move, so the
  movement code does not change.

### Falling: a seam in CloudBoundary, not a rewrite
- CloudBoundary keeps detecting the fall, then hands the fallen player to a contract in `Core.Domain` (an overboard
  handler; its exact shape is part of S1).
- With no handler registered, it does today's teleport. Scenes without the Respawn feature keep their behaviour.

### The choice
- The server asks only the fallen player, through the same exchange Ship Sockets uses for its fitting menu
  (`Features/ShipSockets/ShipSocketsNetworkMediator.cs`: an RPC to one client, a menu, and a request the server
  checks).
- The menu is a `MenuDefinition` (`.docs/UI_FRAMEWORK.md`). It stays available the whole time you hang on the leash, so
  if the whole crew falls nobody is stuck.

### The leash
- **Leash point:** a networked fixture with a point on the ship. The classic airship gets one through the installer;
  designed ships get it as a socket fitting (`Core/Ship/Sockets/`). On a ship with no leash point, only respawn is offered.
- **Server-driven body:** while you hang, the server moves you and your client stops predicting and follows server
  state. Your position depends on the ship's, and each peer sees the ship at a slightly different place, so your own
  prediction would be corrected all the time. This needs one small core addition: a "server-driven" state for a
  humanoid that pauses owner prediction (`Core/Humanoid/HumanoidMovementUseCase.cs`, the reconciliation path).
- **Motion:** a rope with a fixed length, so you are dragged along behind and below the ship. It is an
  `IHumanoidMotionContributor` like the hook, running on the server only while you hang.
- **Climbing your rope:** holding forward or jump goes up the rope at `ClimbSpeed`. It is input the server applies,
  slightly delayed but fine at that speed. Reaching the leash point lets you go, on deck.
- **`LeashConfig`:** rope length, whether you can climb, climb speed, and a drag-damage switch (off for now).

### Rescue: the winch (an upgrade)
- A station at the leash point. Occupying it reels the leash in, much faster than climbing.
- It reuses the hook's reel, arrival hop and climb over the rail (`Features/GrappleHook/GrappleHookProcessor.cs`).
- Whether a ship has a plain leash point or a winch follows the module idea from the crew-tow discussion. For now it
  can be two fittings.

## Slices
Each slice ships with EditMode tests and a scenario that passes solo and host + client.

1. **S1: the overboard choice and respawn rules.** The Respawn feature and `RespawnRules`, the overboard seam in
   CloudBoundary, the choice menu, respawn with the configured item outcome and delay. Scenario: fall, choose respawn,
   aboard without the held item.
2. **S2: the leash.** The leash point fixture, the server-driven state (the core change), hanging and being dragged,
   climbing the rope, the respawn choice while hanging. Scenario: fall, choose the leash, climb back aboard on host and
   client.
3. **S3: the winch.** A station that reels the leash in. Scenario: a second player reels the first one aboard.

## Open, decided later
- Values for live play (items lost or returned, the respawn wait).
- Drag damage, and what being dragged through island rock does.
- A rope ladder for players who are not leashed, and whether lock-on towing comes back.
- The resource loop that motivates the trips (fuel canisters on islands): `.docs/plans/grapple-crew-tow.md`.

## Progress
- [x] S1 (2026-10-15): `OverboardRespawn` passes solo and host + client (2026-10-16).
  - Code: `Features/Respawn/` and the CloudBoundary seam (`CloudBoundaryUseCase`, `Core/Domain/IOverboardHandler.cs`).
  - Assets: built by **TinCan > Dev > Respawn > Build Assets** (`DevTools/Editor/RespawnAssetBuilder.cs`).
  - The overboard tag drives everything on the player's peer, so the only RPC is the choice back to the server.
  - While marked, the body is held still under the clouds, by an `IHumanoidMotionContributor` on the server and owner.
  - The menu reopens if closed before choosing.
  - Carried items: **Keep** or **Lose**. "Return to its rack" behaves like Lose today, because racks are unlimited. A
    real return (a jerry can back to its supply) needs to know where an item came from.
  - Development rules: lose the carried item, no delay.
- [x] S2 (2026-10-16): the leash. `LeashClimb` passes solo and host + client.
  - **Core:** `Core/Humanoid/IServerDrivenHumanoidRule.cs`. While it holds, the owner follows server state instead of
    predicting (`HumanoidMovementUseCase.FollowServer`); it is an optional constructor parameter.
  - **The rope:** `LeashRopeUseCase` runs on the server only: a fixed-length rope (`LeashProcessor`), climbing on forward
    or jump. Reaching the point lands you at its `Stand` spot.
  - **Respawning while leashed:** Interact, through the `Context_Leashed` input context. Nothing is forced, so you can
    climb. It reuses the Interact key, so no new key binding was needed.
  - **Settings:** in the same `RespawnRules` asset: rope length, `CanClimb`, climb and winch speed, arrival distance,
    drag-speed cap.
  - **Wiring lesson:** a motion contributor must not depend on the movement use case (a VContainer cycle that EditMode
    tests cannot see). So the hold-still and rope steps are their own classes, and `LeashStore` is shared state.
- [x] S3 (2026-10-16): the winch. `WinchRescue` passes solo and host + client.
  - **A change from the design above:** the winch is an Interact target that starts or stops reeling, not an occupied
    station. It is simpler, and you need not stay at it.
  - **The fixture:** the ship's leash fixture is `LeashWinch` (a leash point with a winch), set on the installer's
    `_leashFixture`. A plain point without the winch would be its own prefab; the module idea is for later.
  - **The scenario** cranks the winch through the server, as Interact does. `CrankWinchInteractionHandler` is
    unit-tested.
  - **Please review in the Inspector:** the winch's spot on the classic airship, ship-local (5.8, -3.49, 0) facing
    starboard; it is right for the test ship.
  - **On designed ships (2026-10-16, developer feedback: climbing aboard went through walls):** the winch is also a
    socket fitting, `rescue.leash_winch` (`FITTING_LeashWinch`, contributed by the Respawn installer), so its fixed-pose
    fixture stays off designed ships. The Galleon has a small starboard balcony off the quarterdeck: a mount plate at
    (5,2,-6) with rails, reached through a rail gate (`hull.rail_gate`). Its loadout mounts the winch there, the rope
    hangs in open air clear of the balloons, and a climber lands on the quarterdeck. The scenario `GalleonLeashClimb`
    (cheesed_scene) passes solo and host + client; `ShipStairs` and `GalleonLoadout` still pass.
  - **Not yet:** drag damage; a winch spot reviewed on the classic airship.

