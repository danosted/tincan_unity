Status: Approved

# Responsive humanoid movement: client prediction + server reconciliation

## Context

Remote clients feel a round trip plus an interpolation buffer of lag on their own player. The server-driven model was introduced in `21f1fa6` to stop long-running desyncs. It enforces sync, but it throws away the owner's local simulation.

**Root cause (verified in code):**
1. The owner already simulates locally. See `HumanoidPlayer.IsSimulating => IsServer || IsOwner` in `Assets/Scripts/Network/Infrastructure/HumanoidPlayer.cs`. But `NetworkTransformMediator` (`OnIsServerAuthoritative() => true`) overwrites the owner's transform every frame with the *interpolated server* pose. The camera (`ThirdPersonLookView.LateUpdate`) follows that transform. The felt latency is ½RTT + a server tick + the NetworkTransform interpolation buffer (~100 ms at 30 Hz) + ½RTT. Prediction and NetworkTransform also fight each other: the owner's `CharacterController.Move` runs on the tick, then NetworkTransform snaps the character back.
2. Input travels as a latest-value `NetworkVariable<HumanoidInputState>`. The server simulates "whatever is latest" each tick, so it repeats or skips inputs when client and server ticks drift. Identical code therefore produces different trajectories. This is a structural desync source, independent of which side has authority.
3. The client sees the ship at an interpolated, past pose; the server sees it at the present pose. Any world-space comparison of a player on deck is off by ship velocity × latency. That is very likely the historical desync. All reconciliation must happen in ship-local space. `PlayerAttachmentState` already publishes local pose, which confirms the approach.
4. The tick is 30 Hz (`Assets/Prefabs/Singletons/NetworkService.prefab`), and the owner's motion is not interpolated for rendering. Even perfect prediction steps visibly at 30 Hz on a high-refresh display.

**Why the ship feels better:** it uses the same server-driven, NetworkTransform-delayed path, but its acceleration ramps hide the delay. Look yaw is local, and the player rides the ship, so relative motion is small. No change to the ship is needed for this work.

`HumanoidInputState.Sequence`, `PlayerAttachmentState.LastProcessedInputSequence` and the unused `HumanoidMovementSnapshot` show that reconciliation was intended but never wired up. This plan finishes that design, which `.docs/ARCHITECTURE.md` already describes as "Client (Prediction) and Server (Authority)".

## Approach

The server stays authoritative. The owner predicts and is never overwritten by transform sync. The server acknowledges the last input it applied and sends its state in platform-local space. The owner corrects only on real divergence, by rewinding to the server state and replaying the unacknowledged inputs. Remote proxies interpolate snapshots in platform-local space.

### Phase 0: Feedback loop (build first, then record a baseline on the current code)

Goal: one command runs a host plus a latency-simulated client, lets a bot play a fixed route, and writes numbers that an AI or a human can read. Until now testing was a manual Start Host/Join in one editor, and that can't show the problem, because a host has zero latency.

What already exists and gets reused:
- `com.unity.multiplayer.playmode` 2.0.2 (MPPM) is installed. `Library/VP` already contains two virtual-player clones.
- `Assets/Scripts/Features/UI/CommandLineSessionBootstrap.cs` already auto-starts host/client from `-autohost` / `-autojoin`.
- The `unity` MCP/CLI can enter and exit play mode and read the Console.
- NGO's `UnityTransport` already enables the simulator pipeline stage in the Editor and in dev builds (`UNITY_MP_TOOLS_NETSIM_IMPLEMENTATION_ENABLED`). Latency values are supplied only by the Multiplayer Tools package, which is **not installed**.

Pieces to add, all dev-only, in a `TinCan.DevTools` assembly registered by a `NetTestHarnessFeatureInstaller`. Nothing goes into `ProjectLifetimeScope`.
1. **Auto-session in the Editor.** Add a session-request source that reads MPPM tags (`CurrentPlayer.ReadOnlyTags()`: `Host` / `Client`) next to the existing CLI args, and feed it into `CommandLineSessionBootstrap`. Commit an MPPM **Play Mode Scenario** asset, "HostPlusClient": the main editor is tagged Host, one additional editor instance is tagged Client, and Stream Logs To Main Editor is on. Pressing Play (or MCP enter-play) then brings both up connected, with no clicks.
2. **Network conditions.** Add `com.unity.multiplayer.tools` and put its Network Simulator on `Assets/Prefabs/Singletons/NetworkService.prefab`. The preset is picked by tag, e.g. `Client` + `Lag100`: 100 ms RTT, 10 ms jitter, 2% loss. The host stays clean. Verify the package version and API against Unity 6000.4 at install.
3. **Scripted input bot.** Add an `IInputService` decorator, `ScriptedInputService`, enabled by the `Bot:<route>` tag or a `-bot <route>` arg. Routes are data (a ScriptableObject): walk, strafe, sprint, jump in place, board the ship, walk on the deck while the ship turns and banks, jump off. This makes runs deterministic and repeatable without a human. The ship helm can also be driven by the bot, so the "moving platform" case is exercised.
4. **Telemetry.** `NetMovementTelemetry` hooks into the humanoid tick. Its metrics:
   - Owner **response latency**: ticks from the first non-zero input to a visible pose change. The key "sluggish" metric.
   - Corrections per second, and mean/max correction error in cm, measured platform-local.
   - Server input-buffer depth, starvation count and skip count.
   - Server vs client **platform-local divergence** at the acked sequence.

   It writes a periodic line and a final report when the route ends. The report goes to the Console as `[NetTelemetry] {json}` and to `Logs/net-telemetry/<role>-<timestamp>.json`. The file is the primary source because it survives Console noise and domain reloads, and I can read it directly.
5. **Debug overlay** for human playtests, toggled with a key: RTT, tick, correction count and error, and a ghost of the last server pose.

**As built (see `.docs/NETWORK_TEST_HARNESS.md`), deviations from the list above:**
- **No Multiplayer Tools package.** NGO already adds UTP's global simulator layer in the Editor and in dev builds.
  `NetworkConditionsUseCase` sets it through `UnityTransport.GetNetworkDriver().ModifyNetworkSimulatorParameters`.
  The delay is send-side per peer, so both peers get the same preset.
- **No `IInputService` decorator.** The existing `IScriptedInput` seam is already merged by `UnityInputService`,
  and the bot drives that.
- **Routes are code** (`BotRoutes.cs`), not ScriptableObjects: they are versioned and reviewable with the harness.
- **Tags instead of a committed scenario asset for now.** Tags are set in the Multiplayer Play Mode window. A
  committed Play Mode Scenario is still open, pending a check that it can be authored and selected from the CLI.
- **Server-side metrics** (input-buffer depth/starvation, divergence at ack) arrive with Phases 1–2. They need the
  new input buffer and snapshots. Phase 0 measures the client-felt side: start, stop and jump latency, snaps and
  reversals, split by ship still or moving.

**Baseline, 2026-09-26, current code, Lag100 (measured RTT ~117 ms), client1 DeckWalk on a moving ship:**

| Metric | Measured | Physics floor |
|---|---|---|
| start p50 / p95 | 200 / 398 ms (n=17) | ~40 ms |
| stop p50 / p95 | 296 / 694 ms (n=10, 1 timeout) | ~175 ms |
| jump | **0 of 3 jumps registered** (all timed out) | ~1 tick |
| snaps | 30 (max 1.16 m) | 0 |
| reversals | 5 | 0 |

The client jumps are lost outright, not merely late. A jump is a one-tick `IsJumping` in a latest-value
`NetworkVariable`. When two input updates reach the server within one server tick, the jump is overwritten before
it is simulated. This is root cause 2 above, and Phase 1's input queue fixes it. The host report has no latency
samples because the host is at the helm. It shows 46 snaps on the host's own humanoid while it pilots, which needs
a look later but is not client-felt.

**Loop per change:** edit, then compile via `unity`, then EditMode tests, then enter play with the HostPlusClient scenario, wait for the bot route (~30 s), read the telemetry JSON, exit play. The human does the final feel test.

**Baseline first:** run the harness on today's code *before* Phase 1. Expected result: owner response latency of several ticks at 100 ms and nonzero divergence. That proves the harness measures the problem, and every later phase has to beat the baseline.

**Pass criteria** (tune once the baseline exists):
- Owner response ≤ 1 tick.
- Steady-state corrections ≈ 0/s on the ground and on a straight-flying ship, and a low rate while turning/banking.
- Max correction < 5 cm, with no hard snaps except teleports.
- Divergence at ack < 1 cm.

**Also: a fast deterministic core test (EditMode).** A `LoopbackLink` fake with configurable latency, loss and reorder drives `HumanoidInputBuffer`, `HumanoidPredictionHistory` and `HumanoidReconciliationProcessor` over a few hundred simulated ticks, in milliseconds. It catches logic regressions without starting any editor.

**Risks to verify while building Phase 0:**
- Whether MCP enter-play honours the active Play Mode Scenario.
- Whether additional-editor instances stream logs to the main Console. The file output covers the case where they don't.
- The clone recompile time after each C# change. If it is slow, fall back to a local dev build of the client launched with `-autojoin -bot … -logFile`.

### Phase 1: Deterministic input stream (fixes the desync source)
- Replace the owner-write `NetworkVariable<HumanoidInputState>` with an unreliable `[Rpc(SendTo.Server, Delivery = Unreliable)]` on `HumanoidPlayer`. Each send carries the last N (≈4) inputs for redundancy against packet loss.
- New pure class `HumanoidInputBuffer` (Features/HumanoidMovement): a server-side per-player queue keyed by sequence. It discards duplicates and old entries and consumes **exactly one input per server tick**. It keeps a small jitter target of 1–2 inputs. When starved it repeats the last input and records that it did so. It skips ahead if the queue grows past a cap.
- `SimulationUseCase.Tick` is unchanged in shape. On the server, `HumanoidPlayer.InputState` returns the input dequeued for this tick. On the owner, it returns the locally gathered input. Remote proxies no longer need the input, except for animation, which can come from the snapshot.
- Jump is already level-based (`IsJumping = triggered || pressed`), so it survives redundancy. Keep it that way.

### Phase 2: Owner prediction + reconciliation (fixes the sluggish feel)
- Remove `NetworkTransformMediator` from the player: drop it from `HumanoidPlayer`'s `[RequireComponent]` list and from `Assets/Prefabs/NetworkPlayer.prefab`, using the Editor via MCP/CLI rather than YAML. The player's pose is then synced only through snapshots.
- Extend `HumanoidMovementSnapshot` into the single server→client state and fold `PlayerAttachmentState` into it: `LastProcessedInputSequence`, platform `NetworkObjectReference` + local position/rotation (world pose when unattached), horizontal/vertical velocity, `PreviousInputMask`, and a `TeleportEpoch` byte. The epoch is bumped on `ResetCharacter`, respawn and `CloudBoundaryUseCase` so the owner hard-snaps instead of replaying through a teleport.
- The server publishes the snapshot every tick from the simulation, not from `LateUpdate`, so it matches exactly the state after the acked input.
- Owner side: a new pure `HumanoidPredictionHistory` ring buffer (seq → input + predicted platform-local pose + velocities). On receiving a snapshot:
  1. Drop history ≤ acked seq and compare the stored prediction at the acked seq with the snapshot, in platform-local space.
  2. If the error is below a threshold (~2–5 cm, tunable in config), do nothing. This should be the normal case.
  3. Otherwise, set pose and velocities from the snapshot, resolved against the **client's current** ship pose. Then replay the remaining inputs with movement only: no GAS side effects, and ship frame held static during the replay (`SurfaceDelta = 0`).
- Split `HumanoidMovementUseCase.SimulateMovement` so replay can call the pure movement step without re-running `_abilitySystem.ProcessAbilitySimulation` or platform-pose bookkeeping. Expose the per-actor velocity dicts through a small get/set so reconciliation can restore them.

### Phase 3: Remote proxies
- Non-owner clients keep `IsSimulating == false`. They buffer snapshots and interpolate between them in platform-local space, about 2 ticks behind. This replaces `ApplyAttachmentPose` and NetworkTransform with one path, so proxies on the deck stay glued to the ship.

### Phase 4: Visual smoothing
- Render the owner's mesh and camera pivot from a visual transform. It is interpolated between the previous and current tick poses (platform-local) using the tick fraction. A reconciliation correction goes into a visual offset that decays over ~100 ms. `ThirdPersonLookView` follows the visual.
- Try `TickRate` 60 after this and measure bandwidth. It is cheap if snapshots stay small.

## Critical files
- Phase 0:
  - `Assets/Scripts/Features/UI/CommandLineSessionBootstrap.cs`: add an MPPM tag request source.
  - New `Assets/Scripts/DevTools/` assembly: bot, telemetry, overlay and installer.
  - The scenario asset.
  - `Packages/manifest.json`: add `com.unity.multiplayer.tools`.
  - `NetworkService.prefab`: add the Network Simulator, via the Editor.
- `Assets/Scripts/Network/Infrastructure/HumanoidPlayer.cs`: input RPC, snapshot publish/receive, remove NetworkTransform requirement.
- `Assets/Scripts/Features/HumanoidMovement/HumanoidMovementUseCase.cs`: split out a pure movement step; reconciliation entry point; velocity access.
- `Assets/Scripts/Features/HumanoidMovement/HumanoidMovementSnapshot.cs`: extend it; merge in `Network/Infrastructure/PlayerAttachmentState.cs`.
- New `HumanoidInputBuffer`, `HumanoidPredictionHistory` and `HumanoidReconciliationProcessor` (pure) in `Features/HumanoidMovement/`.
- `Assets/Scripts/Network/Infrastructure/NetworkSimulationScheduler.cs`: publish snapshots after `_humanoidMovement.Tick()` on the server.
- `Assets/Scripts/Features/CloudBoundary/CloudBoundaryUseCase.cs` and the respawn path: bump the teleport epoch.
- `Assets/Prefabs/NetworkPlayer.prefab` (via Editor): remove NetworkTransform; add a visual child in Phase 4.
- The ship keeps `NetworkTransformMediator`, so it stays unchanged.

## Tests (EditMode, per AGENTS.md)
- `HumanoidInputBufferTests`: duplicates, out-of-order, loss with redundancy, starvation repeat, overflow skip, `uint` wrap.
- `HumanoidPredictionHistoryTests` / `HumanoidReconciliationProcessorTests`: no correction under threshold; rewind+replay reproduces the server result; teleport epoch forces a snap; platform-local comparison is independent of the ship's world pose.
- The existing suite stays green.

## Docs
- `.docs/ARCHITECTURE.md`: describe the real prediction/reconciliation loop and the ship-local rule.
- `.docs/CODE_MAP.md`: new types and a known trap: "never compare player poses on a ship in world space".
- Save this plan as `.docs/plans/humanoid-prediction-reconciliation.md`.

## Verification
1. Phase 0 harness baseline on the current code (see the Phase 0 loop and pass criteria).
2. After each phase: compile via the `unity` MCP/CLI, run the EditMode tests, run the HostPlusClient scenario with the bot, and compare the telemetry against the baseline and pass criteria.
3. Human playtest with the Lag100 preset:
   - Client walking and jumping on the ground feels instant.
   - Walking on a moving, banking ship shows no rubber-banding.
   - Stepping off the ship, respawning and hitting the cloud boundary hard-snap cleanly.
   - Remote proxies are smooth on and off the deck.
   - Log reconciliation count and error magnitude. The goal is near-zero corrections at steady state.
