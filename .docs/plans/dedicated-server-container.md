Status: Approved (2026-09-30)

# Dedicated server in a container

## Goal
Run TinCan as a headless dedicated server (no local player) in a Linux Docker container. Players join it from the
same PC and from other LAN machines with `-autojoin <ip>:7777` or the Join menu. The listen-server flow (`-autohost`,
the Host button) keeps working unchanged.

Depends on [`crew-gate-and-boarding.md`](crew-gate-and-boarding.md): without a crew the voyage must stay idle, and
joiners must board the ship. Build that first.

## Decisions (recommended options; review them)
1. **Runtime role checks, not Multiplayer Roles.** One code base and one set of installers; server-only and
   client-only behavior is decided at runtime, from `INetworkService` state and a new "headless" fact, not by
   stripping components per build (`com.unity.dedicated-server`). Fewer moving parts while the game is small. The
   first headless run (D3) decides how much guarding is needed; if it turns into many guards, revisit Multiplayer Roles.
2. **Dedicated Server build target, Mono.** Unity's *Linux Dedicated Server* subtarget builds a headless player (no
   renderer, no audio, `UNITY_SERVER` defined, `Application.isBatchMode` true). Mono cross-compiles from Windows with no
   extra toolchain; IL2CPP would need Unity's Linux toolchain package and is not worth it yet.
3. **One flag starts the server.** `-server [address][:port]` (default `0.0.0.0:7777`) sets the transport's listen
   address and port and calls `StartServer()`. It sits next to `-autohost` / `-autojoin` in
   `CommandLineSessionBootstrap` and uses the same parsing. The listen address stops depending on a prefab or scene
   override for the server case.
4. **The image copies a build; it does not build.** The Linux server is built on the Windows dev machine (Editor menu
   or batch script) into `Builds/LinuxServer/`, and the `Dockerfile` copies that folder into a slim Debian/Ubuntu
   image. Building inside a container would need a licensed Unity Editor image; not now.
5. **Logs go to stdout.** The entrypoint passes `-logFile -` so `docker logs` shows the Unity log.

## Pieces
### D1. Tooling
- Install the Unity module **Linux Dedicated Server Build Support** for 6000.4.5f1 (Hub, or the `unity` CLI's module
  install). Today only `windowsstandalonesupport` is installed under `PlaybackEngines/`.
- `DevTools/Editor/PlayerBuild.cs` (built as `ServerBuild.cs`, renamed when the client build joined it): menu **TinCan > Build > Linux Server** and a static `LinuxServerFromCommandLine()` for
  `-executeMethod`. Uses the build profile's scene list (scene 0 is `drm_cloud_environment`), subtarget Server,
  output `Builds/LinuxServer/TinCanServer.x86_64`.
- `.tools/build-server.ps1`: triggers the menu in the running Editor through `unity cmd`, or runs Unity in batch mode
  when no Editor has the project open.

### D2. Server start
- `INetworkService.SetListenEndpoint(string address, ushort port)` (Core.Domain), implemented in `NGONetworkService`
  by writing `ServerListenAddress` and `Port` on the `UnityTransport` (`SetConnectionData` keeps the connect address).
- `CommandLineSessionBootstrap`: a `-server` request kind; `Begin` sets the endpoint and calls `StartServer()`.
- `MainMenuBootstrap` already closes menus on `NetworkState.Server`; the startup open must not happen on a server
  started from the command line (check order: the bootstrap starts the server after the menu opens).
- Tests: `CommandLineSessionBootstrapTests` (parse `-server`, `-server :9000`, `-server 10.0.0.5:7777`, default),
  `NGONetworkServiceTests` (listen endpoint set, connect address kept).

### D3. Headless hardening (driven by the first run)
Run the Linux server build once, bare (WSL2 is enough: `./TinCanServer.x86_64 -server -logFile -`), join from the
Windows client, and fix what the log shows. Expected suspects:
- anything that assumes a local player or a camera on the server (`LocalClientId`'s player, possession camera and
  cursor responders, `Camera.main`, the station view presenter);
- presentation that is harmless but wasted (HUD presenters, cue presenter, beacon, UI Toolkit overlays, cloud
  visuals): gate them with a headless check where they throw or cost CPU; leave them if they are only idle;
- input (`InputSystemReader`) with no devices;
- the voyage end screen menu opening on the server.
The rule: gameplay rules stay role-free (they already run under `IsServer`); only presentation gets a headless
guard. Add `bool IsHeadless` to a Core.Domain service (for example `IRuntimeEnvironment`, reading
`Application.isBatchMode`) rather than scattering `Application.isBatchMode` so tests can fake it.

### D4. Container
- `Docker/server/Dockerfile`: `debian:bookworm-slim` (or `ubuntu:24.04`), `ca-certificates` and whatever the first
  run shows the player needs, a non-root user, `COPY Builds/LinuxServer /opt/tincan`, `EXPOSE 7777/udp`,
  `ENTRYPOINT ["/opt/tincan/TinCanServer.x86_64", "-server", "-logFile", "-"]`.
- `Docker/server/compose.yaml`: one service, `ports: ["7777:7777/udp"]`, `restart: unless-stopped`.
- `.dockerignore` so the build context is only `Builds/LinuxServer` (never `Library/` or `Assets/`); `Builds/` stays
  gitignored.

### D5. Verify
- Same PC: `docker compose up`, then `TinCan.exe -autojoin 127.0.0.1:7777`; a second client joins late and boards.
- LAN PC: `TinCan.exe -autojoin <host LAN IP>:7777`. With Docker Desktop on Windows, the published UDP port is
  forwarded by Docker's backend: allow it (or UDP 7777) through Windows Firewall on the host.
- Voyage: idle with nobody on board; starts when the first player spawns; stands down when everyone leaves.
- Human playtest: two players on two PCs through a full voyage, plus a late join mid-voyage.

### D6. Docs
- `.docs/Network_Initialization_Flow.md`: the three start modes (host, client, dedicated server) and the listen
  address.
- `.docs/CODE_MAP.md`: `-server` in "Command-line session start"; the stale trap "Standalone builds are blocked"
  (Windows player builds work now; `Builds/Win64` exists) replaced with the current state.
- `.tools/README.md`: `build-server.ps1` and the Docker commands.

## Progress
- 2026-10-01: D1, D2, D4 and D6 built; D3 needed one fix; D5 done on this PC, the LAN PC is left.
  - D1: `linux-server` module installed. An Editor started before the install built "successfully" but wrote no
    player (the postprocess threw `Build target 'StandaloneLinux64' not supported`); `PlayerBuild` now checks for the
    executable, and the Editor was restarted. `PlayerBuild` also builds the Windows client (**TinCan > Build >
    Windows Client**, `build-server.ps1 -Client`), because clients must match the server's code.
  - D2: `-server` + `INetworkService.SetListenEndpoint` (forced over NGO's `-port` / `-ip`); EditMode 664/664.
  - D3: the headless server boots in the container with no exceptions (only "no shaders included" notes, expected
    with Dedicated Server Optimizations). One fix: an uncapped headless loop used 3.5 cores idle;
    `StartServer` now sets `Application.targetFrameRate` to the tick rate (30): about 8–10 % of one core.
    No `IsHeadless` guard was needed, so no `IRuntimeEnvironment` was added.
  - D4: image 100 MB build on `debian:bookworm-slim`, no extra packages; `docker compose` up/down verified.
  - D5 (same PC): `TinCan.exe -autojoin 127.0.0.1:7777` joined the container; server log: the voyage began once the
    player existed, the player boarded, and when the client quit, "the crew left, standing down".
  - Left: a LAN PC join (host firewall for Docker Desktop's UDP 7777), a two-player voyage, and a late join mid-voyage.

## Risks and open questions
- **Unknown headless surface.** D3 can be small or large; it is sized by the first run, not guessed now.
- **Docker Desktop UDP from the LAN.** Port publishing on Windows goes through Docker's userland proxy; LAN clients
  depend on the firewall rule. If it misbehaves, run the container on a Linux host or use WSL2 mirrored networking.
- **Resource use.** A server still runs physics, the cloud surface query and NGO at the tick rate; measure CPU in the
  container before planning more than one instance per machine.
- **Restart with no one left.** Handled by the crew gate (the voyage returns to Idle). A server-level restart (reload
  the scene after N minutes empty) is out of scope.
- **Version skew.** Clients and server must be built from the same commit (NGO rejects a `NetworkConfig` mismatch).
  Tag the image with the commit hash.
