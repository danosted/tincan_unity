Status: Done (2026-09-30, built unattended; awaiting the developer's review)

# Voyage session (first-voyage.md V2 fail state + V3)

## Goal
A session loop for the first playtest: every voyage starts where the ship is, runs to a destination ahead of it, and ends
in **Arrived** (win) or **Lost** (the ship's health reaches 0). An end screen offers **Restart**, which resets the crew's
world in place, without relaunching.

## Decisions (taken unattended with the recommended option; review them)
1. **The route starts where the ship is.** The destination is `RouteLength` metres ahead of the ship's level heading at
   the start, at the same altitude. Restart does not teleport the ship (moving a ship with players on its deck is the
   risky part), and a new route lies ahead of wherever the last one ended.
2. **Features reset themselves.** A core contract `ISessionParticipant` (`Core/Domain/ISessionParticipant.cs`) has two
   calls: `ResetForSession()` (restore the start state) and `SetSessionActive(bool)` (pressure on or off).
   - Sky hazards clear the sky, and run the field only while underway.
   - Ship damage restores broken parts and the ship's health (the existing `GE_ShipPartRestore`: the ship carries the
     same `Attr_Health` and `Attr_MaxHealth`), and runs random breakage only while underway.
   - Fuel fills the tank and resets the jerry-can crate.
   - The designed-events director drops a running event and runs its rotation only while underway. The sandbox showed
     `HullStress` breaking parts during a briefing.

   The Voyage feature calls every registered participant and references none of them. A scene without Voyage behaves
   exactly as before: nothing calls the participants. The pressure is switched off on the very first server tick, so the
   seconds between the ship spawning and its voyage state spawning are quiet too.
3. **Sinking is read from GAS.** Lost = the ship controller's health is depleted (`HealthQueries.IsDepleted`). No
   `HullFailedEvent` is needed; the voyage publishes `VoyageStartedEvent` and `VoyageEndedEvent` (outcome, seconds
   underway) for the stats that come in V4.
4. **State lives on a ship fixture.** `VoyageState.prefab` has a `NetworkObject`, an `EntityNetworkMediator` and a
   `VoyageNetworkMediator`. It replicates the phase, the voyage number, the destination and the briefing countdown. It
   registers as an actor (`IVoyageState`), and a server RPC takes Restart from any player.
5. **Every peer presents.**
   - The HUD shows a "Voyage" meter plus a line: the voyage number and countdown, then distance and bearing ("3.2 km to
     go, 12° to starboard"), then the outcome.
   - A beacon pillar (URP unlit, 600 m tall) marks the destination.
   - The end screen is a menu (`Menu_VoyageArrived` or `Menu_VoyageLost`) with Restart and Quit. It reopens while the
     voyage is over, so Esc cannot drop you into a dead session. The Menu input context silences gameplay meanwhile.

## Pieces
- `Features/Voyage/` (assembly `TinCan.Features.Voyage`, installer `VoyageFeatureInstaller`):
  - `VoyagePhase` (+ `VoyagePhaseExtensions`), `VoyageConfig`;
  - `VoyageRouteProcessor` (pure);
  - `IVoyageState` + `VoyageNetworkMediator`;
  - `IVoyage` + `VoyageUseCase` (server, `AfterAirship`);
  - `VoyageHudPresenter`, `VoyageEndScreenPresenter`, `VoyageBeaconPresenter`, `RestartVoyageMenuCommand`;
  - `VoyageStartedEvent`, `VoyageEndedEvent`.
- Participants: `SkyHazardUseCase`, `ShipBreakageUseCase`, `FuelConsumptionUseCase`, `EventDirectorUseCase`.
- Assets (built by **TinCan > Dev > Voyage > Build Assets**, `DevTools/Editor/VoyageAssetBuilder.cs`):
  - the config, the two menus, the beacon material, the fixture and its prefab, and the installer;
  - `Profile_FuelSandbox` + `Profile_Test_Voyage`. The test profile also loads Events, which ship damage builds on;
    `ArchitectureRulesTests` caught that.
- Test area `Test_Voyage` (in the build list) and scenario `VoyageLoop`.
- Tidied on the way (one type per file, since these files were touched): `ISkyHazards`, `SkyHazardDestroyedEvent`,
  `SkyHazardHitShipEvent`, `IShipContactQuery` / `PhysicsShipContactQuery` and `IShipBreakage` got their own files.

## Tuning (first guess)
- Route 4 km: at the ship's 15 m/s, about 4.5 minutes at full throttle.
- A full tank lasts about 2 min at full throttle, so the fuel runner has to work.
- Briefing 10 s. Arrival within 60 m.
- Watch in the playtest: parked with nobody at the cannon, the sandbox ship lost about half its hull in the first minute
  or two (one hazard every 6 s, −50 each). That may be right for a crew of 3–4 with a gunner, or too harsh.

## Verification (2026-09-30)
- EditMode: `VoyageRouteProcessorTests`, `VoyageUseCaseTests` (with the Restart command), `VoyageEndScreenPresenterTests`,
  and the participant cases in `SkyHazardTests`, `ShipBreakageUseCaseTests`, `FuelConsumptionUseCaseTests` and
  `EventDirectorUseCaseTests`.
- Scenario `VoyageLoop`, solo and host + client, passed:
  1. Begin, then cast off.
  2. The destination is moved onto the ship → Arrived; the end screen shows on the subject's peer.
  3. The subject presses Restart (a server RPC from the client) → the voyage number goes up.
  4. The server checks the ship's health is full, casts off and sinks the ship → Lost; the subject sees the Lost screen.

  The first version waited for the Briefing phase on the subject. In solo the server cast off and sank the ship within
  a few frames, so that wait raced; the replicated voyage number replaced it.
- Sandbox (`drm_cloud_environment`, pirate ship, host only): voyage 1 briefed and cast off. The HUD showed the Hull and
  Voyage meters and "4.0 km to go, dead ahead", and hazards hit.
- Not verified by a machine: flying the whole route by hand, and the beacon's look from the helm.
