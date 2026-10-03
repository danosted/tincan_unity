Status: Approved

# Performance budgets and continuous perf checks

## Goal
Keep an eye on performance continuously instead of finding problems in playtests. Six load areas each get a repeatable
run that produces numbers and a pass/fail verdict against a hard budget:

1. **Spawn stress**: many sky hazards, cannon fire and flying cans at once.
2. **Full crew load**: four players at stations, firing and moving.
3. **Physics density**: many active colliders and raycasts.
4. **Rendering**: a worst-case view with everything on screen.
5. **Networking**: peak message rate and bandwidth with four clients.
6. **Memory over time**: a long soak to catch leaks and GC spikes.

Perf checks reuse the scenario harness (`.docs/NETWORK_TEST_HARNESS.md`): bots, launch flags, JSON reports,
and the same `.tools` modules `verify.ps1` is built from. They are not a second framework.

## Decisions (recommended options; review them)
1. **Measure first, then set budgets.** Every budget starts as "baseline + margin", from 3–5 runs on the reference
   setup. Budgets are tightened when performance improves and never loosened silently: loosening one needs a line in
   the Progress log saying why. The numbers in [Starting budgets](#starting-budgets) are starting guesses until the baseline replaces them.
2. **Gate on percentiles, not averages.** Report p50 / p95 / p99 / max for every per-frame value. Budgets check p95
   or p99, plus a separate "hitches" count (frames over 2× budget). Averages hide what players feel.
3. **Containers by default; the desktop only for GPU timing.** One aim is that perf runs don't take over the desktop
   or the Editor while the developer works.
   - **Containers (Linux)** run the dedicated server, the headless bot clients and, if the P0 spike works, a
     rendering *counter* client (see [Rendering in a container](#rendering-in-a-container)). Podman gives fixed specs
     (`--cpus`, `--cpuset-cpus`, `--memory`), so server cost, script GC, bandwidth and draw-call counts are repeatable
     and nothing opens on the desktop.
   - **The Windows client build** on the reference PC measures only GPU-bound **timings**: frame time under render
     load. That run opens a window, so it is the one run that is scheduled (nightly, or by hand), not run on every
     change. It moves to the LAN PC later.
   - **The Editor** runs the cheap early-warning version (same scenarios, looser thresholds, never the gate): Editor
     overhead and MPPM clones sharing the CPU make its numbers noisy.
   - **Builds don't occupy the Editor.** Perf builds run in batch mode from a separate git worktree
     (`git worktree add ../tincan_perf`), so building never blocks the open Editor. A worktree has its own `Library/`,
     so the first build there is slow.
4. **A separate development "perf" build.** `-netsim` (UTP's simulator) and some `ProfilerRecorder` counters only
   work in development players, and `PlayerBuild` makes release builds today (`BuildOptions.None`). Add a perf
   variant (`BuildOptions.Development`, no deep profiling, no profiler autoconnect) for the Linux server/client and the
   Windows client. A dev build runs slightly slower than release, but always the same amount slower, so its numbers
   are safe to compare. Release builds keep shipping as they do now.
5. **One sampler, many scenarios.** One `PerfSampler` in `TinCan.DevTools` records every counter; each area is a
   scenario (or bot route + scenario) that sets up its load. The report goes next to the movement telemetry.
6. **Local first, CI later.** The reference PC runs the gate through `perf.ps1`. A CI runner is a later step once
   budgets are stable; nothing here should assume one.
7. **Budgets are code, results are data.** Budget definitions live in git, reviewed in diffs and tied to commits.
   Measurements are JSON files on disk, one folder per run, for history and comparison. No database until the files
   stop being enough. See [Budgets and results](#budgets-and-results).

## What containers do and don't control
- **Do:** CPU count and quota (`--cpus 2`), core pinning (`--cpuset-cpus 2,3`, which keeps the Editor and other apps
  off those cores), memory ceiling (`--memory 1g`, which also makes a leak fail loudly), and identical images per
  commit (tag with the commit hash, as the server image already should be).
- **Don't:** turn this PC into a slower machine. A CPU quota throttles in bursts, which is not the same as a slower
  CPU, so "2 cores" in a container is not a low-spec stand-in. Everything shares one physical CPU, cache and memory
  bandwidth with the host, so runs still need a quiet machine. Podman's WSL2 machine has its own overhead, so
  container numbers are only compared with other container numbers.
- **Network shaping:** use the existing `-netsim` presets (needs the dev build). `tc netem` inside the container
  network is an alternative if the UTP simulator turns out unrepresentative; not planned.
- **Engine: Podman** (chosen by the developer; its machine is rootful on cgroups v2 with the cpuset, cpu and memory
  controllers, so `--cpus`, `--cpuset-cpus` and `--memory` work). The server image is built and run with it too.
  With the WSL backend the VM's CPU and memory come from `.wslconfig`, not `podman machine set`; record the WSL
  limits in the environment profile. Docker Desktop is also installed; numbers from it would not be comparable.

## Rendering in a container
GPU access in a container is possible but doesn't give representative timings here:
- `--gpus all` is NVIDIA/CUDA only; this PC has an AMD RX 9070 XT.
- WSL2's vendor-neutral path is `/dev/dxg` plus Mesa's D3D12 driver (OpenGL on D3D12; Vulkan through "dozen"), the
  same one WSLg uses. A container can mount `/dev/dxg` and `/usr/lib/wsl` and run the Linux player on it. But that is
  Linux + OpenGL/Vulkan translated to D3D12: not the D3D11/12 path players have, and frame times would measure the
  translation layer.
- Windows containers (native DirectX through `--device class/...`) need Windows Pro/Enterprise; this PC runs Home.

What a container *can* give is **counts**: draw calls, SetPass calls, triangles and visible renderers depend on
the scene, the camera and URP's culling and batching, not on GPU speed. A Linux dev client rendering off-screen
(Xvfb with Mesa llvmpipe as a software renderer, or the D3D12 path above) at a fixed resolution and camera pose
should report the same counts on every run. **P0 spike:** check that URP runs on it and that its counts match the
Windows client's within a small margin (the graphics API can change SRP Batcher grouping). If so, the rendering
*count* budgets run in containers on every change and only frame *timing* needs the desktop.

## Budgets and results

### Shape
- **Environment profile**: where a number came from. `ref-desktop` (this PC: Ryzen 7 7800X3D, 8C/16T, 31 GB,
  RX 9070 XT, Windows 11, 1920×1080), `ctr-server-2c` (server container: 2 pinned cores, 1 GB), `ctr-bot-1c`,
  `ctr-render-sw`, `editor`. Numbers are compared only within one profile. A profile is a named, versioned
  definition (cores, memory, engine, build variant), not free text.
- **Budget**: `(scenario, role, profile, metric, stat) → comparator, limit, warn limit, rationale, set-at commit`.
  Example: `CrewLoad / server / ctr-server-2c / tick_ms / p95 ≤ 10, warn 8`.
  Lives in `.docs/perf/budgets.json`, edited by hand or written by a "set from baseline" command and reviewed in the diff.
- **Run**: one execution. Commit, dirty flag, branch, timestamp, scenario, environment profile, build variant,
  engine and Unity version, duration, verdict.
- **Measurement**: `(role, metric, stat, value, unit)`, plus per-minute samples for the soak.

### On disk
One folder per run under `Logs/perf/runs/` (git-ignored), named so a directory listing sorts by time:

```
Logs/perf/runs/2026-10-02T21-14-05_CrewLoad_ee5e164/
  run.json        # the run record: commit, profile, scenario, verdict, budget results
  server.json     # one report per role, written by the game's PerfSampler
  bot1.json ...
  client.json
  soak-server.csv # per-minute samples (soak only)
```

The game writes the role reports into a mounted folder; the runner collects them and writes `run.json` with the
verdict. The report format is the stable part: whatever comes later reads these files.

After a run the runner checks budgets in two ways:
1. **Absolute**: against `budgets.json` (the gate: fail or warn).
2. **Relative**: against the median of the last N passing runs on the same profile (a drift warning, never a fail).
   This catches a slow creep that stays inside the budget.

`.\.tools\perf.ps1 compare <runA> <runB>` and `perf.ps1 trend <scenario> <metric>` read the run folders and print
tables. At the scale of a few runs a day this is plenty.

**When to add a database:** when the folder scan gets slow, when dashboards (Grafana) are wanted, or when a second
machine (the LAN PC, CI) writes results. Then SQLite, or Postgres in a container, loaded from the existing run
folders. Not before.

### Starting budgets
Starting guesses; P2 replaces them with baseline + margin. Reference: `ref-desktop` at 1920×1080, target 60 fps
(confirmed with the developer). Server tick rate is 30 Hz (`StartServer` caps the headless loop to it).

| Area | Metric | Where measured | Starting budget |
|---|---|---|---|
| All (client) | Frame time p95 / p99 | Windows client | ≤ 16.6 ms / ≤ 20 ms |
| All (client) | Hitches (frame > 33 ms) per minute, after warm-up | Windows client | ≤ 1 |
| All | GC alloc per frame in steady state, p95 | client + server | 0 B (p99 ≤ 1 KB while adding things) |
| All (server) | Server time per tick p95 | Linux server container | ≤ 10 ms (of 33 ms) |
| Physics | Physics step time per fixed update p95 | client + server | ≤ 4 ms |
| Physics | Raycasts / queries per fixed step | server | record only, budget after baseline |
| Rendering | Draw calls / SetPass calls in the worst view | render container (Windows client if the spike fails) | baseline + 10 % |
| Rendering | Triangles in the worst view | render container (Windows client if the spike fails) | record only |
| Networking | Bytes/s per client, sent and received, p95 | server + clients | baseline + 20 % |
| Networking | Messages/s per client, RPCs by type | server | baseline + 20 % |
| Spawn | Max live hazards before the client budget breaks | Windows client | ≥ designed peak × 2 |
| Soak | Managed heap slope after 10 min warm-up | server + clients | ≈ 0 (≤ 1 MB per 10 min) |
| Soak | Live `NetworkObject` count at voyage end vs start | server | equal |

(URP's SRP Batcher changes what "batches" means, so the rendering budget uses SetPass and draw calls, not batches.)

## Pieces

### P0. Perf builds and headless clients
- `PlayerBuild`: a perf variant (Development) for **Linux Server** and **Windows Client**, output
  `Builds/LinuxServerPerf/`, `Builds/Win64Perf/`, built through the build module.
- **Headless bot clients.** Try the Linux server build as a client first: `-autojoin <server> -bot <route> -batchmode
  -nographics`. Roles are decided at runtime (dedicated-server plan, decision 1), so this may just work. If it
  doesn't (stripped shaders, `UNITY_SERVER` code paths, input), build a Linux *player* with `-nographics` instead.
- Containers started by the Container module with `podman run`: one server and N bots from the same image, each with an explicit
  `--cpus` / `--cpuset-cpus` / `--memory`, a `-bot` route, and a volume for reports. No compose file: `podman compose`
  needs a separate provider, and the module needs per-container arguments anyway. Cores split on the 8C/16T reference PC: server 2, bots 1 each, the rest left to the
  desktop and the Editor.
- **Batch-mode builds from a worktree** (decision 3), so a perf build never waits on the open Editor.
- **Render-counter spike** ([Rendering in a container](#rendering-in-a-container)): a Linux dev client under Xvfb +
  llvmpipe, `WorstView` pose, counts compared with the Windows client. Decides where rendering count budgets run.

### P1. `PerfSampler` and report
- `DevTools/Perf/PerfSampler.cs`: `ProfilerRecorder`s for main-thread time, GC allocated in frame, GC count, physics
  step, draw calls, SetPass calls, triangles, managed heap used/reserved, total native allocated. Ring buffer, no
  allocation per sample. Server side: time per network tick (a timestamp around the tick, not a frame counter).
- **Verify the counter names and which ones a release vs development player exposes** against the 6000.4 docs and
  the installed Editor before relying on them; don't use remembered names.
- `DevTools/Perf/PerfReport.cs`: p50/p95/p99/max, hitches, warm-up cut-off; written as JSON to `Logs/perf/` with
  `latest-<role>.json`, plus a one-line `[Perf]` summary in the log (so `podman logs` shows it).
- `-perf` flag (and an MPPM tag `perf`) turns the sampler on; with no flag nothing is registered, like the harness.
- **Network counters.** NGO 2.11 has no detailed metrics without the Multiplayer Tools package. Check (P1 spike) whether
  UTP 2.7's driver statistics or NGO's public API give bytes and messages per client; otherwise wrap the transport
  with a counting `NetworkTransport` that delegates to `UnityTransport`. RPC counts by type come from a counter in
  the mediators' send path only if the transport view is not enough.
- EditMode tests: percentile and hitch maths, warm-up cut-off, slope fit for the soak.

### P2. Baseline
Run every area scenario 3–5 times per environment profile. The runs land in `Logs/perf/runs/`;
`perf.ps1 set-budgets --from-runs <ids> --margin <pct>` writes `.docs/perf/budgets.json` from their medians, replacing
the starting guesses. A diff of that file shows every intentional change in cost, with the commit that caused it.

### P3. Area scenarios
Each is a scenario in `ScenarioCatalog` with a perf assertion step that reads the sampler, run in a test-range
scene where that works and in `drm_cloud_environment` for rendering (the real art is what costs).
- **SpawnRamp**: the server raises the live hazard count in steps (10, 25, 50, 100, …), holds each for 10 s, and
  reports the step where the client budget first breaks. First finding to expect: hazards and flying cans are
  `Instantiate` + `Destroy` with no pool (`SkyHazardSpawningService`, `FlyingCanSpawningService`). Pooling through the
  existing `NetworkPrefabInterceptor` (an `INetworkPrefabInstanceHandler`) is the likely fix, decided from the numbers.
  Cannonballs are simulated arcs, not spawned objects, so cannon fire stresses the per-tick sweep, not spawning.
- **CrewLoad**: the server plus 3 headless bot clients in containers, one Windows client rendering. New bot routes:
  `Helm` (exists as `Pilot`), `Gunner` (take a cannon, aim, fire on a cadence), `Repairer`, `DeckRunner`. All four
  for 3 minutes during an active voyage with hazards.
- **PhysicsDensity**: hazards plus players on a moving, tilting deck (`PilotTilt`), every gunner aiming (aim
  highlight casts rays). Reports physics step time and query counts.
- **WorstView**: a fixed camera pose in the POC scene looking down the ship with clouds, hazards in view and four
  players. Measured only in the Windows client.
- **NetPeak**: the CrewLoad set-up under `Lag100`, reporting per-client bytes/messages and server send per tick.
- **Soak**: CrewLoad looping voyages for 60 min (overnight variant: 4 h). The sampler writes a row per minute;
  the verdict is the slope after warm-up and `NetworkObject` count equality. For a leak, take Memory Profiler
  snapshots at start and end and diff them (`com.unity.memoryprofiler`, add when first needed).

### P4. `perf.ps1` entry script
`verify.ps1` stays the feature loop and gets no perf switches. Perf gets its own entry script, assembled from the
tooling modules (see [Tooling modules](#tooling-modules)):
- `perf.ps1 run <Scenario>` builds the perf variant if it is older than the code, runs the container set-up,
  collects the reports into a run folder and checks `budgets.json` (absolute) and recent history (drift).
  `-Desktop` adds the Windows client timing run; `-Editor` runs the scenario in Play with looser thresholds as the
  quick check. Exit codes follow the existing convention (0 pass, 1 budget failed, 2 environment not usable).
- `perf.ps1 compare | trend | set-budgets` over the run folders.
- Container-only perf runs don't touch the Editor or the desktop, so they don't need the "ask first" rule of play
  tiers. `-Desktop` and `-Editor` runs do.

### Tooling modules
`.tools/` scripts are being split into PowerShell modules with thin entry scripts (separate plan, a prerequisite
for P0's build changes and P4). The perf work adds three modules there: building (perf variants, worktree batch
builds), Container (start and stop the containers, wait for reports, collect logs) and perf (run folders, budget checks, compare,
trend).

### P5. Docs
- `.docs/PERFORMANCE.md`: budgets, how to run each area, how to read a report, how to update the baseline.
- `.docs/NETWORK_TEST_HARNESS.md`: the `-perf` flag and new bot routes.
- `.tools/README.md`: perf builds and the perf containers.
- `.docs/CODE_MAP.md`: `DevTools/Perf` in the folder map.

## Order
P0 and P1 first (agreed with the developer; nothing is measurable without them), with the run folders and
`run.json` from P4 built alongside P1 so the first runs are already recorded. Then P2 on CrewLoad and NetPeak (the biggest risk for a co-op
game), then SpawnRamp and PhysicsDensity, then WorstView, and Soak last as an overnight run. P4 grows with each
scenario.

## Risks and open questions
- **Headless clients run uncapped.** Fixed 2026-10-03: batch-mode clients cap at 60 fps (`NGONetworkService.StartClient`);
  a bot now costs ~2 ms of main thread per frame on one pinned core.
- **Input-ack latency on a dedicated server.** No netsim: a client on this PC to the Podman server took 85–108 ms from
  input to server confirmation (2026-10-02); bots to the server inside the Podman network (no Windows forwarding,
  capped bots) took ~132 ms, rtt estimate ~185 ms (2026-10-03). So not Podman/WSL forwarding. About four 33 ms ticks:
  the likely cause is the server's 30 fps frame cap (`NGONetworkService.StartServer`), which reads the transport once
  per frame, plus the server input buffer. Experiment for the developer: cap the server at 60 or 120 fps (tick rate
  stays 30) and compare `ackLatencyMs` in the bot logs and `main_thread_ms` in the server report. Affects how a
  dedicated server feels, so it is a gameplay decision as much as a perf one.
- **One PC hosts everything.** The server, 3 bots and the rendering client share a CPU, so CrewLoad's client frame time
  is pessimistic. Pin containers to cores the client doesn't need, and compare runs only with each other. Moving
  the containers (or the desktop timing run) to the LAN PC is the clean answer, but it takes set-up time: a later
  step, only if the noise is too high.
- **Software-render counts may not match.** If llvmpipe or the D3D12 path can't run URP, or its counts drift from the
  Windows client's, rendering count budgets stay on the desktop run.
- **Reference hardware.** Budgets are only meaningful on the PC they were set on. Record CPU/GPU/driver in each report;
  a different machine needs its own baseline.
- **Headless client viability.** Unknown until P0 tries it; a separate Linux client build doubles the build time.
- **Network counter source.** Decided by the P1 spike; a transport wrapper is the fallback and is small.
- **Dev build vs release.** Dev builds are a bit slower; if a release-only regression ever matters, add a release
  frame-time spot check, not a second gate.
- **Flaky budgets.** If a budget fails without a code change, widen the run (more samples) before widening the budget.

## Progress
- 2026-10-02: drafted. The developer confirmed the reference setup (this PC, 1080p, 60 fps), the LAN PC as a later
  option, and P0 + P1 first. Added: containers by default with desktop runs only for GPU timing, the rendering
  container spike, and the budgets-in-git / results-on-disk split. Results stay JSON files in run
  folders (no database yet); Podman is the container engine (changed from Docker Desktop the same day).
- 2026-10-03 (overnight, developer away; to review): P0, P1, P2 (CrewLoad) and most of P4 and P5 built.
  - **Spikes.** Counter names read from `ProfilerRecorderHandle.GetAvailable` in 6000.4.5f1: there is no
    "Draw Calls Count" any more (draw calls are split by path and summed); `NetworkTickSystem.Tick` times the
    simulation (the game's scheduler runs inside it); NGO has a marker per message type. Network bytes come from UTP
    2.7's public `NetworkDriver.GetStatistics()` (no transport wrapper). The Linux server binary works as a headless
    bot client (`-autojoin -bot`), so no separate Linux client build.
  - **P0.** Perf (development) variants in `PlayerBuild` (`Builds/LinuxServerPerf`, `Builds/Win64Perf`, image
    `tincan-server-perf`, `build.ps1 -Perf`). Batch-mode clients cap at 60 fps (`NGONetworkService.StartClient`).
    `-botloop` repeats a bot route. Containers start from the Container module with `podman run` on a `tincan-perf`
    network (bots join `perf-server:7777`), pinned cores and memory limits, reports through a `/perf` mount.
    Deferred: worktree batch builds and the render-counter spike.
  - **P1.** `DevTools/Perf/` (`PerfOptions`, `PerfSeries`, `PerfReport`, `PerfSampler`), `-perf` registered by the
    harness installer, EditMode tests (`PerfTests.cs`, `BotRouteUseCaseTests` loop case). `-perfprofile <frames>`
    records a profiler capture of the window for diagnosis.
  - **P4.** `.tools/perf.ps1` (`run`, `list`, `compare`, `trend`, `set-budgets`) on `modules/TinCan.Perf.psm1`.
    Not yet: `perf.ps1 run -Editor`.
  - **P5.** `.docs/PERFORMANCE.md`; `NETWORK_TEST_HARNESS.md` flags; `.tools/README.md`; `CODE_MAP.md`.
  - **Build stalls found on the way.** Twice the queued perf build never started (30 minutes each) and pipeline evals
    failed with "main thread operation timed out". The developer traced it to a Windows UAC / firewall prompt waiting
    on the desktop, which blocks the Editor's main thread (first blamed on `EditorApplication.delayCall` and on Unity
    Hub not running; neither was the cause). The menus now build synchronously (simpler, not a fix), and `build.ps1`
    probes the main thread before building instead of trusting `editor_status`, which can answer from a cached
    heartbeat, and names a waiting prompt as the likely cause.
  - **Pilot runs** (5 container, 3 desktop) found two sampler bugs, fixed before the baseline: `GPU Frame Time`
    returns sentinels, and render counters read 0 on many frames at high frame rates.
  - **P2 baseline (CrewLoad).** 23 budgets in `.docs/perf/budgets.json` from 5 container-only and 3 `-Desktop` runs;
    three later runs passed with no drift. Numbers and findings: `.docs/PERFORMANCE.md`, "Baseline 2026-10-03".
    Pilot runs (before the sampler fixes) are kept in `Logs/perf/pilot-2026-10-03/`, out of the run history.
  - **Diagnosis.** `perf.ps1 run -ProfileFrames N` (server capture with allocation call stacks; verdict DIAGNOSIS, out
    of drift and set-budgets) and `perf.ps1 analyze-gc <run>`. First result: `GameplayTagContainer.HasTag` (LINQ
    `Any` with a closure) is 44 % of the server's per-frame allocation. Not fixed: game code, left for the developer.
  - **Drift** compares only runs of the same set-up (container-only or `-Desktop`) that did not fail; the first
    version compared bots against desktop runs and warned on noise.
  - **Next, for review:** fix `HasTag`/`HasAny`/`HasAll` and re-baseline the GC budgets down; decide the server frame
    cap (see Risks, input-ack latency); P3 routes (helm through interaction on a client, gunner, repairer) so CrewLoad
    is a real crew; SpawnRamp; WorstView; Soak; `perf.ps1 run -Editor`; the render-counter container spike.
