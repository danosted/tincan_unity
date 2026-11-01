Status: Approved (2026-10-31); fourth in [core-systems-roadmap.md](core-systems-roadmap.md)

# Crew pings, world markers and names

## Why
TinCan's jobs depend on each other: the fuel runner needs to know where the breach is, the gunner where the hazard
is, the pilot where the island is. Players can only point with words. A ping (look at something, press a key, everyone
sees a marker) is the cheapest co-op communication there is. Players are also anonymous capsules: nobody can tell
who is who on deck.

## Today (verified 2026-10-31)
- No ping, chat or player name. `DisplayName` exists only on items, ship parts, fittings and input actions.
- Every marker is built on its own: the damage point's `Marker` child toggled by a cue (`ShipDamagePointNetworkMediator`),
  the grapple aim marker (`GrappleAimPresenter`), the repair aim highlight (`RepairAimHighlightPresenter`), the target
  outline (`TargetOutline`), the chart's points of interest and the compass (`Features/Navigation/`).
- The HUD contract `IHudValues` (`Core/Domain/Hud/`) has named texts and meters only. Nothing draws a marker anchored
  in the world, on screen or at its edge.
- Targeting (`Core/Targeting/`) already knows what the local player aims at, and interaction already gives targets a
  name.

## Design

### W. A world indicator layer (core)
- `IWorldIndicators` (`Core/Domain/Hud/`): `Show(id, WorldIndicatorSpec)`, `Update(id, ...)`, `Hide(id)`. A spec
  has an anchor (a `Transform` plus a local offset, so a marker on a moving ship stays put), a kind (icon and colour),
  a label, whether it shows distance, whether it clamps to the screen edge when off screen, and a lifetime.
- Presentation only, never replicated. Drawn by a `WorldIndicatorView` in `App/Views/` (UI Toolkit, the same panel
  settings as the HUD), projecting through `ILocalViewCamera`. The model (`WorldIndicatorUseCase`: lifetimes, fade,
  edge clamping math in a pure `IndicatorProjectionProcessor`) is tested; the view is a placeholder like the others.
- Lives in the core UI assembly (`TinCan.UI`), registered by `UiFeatureInstaller`.

### P. Pings (feature `TinCan.Features.Pings`)
- A `Ping` action in the humanoid input context (contributed by the feature, rebindable, per `INPUT.md`).
- On press: ask targeting what is under the look. A target with a name pings that thing ("Breach", "Jerry can",
  "Hazard"); otherwise a ray from the eye pings the spot ("Here"). Features can name their kind through
  `FeatureInstaller.IExtension<PingKind>` (icon, colour, label, which actor kinds it matches).
- `PingsNetworkMediator` on a session fixture: the client sends a `ServerRpc` with the target's `NetworkObject` id
  and a local offset (never a world position: the ship moves, see `humanoid-prediction-reconciliation.md`, point 3).
  The server applies a rate limit (per player per second) and a cap (three live pings each), then sends a `ClientRpc`
  to everyone. A UI action, not simulation, so an RPC is right (Rule 1.1.3).
- Every peer shows it through `IWorldIndicators` with the pinger's name and a short sound (`AudioChannel.Ui`,
  [audio-mixer.md](audio-mixer.md)). It expires after a few seconds, or when its target despawns.
- `PingUseCase` (validation, rate limit, expiry) and the kind lookup ship with EditMode tests; a scenario `PingCrew`
  checks that a host's ping on a damage point reaches the client and expires.

### N. Names and nameplates
- `player.name` comes from [player-settings.md](player-settings.md). The owner sends it once on spawn; the server
  trims it, falls back to "Crew N" and stores it in a `NetworkVariable<FixedString32Bytes>` on a player mediator
  (a new `PlayerIdentityNetworkMediator` or an existing one; to decide during the build).
- Nameplates over crewmates use the world indicator layer (hidden on the local player, faded with distance).
- The name also goes on pings and in the voyage stats (`first-voyage.md` V4).

## Slices
1. **S1 World indicators:** contract, use case, view, tests. Nothing uses it yet except a dev-only test marker.
2. **S2 Pings:** the feature, the input action, the mediator, the scenario.
3. **S3 Names:** the setting, the replicated name, nameplates, names on pings.
4. **Later:** migrate ad hoc markers where it helps (damage points far away, island landmarks); a ping wheel with
   callouts ("Need fuel", "On my way").

## Decisions (developer, 2026-10-31: the recommended options)
1. **No text chat.** Playtests use voice (Discord, or the same room).
2. **One ping key** that guesses from the target; a wheel comes later if callouts are missed.
3. **Existing markers move only where an off-screen arrow or distance helps**; the rest stay as they are.
4. **Names before hosting:** until the hosting plan's connection approval exists (it comes last), the owner sends its
   name once after spawn, as S3 describes. Approval takes the name over when it lands.

## Verification
- EditMode suite green, plus the new tests.
- `verify.ps1 -Scenario PingCrew` solo and host + client (asked for first).
- Human: ping with three players on a voyage; check readability in first person, at the screen edge, while the ship
  turns.
