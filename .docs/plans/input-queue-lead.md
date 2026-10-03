Status: Approved

# Input latency: client time lead and the server input queue

## Context

Measured 2026-10-03 (`.docs/PERFORMANCE.md`, "seeded runs and the dedicated server's input latency"): a client's input
is acknowledged ~98 ms after it is sent, even with server and clients on one PC. A 60 fps server frame cap saves
~16 ms; the rest is the server's **input queue**. Each client's queue on the server stands at 2–4 inputs, and the
latency follows the depth (~2.2: 82 ms, ~3: 98 ms, ~4: 115–130 ms).

How input flows today (code):
- Every client tick the owner builds one `HumanoidInputState` with a sequence number, predicts locally, and sends it
  unreliably with the last 4 attached (`HumanoidPlayer.SendInput`, `RedundantInputs = 4`).
- The server consumes exactly one input per client per tick (`HumanoidInputBuffer.Consume`). It trims the queue only
  above 4 (`DefaultMaxQueued`), repeats the last input when starved, and carries one-shot bits over skipped inputs.
- NGO runs the client's clock ahead of the server's: `NetworkTimeSystem.Sync` sets the client offset to
  *server time as last received + ½ RTT + LocalBufferSec*, with `LocalBufferSec = 1 / TickRate` (33 ms).
  With a correct RTT an input for tick T arrives one tick before the server simulates T: queue depth ~1.

**Hypothesis:** the RTT NGO uses is inflated. It comes from `UnityTransport.GetCurrentRtt`, which UTP estimates from
reliable-packet acknowledgements; our input stream is unreliable, so the estimate goes stale (already noted in
`NETWORK_TEST_HARNESS.md`). Bot logs report `rtt 185–230 ms` against a server on the same PC (real: ~1 ms). With
RTT ≈ 200 ms the client leads by ~100 + 33 ms, its inputs arrive 3–4 ticks early, and they wait in the queue. The
queue never drains because the lead is steady and trimming only starts above 4. This affects every remote client:
dedicated server or a menu-hosted listen server.

## Goal

Hold each client's server queue at a small target (1–1.5 inputs) under real conditions (latency, jitter, loss),
without more prediction corrections. Expected: input ack ~98 → ~30–40 ms on a LAN (−2 ticks from the queue, −16 ms
with a 60 fps server cap); the same absolute saving over the internet.

## Decisions (recommended; review them)

1. **Server-driven queue control (B) over a better RTT (A).** The server knows each client's real queue depth; it
   puts it in the owner snapshot, and the client slowly adjusts its lead (`NetworkTimeSystem.LocalBufferSec`, a
   public setting) to hold the target. It adapts to jitter, loss and slow machines and does not depend on RTT
   accuracy. This is the common approach in shipped server-authoritative games. A better RTT (A) can follow if
   other systems need it.
2. **Keep server trimming as a safety net (C)**, unchanged at first: trimming skips inputs the client already
   predicted, which forces corrections, so it must stay rare.
3. **Server frame cap:** decide separately whether the default goes from 30 to 60 fps (−16 ms, +24 % server CPU).
   Measured independently, so it can ship on its own.

## Phases

### Phase 0: Confirm the hypothesis (dev-only, no gameplay change)
On each client, record NGO's RTT estimate (`GetCurrentRtt`) and the actual lead (`LocalTime − ServerTime`), next to
the server's per-client queue depth and the input ack. Prediction: lead ≈ ½ RTT estimate + 33 ms, and
queue depth ≈ (lead − real one-way time) / 33 ms. Runs: CrewLoad (no netsim) and the net harness with `Lag100`.
If the numbers do not line up, stop and rethink before Phase 1.

### Phase 1: Report the queue to the owner
- `HumanoidInputBuffer`: a smoothed depth (exponential average over ~1 s), next to the existing stats.
- `HumanoidMovementSnapshot` (server → owner, every tick): one byte, the smoothed depth in tenths, capped.
- EditMode tests: depth smoothing, snapshot round trip.

### Phase 2: Client lead controller
- A pure processor (`InputLeadProcessor`, Core.Humanoid): given the reported depth, the target and the tick time,
  returns the adjustment to `LocalBufferSec`. Slow and bounded: at most a few ms per second, a dead band around the
  target, a floor (never below 0) and a ceiling.
- `HumanoidPlayer` (owner only) feeds it each snapshot and applies the result to `NetworkManager.NetworkTimeSystem`.
- Starvation (server repeats inputs) pushes the lead up quickly; a deep queue lowers it slowly.
- EditMode tests for the processor: converges to the target, holds within the dead band, reacts to starvation, never
  leaves the bounds.

### Phase 3: Verify and tune
- CrewLoad perf runs (`botAckMs`, `inputQueue.client<n>`) and the net harness at none / `Lag50` / `Lag100` / `Lossy`
  (ack latency, corrections, mean correction distance, starved and skipped counts).
- `verify.ps1 -All` (13 scenarios, host + client).
- Pass: queue depth ~1–1.5, starved near 0 after connect, corrections not up, ack down ≥ 50 ms on a LAN, all green.
- Re-set the perf budgets afterwards (traffic and timing change slightly).

### Phase 4 (separate decision): server frame cap default 60

## Risks and open questions
- **Too little lead starves the server** (input repeated, misprediction under jitter). Hence a target above 0, a slow
  controller and a fast reaction to starvation.
- **NGO overrides.** `NetworkTimeSystem.Sync` recomputes the desired offset from RTT on every sync; the controller
  adjusts `LocalBufferSec`, which Sync reads, so the two compose. Check that NGO's own hard reset (offset error
  above 200 ms) does not fight the controller.
- **Host's own player** is local (no queue); unaffected.
- **Ship (helm) input** goes a different path (possession); out of scope unless Phase 0 shows the same lead problem.
- **Phase 0 could prove the hypothesis wrong**, e.g. queue depth from frame timing rather than the lead.

## Progress
- 2026-10-03: drafted from the perf findings; Phase 0 next.
- 2026-10-03: **Phase 0 confirms the hypothesis.** Client perf reports now carry `ngo_rtt_ms` (NGO's RTT estimate) and
  `time_lead_ms` (`LocalTime − ServerTime`); `perf.ps1 -NetSim <preset>` runs the server and bots under the harness
  presets (verdict `EXPERIMENT`: budgets are set without simulated conditions).
  - No netsim (real RTT ~1 ms, container network): NGO's estimate 174–220 ms; lead 170–193 ms, equal to
    ½ estimate + 33 ms (local buffer) + 50 ms (server buffer) within 1 ms for every bot; server queue 3.0–3.7; ack
    82–117 ms. The worst estimate has the deepest queue and the slowest ack.
  - `Lag100` (real RTT ~100 ms): estimate 185–330 ms; lead again equal to the formula; queue 3.0–3.6 (trimmed at 4,
    skipped 0–2, starved 5–6); ack 214–245 ms, i.e. ~120 ms over the real round trip.
  - Source of the estimate: UTP's reliable pipeline (`ReliableUtility`: ack time − send time − reported processing
    time). Our hot traffic is unreliable, so reliable acks are sparse and slow; the estimate inflates. Option B does
    not depend on it.
- 2026-10-03: **Phases 1–3 built** (the developer: "the input queue is worth putting some effort into", then
  "continue"; option B as recommended).
  - Phase 1: `HumanoidInputBuffer.SmoothedDepth` (exponential average, 1/30 per tick); one byte on
    `HumanoidMovementSnapshot` (`QueueDepthTenths`), set by the server in `HumanoidPlayer.PublishAuthoritativeState`.
  - Phase 2: `InputLeadProcessor` (Core.Humanoid, pure) steers NGO's `LocalBufferSec` from the reported depth;
    `HumanoidPlayer.SteerInputLead` on the owner, every snapshot. `-noinputlead` turns it off. Tests:
    `InputLeadProcessorTests` (incl. a closed-loop model with the measured lags), `HumanoidInputBufferTests`
    (smoothed depth, wire tenths).
  - Phase 3: CrewLoad, 2 runs per setting (bot telemetry: ack, corrections; server: queue, starved):

    | Setting | Ack, no lag | Ack, Lag100 | Corrections (no lag / Lag100) | Starved (no lag / Lag100) |
    |---|---|---|---|---|
    | off | 98 ms | 230 ms | 7.0 % / 6.9 % | 20 / 44 |
    | target 1.5 ± 0.5 | 68.5 ms | 186.5 ms | 7.9 % / 6.9 % | 20 / 53 |
    | **target 1.2 ± 0.3 (kept)** | **39 ms** | **175 ms** | **5.2 % / 6.6 %** | 23 / **122** |

    Kept 1.2 ± 0.3: 55–60 ms less input latency, fewer and smaller corrections. Cost: under Lag100 jitter the
    server starves more (122 of ~27,000 ticks, ~0.45 %), without more corrections. If that shows as jitter in play,
    move the target up (1.3–1.5) or raise faster on starvation.
  - Open: the server frame cap default (Phase 4, ~16 ms more); a playtest on the LAN and over the internet;
    `Lossy` and `Lag200` runs.
  - Bug found on the way: the first version read `-noinputlead` in a static initializer of `HumanoidPlayer`. In the
    Editor that reads MPPM player tags before MPPM is ready, throws, and leaves the type unusable, so every scenario
    timed out. Now read on first use in Play; trap added to `CODE_MAP.md`. Player builds were unaffected (no tags
    outside the Editor), so the perf numbers above stand.
  - Checked: 693/693 EditMode; `verify.ps1 -All` 13/13 solo, 12/13 host + client in the batch (HazardStrike's client
    did not join in time once), HazardStrike then passed host + client twice on its own.
