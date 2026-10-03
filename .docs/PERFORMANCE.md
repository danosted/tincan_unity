# Performance: perf runs and budgets

Perf runs answer one question: did this change make the game cost more frames, ticks, memory or bandwidth? A run
plays a fixed load (a **scenario**) in containers, every instance measures itself, and the numbers are checked
against **budgets**: limits that live in git, next to the code they constrain.

Plan and open work: [`plans/performance-budgets.md`](plans/performance-budgets.md).

## Running

```powershell
.\.tools\build.ps1 -Perf -Image -Client    # once per code change: development builds + image tincan-server-perf
.\.tools\perf.ps1 run CrewLoad             # containers only; nothing opens on the desktop (~2.5 min)
.\.tools\perf.ps1 run CrewLoad -Desktop    # + the Windows client rendering at 1920x1080 (opens a window)
.\.tools\perf.ps1 run CrewLoad -Repeat 5   # several runs in a row
.\.tools\perf.ps1 list                     # every run and its verdict
```

Needs the Podman machine running (`podman machine start`). A `-Desktop` run also needs the one-time firewall rule for
the perf client; perf.ps1 offers to create it (asks first), or run `.\.tools\setup.ps1 -Only Firewall`. Unattended,
it stops with exit code 2 instead of launching a client that would wait on a Windows prompt. Exit code 0 pass (or no budgets yet), 1 a budget failed,
2 the environment was not usable. A container-only run does not touch the Editor or the desktop; a `-Desktop` run
opens a game window, so schedule it.

The perf builds are **development builds**: most profiler counters and the network simulator only exist there. They
run a little slower than release builds, but always by the same amount, so their numbers compare with each other.
Never compare a perf number with one from a release build or from the Editor.

## Scenarios

| Scenario | Load | Roles |
|---|---|---|
| `CrewLoad` | A dedicated server, four players on DeckWalk (looping), an active voyage with sky hazards. | `server` (container), `bot1`..`bot4` (headless clients in containers); with `-Desktop`, `bot1`..`bot3` and `client` (the Windows client, rendering). |

Not yet: helm, gunner and repair routes (bots on clients cannot take the helm, so the ship holds its course), spawn
ramps, the worst rendering view, and the soak. See the plan's P3.

## Environment profiles

A number only means something next to where it was measured. Each role runs in a named **profile**
(`.tools/modules/TinCan.Perf.psm1`, `$Profiles`, copied into every `run.json` and `budgets.json`):

| Profile | What |
|---|---|
| `ctr-server-2c` | The perf server image in Podman: 2 CPUs pinned (`--cpuset-cpus 2,3`), 1 GB. |
| `ctr-bot-1c` | The same image as a headless client (`-batchmode -nographics`, capped at 60 fps): 1 CPU pinned, 768 MB. |
| `ref-desktop` | The reference PC (Ryzen 7 7800X3D, RX 9070 XT, 31 GB, Windows 11), the Windows perf client at 1920x1080 windowed. |

Pinning separates containers from each other; it does not make this PC slower or quieter. Runs still want an idle
machine. Budgets set on one PC do not transfer to another: a new machine needs its own baseline.

## What is measured

Every instance started with `-perf` waits for its session, warms up (`-perfwarmup`, 30 s in perf.ps1), measures a
window (`-perfduration`, 90 s), writes `<role>.json` and, with `-perfquit`, exits. Code:
`Assets/Scripts/DevTools/Perf/` (`PerfSampler`, `PerfSeries`, `PerfReport`, `PerfOptions`).

| Metric | Unit | Source | Notes |
|---|---|---|---|
| `frame_ms` | ms | `Time.unscaledDeltaTime` | Includes waiting for the frame cap: a server is ~33.3 ms (30 Hz cap), a bot ~16.7 ms. Means something on the desktop client. |
| `main_thread_ms` | ms | counter `CPU Main Thread Frame Time` | Main-thread work per frame. The server's and bots' real CPU cost. |
| `tick_ms` | ms per tick | marker `NetworkTickSystem.Tick` | All simulation work in one network tick (the game's `NetworkSimulationScheduler` runs inside it). The server's key number. |
| `gc_alloc_bytes`, `gc_alloc_count` | per frame | counters `GC Allocated In Frame`, `GC Allocation In Frame Count` | Managed allocation. Steady-state goal: 0. |
| `physics_ms`, `physics_queries` | per frame | marker `FixedUpdate.PhysicsFixedUpdate`, counter `Physics Queries` | |
| `draw_calls`, `setpass_calls`, `triangles` | per frame | counters `Standard`/`Standard Indirect`/`Standard Instanced`/`SRP Batcher`/`BRG`(+`Indirect`) `Draw Calls Count` (summed), `SetPass Calls Count`, `Triangles Count` | Unity 6000.4 has no single draw-call counter. Frames that read 0 are skipped (no finished render reading yet, common at hundreds of fps), so headless instances record none. |
| `gpu_ms` | ms | counter `GPU Frame Time` | Desktop client only. Readings that are not a plausible frame time (≤ 0 or ≥ 1 s) are skipped. Not budgeted yet. |
| `tx_bytes_s`, `rx_bytes_s`, `tx_packets_s`, `rx_packets_s` | per second | `UnityTransport.GetNetworkDriver().GetStatistics()` | All of this instance's traffic (a server: every client). |
| `messages_sent_s`, `messages_received_s` | per second | NGO markers `NetworkMessageManager.SerializeAndEnqueue.*` / `DeserializeAndHandle.*` | The report also lists totals per message type (`messagesSent`, `messagesReceived`). |
| `gc_used_mb`, `total_used_mb` | MB, per second | counters `GC Used Memory`, `Total Used Memory` | `memorySlopeMbPerMin` in the header is the growth rate over the window. |
| `network_objects` | per second | `SpawnManager.SpawnedObjectsList.Count` | |

Each metric has `n`, `mean`, `p50`, `p95`, `p99`, `max`, `total` and `hitches` (frames over `hitchThresholdMs`, twice
the frame budget). `unavailable` lists counters the player did not expose (a release build loses most of them).

Counter names were read from `ProfilerRecorderHandle.GetAvailable` in 6000.4.5f1, not remembered. After a Unity
upgrade, check them again (the plan's P1 shows how).

## Budgets

`.docs/perf/budgets.json`, one entry per scenario, role kind, profile, metric and stat:

- `baseline` is the median of the runs it was set from, `spread` their range, `runs` how many.
- `max` = baseline × (1 + margin) + floor. Above it the run **fails**. `warn` is halfway.
- The margin and floor per metric are in `TinCan.Perf.psm1` (`$BudgetedMetrics`): ~20–30 % for times and bytes, ~10 %
  for draw calls, plus an absolute floor so near-zero values do not flap.

After each run perf.ps1 also compares every budgeted metric with the median of the last five passing runs and prints
a **drift** warning when it is 15 % higher. History means runs of the same set-up (container-only, or with `-Desktop`) that did not fail. Drift never fails a run; it catches slow creep inside a budget.

### Setting and moving budgets

```powershell
.\.tools\perf.ps1 set-budgets <run> <run> <run> -Roles server,bot -Rationale "why"
```

Writes the medians of those runs for those role kinds and keeps every other entry. Rules:

- Set from 3–5 runs of the same commit on an idle machine.
- **Tighten** freely when a change makes things cheaper.
- **Loosen** only on purpose: say why in `-Rationale` and in the commit message. The diff of `budgets.json` is the review.
- A budget that fails without a code change is noise: add runs before moving the budget.

## Reading results

```powershell
.\.tools\perf.ps1 compare <runA> <runB>              # budgeted metrics side by side, with % change
.\.tools\perf.ps1 trend CrewLoad server.tick_ms.p95  # one metric across every run
```

A run folder (`Logs/perf/runs/<time>_<scenario>_<commit>/`, git-ignored) holds `run.json` (what ran, where, verdict,
budget rows, drift rows), one report per role, and each container's log. Results stay files until a database earns
its place (see the plan).

## Finding what costs: diagnosis runs

```powershell
.\.tools\perf.ps1 run CrewLoad -ProfileFrames 150 -DurationSeconds 30   # the server also records server.raw
.\.tools\perf.ps1 analyze-gc <that run>                                # in the open Editor: where the server allocates
```

`-ProfileFrames` makes the server record a Unity profiler capture of the window's first frames, with allocation call
stacks (`-perfprofile`). The run is a **diagnosis**: the profiler's own cost skews its numbers, so its verdict is
`DIAGNOSIS`, drift ignores it and `set-budgets` refuses it. `analyze-gc` loads the capture in the Editor's Profiler
and sums every `GC.Alloc` by the innermost `TinCan` method on its call stack. For anything else, open `server.raw` in
**Window > Analysis > Profiler** (Load).

## Baseline 2026-10-03

The first budgets, from commit `fa27ec6` plus the uncommitted perf work, on the reference PC with the developer
away (idle machine). Server and bot budgets come from 5 container-only CrewLoad runs, client budgets from 3
`-Desktop` runs. Three later runs (one container-only, one `-Desktop`, one container-only after the drift fix) all
passed with no drift. All 23 budgets: `.docs/perf/budgets.json`.

| Role | Metric | Baseline (median) | Spread over runs | Fails above |
|---|---|---|---|---|
| server | `tick_ms` p95 / p99 | 0.95 / 1.72 ms | 0.90–0.96 / 1.59–1.77 | 1.68 / 3.23 ms |
| server | `main_thread_ms` p95 | 2.53 ms | 2.44–2.58 | 3.66 ms |
| server | `physics_ms` p95, `physics_queries` p95 | 0.48 ms, 46 | 0.48–0.50, 46 | 0.93 ms, 58 |
| server | `gc_alloc_bytes` p95 / mean | 71.2 KB / 39.2 KB per frame | 71.1–71.2 / 37.9–39.7 KB | 89.5 / 49.3 KB |
| server | `tx_bytes_s` / `rx_bytes_s` p95 | 94.9 / 42.8 KB/s | 86.8–100.2 / 42.7–42.9 | 114.9 / 52.4 KB/s |
| server | `messages_sent_s` p95 | 2,151 /s | 2,133–2,203 | 2,592 /s |
| server | `total_used_mb` max, `network_objects` max | 77.5 MB, 31 | 77.2–77.6, 31 | 105 MB, 44 |
| bot | `main_thread_ms` p95, `gc_alloc_bytes` p95 | 2.14 ms, 47.7 KB | 2.05–2.19, 46.9–48.2 | 3.17 ms, 60.1 KB |
| bot | `tx_bytes_s` / `rx_bytes_s` p95 | 10.8 / 36.4 KB/s | 10.8 / 25.7–38.2 | 13.5 / 44.7 KB/s |
| client | `frame_ms` p95 / p99 | 2.39 / 2.63 ms (uncapped, ~400 fps) | 2.36–2.43 / 2.62–2.66 | 3.87 / 5.29 ms |
| client | `main_thread_ms` p95, `gc_alloc_bytes` p95 | 1.60 ms, 64.9 KB | 1.53–1.65, 47.5–65.9 | 2.92 ms, 81.6 KB |
| client | `draw_calls` p95, `setpass_calls` p95 | 738, 52 | 662–790, 49–54 | 822, 62 |

What the baseline says:

- **Frame and tick time have lots of headroom.** The server spends ~1 ms per 33 ms tick; the desktop client renders
  this load at ~400 fps on the reference PC. The plan's "frame time under 16.6 ms" target is far away here; the
  numbers to watch on this machine are regressions, which the budgets catch.
- **Managed allocation is the real problem.** The server allocates ~39 KB (590 allocations) per frame and collects
  garbage more than once a second; bots ~48 KB and the client ~65 KB per frame (p95). The "zero per-frame
  allocations" goal is far off, so these budgets hold the line at today's level; tighten them as allocations go.
- **Where the server allocates** (`analyze-gc`, 2026-10-03): `GameplayTagContainer.HasTag` alone is 44 % (~14.5 KB,
  ~226 allocations per frame: `_tags.Any(t => t.IsChildOf(tag))` allocates a closure, a delegate and an enumerator per
  call; `HasAny` / `HasAll` likewise). Then `FlyingCanUseCase.CullDistantCans` (~1.8 KB), `ShipDamageLocator.FindPoints`
  (~1.3 KB), `InputContextProcessor.Resolve` (~1.1 KB), NGO internals (`BatchedReceiveQueue`, `NetworkTransform`,
  ~2.4 KB), `HumanoidPlayer`'s input RPC handler (~1.1 KB), and a long tail of `Tick` methods.
- **Spawning shows in the traffic.** Sky hazards are instantiated and destroyed (~1.2 per second each way,
  `CreateObjectMessage` / `DestroyObjectMessage`), and `NetworkTransformMessage` is ~65 % of all server messages.
- **The load is still light.** Bots walk the deck; nobody steers, fires or repairs (the ship holds course), so
  these budgets cover "four players aboard with hazards", not combat. New routes (P3) will need their own baselines.

### 2026-10-03: `GameplayTagContainer` stops allocating

`HasTag` / `HasAny` / `HasAll` loop by index instead of LINQ (`Core/Domain/Abilities/Tags/GameplayTagContainer.cs`,
test `GameplayTagContainerTests.Queries_DoNotAllocate`). Medians of 5 container-only runs before and after (server):

| Server | Before | After | Change |
|---|---|---|---|
| GC per frame, mean / p95 | 39.2 / 71.2 KB | 23.7 / 54.8 KB | −40 % / −23 % |
| Allocations per frame, mean | 582 | 339 | −42 % |
| Garbage collections per 90 s window | 94–97 | 60–62 | −36 % |
| `main_thread_ms` p95 | 2.53 ms | 2.27 ms | −10 % |
| `tick_ms` p99 | 1.72 ms | 1.64 ms | −5 % |

Bots and the desktop client did not change (their allocations are elsewhere). The server budgets were tightened to
the new medians (`set-budgets -Roles server`). Two of the eight runs failed a budget that the change does not touch
(server `tx_bytes_s` p95 116 KB/s, server `physics_queries` p95 68): both follow how many hazards spawn near the ship,
which is random per run (`SkyHazardUseCase` uses an unseeded `System.Random`). See the plan's risks.
