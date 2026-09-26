# Network test harness

A host has zero latency to itself, so a bug in how a *remote* client feels (sluggish movement, rubber-banding)
cannot be seen by pressing Play and Start Host. The harness reproduces a remote client in the Editor, plays a
fixed input route with a bot, and writes numbers you can compare between changes.

Code: `Assets/Scripts/DevTools/` (assembly `TinCan.DevTools`), registered by
`Assets/Resources/Installers/NetTestHarnessFeatureInstaller.asset`. With none of the flags below it registers
nothing, so normal play is unaffected.

## Flags

Every flag works on a build's command line or as a **Multiplayer Play Mode player tag**
(`Core/Domain/LaunchArguments.cs`). A tag `name` means `-name`; a tag `name:value` (or `name=value`) means
`-name value`.

| Tag | Command line | Effect |
|---|---|---|
| `autohost` | `-autohost` | Start Host on launch (`Features/UI/CommandLineSessionBootstrap.cs`). |
| `autojoin` | `-autojoin [address[:port]]` | Join on launch; defaults to `127.0.0.1:7777`. |
| `netsim:<preset>` | `-netsim <preset>` | Delay, jitter and loss on this peer's outgoing packets (see presets). |
| `bot:<route>` | `-bot <route>` | Play a scripted input route once the local player exists. Implies `telemetry`. |
| `telemetry` | `-telemetry` | Measure the local player and write a report. |

**Presets** (`DevTools/NetworkConditionPresets.cs`). Each peer delays only what it sends, so give host and client
the same preset: the round trip is then twice the send delay.

| Preset | Send delay | Jitter | Loss | Round trip |
|---|---|---|---|---|
| `Lag50` | 25 ms | 3 ms | 0% | ~50 ms |
| `Lag100` | 50 ms | 5 ms | 1% | ~100 ms |
| `Lag200` | 100 ms | 10 ms | 2% | ~200 ms |
| `Lossy` | 50 ms | 15 ms | 5% | ~100 ms |

The presets use UTP's built-in simulator layer, which NGO enables in the Editor and in development builds. No
extra package is needed.

**Routes** (`DevTools/BotRoutes.cs`). Moves are paired (forward then back), so the player stays on the deck.

| Route | Run it on | What it does |
|---|---|---|
| `DeckWalk` | client | Starts, stops, strafes, jumps and sprints, three times over (~50 s). |
| `Pilot` | host | Takes the helm through the server's possession authority, then cruises, turns both ways and brakes (~58 s). The deck moves under the client. |
| `Idle` | either | Stands still for 30 s: measures drift and snaps. |

## One-time setup (Multiplayer Play Mode)

**TinCan > Dev > Net Harness > Apply Host + Client (Lag100)**
(`DevTools/Editor/NetHarnessPlayerTagsMenu.cs`) does it in one click:
- It gives **Player 1** (this Editor) `autohost`, `netsim:Lag100`, `bot:Pilot`, and **Player 2** `autojoin`,
  `netsim:Lag100`, `bot:DeckWalk`.
- It launches Player 2 if it is not running. After an Editor restart MPPM remembers Player 2 as active but does not
  launch it until it is activated again, so run the menu once per Editor session.

From the shell:

```bash
unity cmd menu --path "TinCan/Dev/Net Harness/Apply Host + Client (Lag100)"
```

**Clear Tags** returns to the manual Start Host menu. MPPM has no public API for tags, so the menu reaches into its
internals and logs an error if a Unity upgrade moves them. The fallback is by hand: create the tags under **Project
Settings > Multiplayer > Playmode**, then assign them in **Window > Multiplayer > Multiplayer Play Mode**.

## Running a measurement

Press Play, or from the shell:

```bash
unity cmd editor_play
```

Both peers connect on their own and the bots start. After about a minute each peer writes its report to
`Logs/net-telemetry/` (git-ignored): a timestamped file plus `latest-host.json` and `latest-client1.json`. The
Console also gets a `[NetTelemetry]` line every 10 s and a one-line JSON report at the end. Then:

```bash
unity cmd editor_stop
```

**F3** toggles the live overlay in each game view.

## Reading a report

Everything is measured on the locally **rendered** pose, in the local space of the platform the movement layer says
the player stands on. Ship motion itself never counts as player motion. Results are split into `shipStill` and
`shipMoving`.

| Field | Meaning | Physics floor |
|---|---|---|
| `start` | Move input on until the player visibly moves 2 cm. **The "sluggish" number.** | ~40 ms (acceleration), plus up to one tick |
| `stop` | Move input off until speed visibly halves. | ~175 ms (deceleration) |
| `jump` | Jump pressed until the player visibly rises 5 cm. | about one tick |
| `snaps` / `maxSnapM` | Frames that moved faster than any legal motion (teleports, hard corrections). | 0 |
| `reversals` | Direction flips while input is steady: prediction fighting a correction. | 0 |
| `avgRttMs` | Transport round trip on a client. | ~2 × preset send delay |

Latency lines read `p50 / p95 / max (n=samples, timeouts)`. A timeout means no response within 2 s.

The host's own player is local, so its numbers are the floor. The client's numbers are what a remote player feels.

## Extending

- **New route:** add a `BotRoute.Builder` chain to `BotRoutes.cs` and list it in `All`.
- **New preset:** add it to `NetworkConditionPresets.cs`.
- **New metric:** extend `MovementResponseTracker` (pure, tested in `NetTestHarnessTests.cs`) and
  `ResponseSummary`.
