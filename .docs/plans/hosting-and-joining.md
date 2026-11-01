Status: Approved (2026-10-31); last in [core-systems-roadmap.md](core-systems-roadmap.md)

# Hosting and joining without a vendor's servers

## Why
Joining works today through the network service: Host, or Join with an address and port, on the same PC or the LAN.
Over the internet the host must forward UDP 7777 or everyone joins a VPN, and a friend needs the host's IP. There is
no list of games, no player names, no version check, no reconnect. Playtests are not blocked (LAN, a VPN, or the
dedicated server container all work), but every remote session costs setup.

Constraint (developer, 2026-10-31): use Unity's packages where they help, but do not depend on Unity's (or any
vendor's) online infrastructure. Whatever needs a server runs on a server we run. That rules out Unity's Multiplayer
Services SDK: its sessions, codes and relay all live in Unity's cloud.

## Today (verified 2026-10-31)
- `INetworkService` (`Core/Domain/Networking/`), implemented by `NGONetworkService` (`Network/Infrastructure/`):
  `SetConnection`, `SetListenEndpoint`, `StartHost`, `StartServer`, `StartClient`, `Shutdown`, over NGO and
  `UnityTransport` (UDP).
- Callers: `StartHostMenuCommand`, `JoinGameMenuCommand` (the `Menu_Join` address and port fields),
  `CommandLineSessionBootstrap` (`-autohost`, `-autojoin`, `-server`, `-joindelay`), `ShipyardUseCase` (hosts solo).
  `SessionRequest` / `SessionRequestKind` carry address and port.
- No connection approval: any build of any version can connect, with no name, no password and no player cap.
- Dedicated server: a Linux build in a Podman container, joined by address ([dedicated-server-container.md](dedicated-server-container.md)).
- The harness, MPPM and the perf bots join by address (`verify.ps1`, `perf.ps1`).
- A voyage idles until a crew is aboard, and late joiners board the ship ([crew-gate-and-boarding.md](crew-gate-and-boarding.md)),
  so no lobby screen is needed before play.

## What we can use without anyone's servers
| Piece | What it gives | Infrastructure |
|---|---|---|
| NGO connection approval | version check, name, password, player cap, a refusal reason | none |
| LAN discovery (UDP broadcast) | a list of games on the local network | none |
| UPnP / NAT-PMP port mapping on the host | opens the port on most home routers automatically | none (the player's router). A .NET Standard 2.0 library: Open.Nat.V2 (3.2.1, 2026-02) or Mono.Nat (2.0.1); to check in Unity before choosing |
| Address codes | the host's public address and port packed into a short code (`TC-4F7Q-K2MB`), so nobody reads out an IP | finding the public address needs one STUN request: to any public STUN server (a stateless standard, nothing is kept), or to our own on the VPS |
| Our dedicated server on a VPS | always reachable, no NAT problems, works when UPnP fails | our VPS running the existing container |
| A self-hosted overlay VPN (Headscale with Tailscale clients, or a self-run ZeroTier controller) | everyone on one virtual LAN | our VPS; zero game code |
| UTP's DTLS encryption with our own certificate | encrypted traffic to our server | none |

Not covered without a server of our own: hosts behind carrier-grade NAT or with UPnP switched off. For them the answer
is our dedicated server (or the VPN). NAT hole-punching would need a rendezvous service and transport work that
`UnityTransport` does not do; it is listed under Later and not recommended now.

Platform layers (Steam, Epic Online Services) are someone else's infrastructure too. They can sit behind the same seam
later if TinCan ships on a store; nothing here depends on them.

## Design

### The seam: `ISessionService` (Core.Domain/Networking)
- `HostAsync(HostOptions)` (name, max players, password, LAN only or internet), `JoinAsync(JoinTarget)` (an address, an
  address code, or a discovered game), `Discover()` (LAN games, a live list), `LeaveAsync()`, `Current` (address code
  when we host, players with names) and events (`PlayerJoined`, `PlayerLeft`, `Ended(reason)`).
- `DirectSessionService` in `TinCan.Network`, over `INetworkService`. The only implementation for now. A Steam or
  rendezvous implementation could sit next to it later.
- Menu commands and `CommandLineSessionBootstrap` call the seam; `-joincode <code>` joins by code.

### Connection approval (core, `TinCan.Network`)
- The client sends a payload: protocol version (from the build), player name ([player-settings.md](player-settings.md)),
  password hash.
- The server checks them in an approval processor (pure, tested): version mismatch, wrong password, full, banned
  name, each with a refusal reason the client shows in the menu.
- The approved name becomes the player's replicated name (the nameplates in
  [crew-pings-and-markers.md](crew-pings-and-markers.md) read it).

### Hosting over the internet
1. On "Host (internet)", the host asks the router for a UDP mapping of its port (UPnP, then NAT-PMP), with a lease
   renewed while hosting and removed on shutdown.
2. It learns its public address (from the router's reply, or one STUN request) and shows the address code in the pause
   menu, with a copy button.
3. If mapping fails, the menu says so plainly and offers the alternatives: forward the port by hand, use the VPN, or
   play on the dedicated server.
4. Joining by code decodes the address and port and joins directly. `AddressCodeProcessor` (encode, decode, a
   checksum against typos) is pure and tested; IPv6 gets a longer code.

### LAN discovery
The host answers a broadcast on a fixed UDP port with name, players, version and whether a password is needed. The Join
menu lists them live. The dedicated server answers too, so a LAN server shows up without typing an address.

### Our dedicated server
Running the existing container on a VPS is an operations step, not code: a short guide in
`Container/server/README.md` (open port, restart policy, update by pulling a new image). The server gets a name and an
optional password through launch flags, and shows in LAN discovery when it is on the LAN.

### Drops
"Host left" and "Connection lost" end in a menu message. "Rejoin last game" remembers the last target. The player
rejoins as a new client and boards like any late joiner.

## Slices
1. **S1 Seam and approval:** `ISessionService`, `DirectSessionService`, the approval processor and payload, refusal
   messages, names. Callers moved; scenarios unchanged (the harness sends a valid payload). EditMode tests for the
   approval processor, the seam, the bootstrap parsing.
2. **S2 LAN discovery:** the responder, the Join list. A scenario step checks that the client discovers the host in
   MPPM.
3. **S3 Internet hosting:** port mapping (after a yes on the library), public address, address codes, the fallback
   messages. Manual check on two networks.
4. **S4 Our server:** server name and password flags, the VPS guide, rejoin last game.
5. **Later, only if S3 is not enough:** a small rendezvous service of our own (a game list over the internet, NAT
   hole-punching); a Steam implementation of the seam if TinCan goes to Steam.

## Decisions (developer, 2026-10-31)
1. **Last in the roadmap.** Joining by address works for playtests now; the other core systems come first.
2. **Port mapping: Open.Nat.V2** (a 2026 release, UPnP and NAT-PMP, no dependencies; Mono.Nat has not moved in
   years). S3 starts by checking that it works in the Mono player build.
3. **Public address: the router's reply first, then a public STUN server as a check.** If the two differ, the host is
   behind a second NAT (often the provider's) and an opened port is useless: the menu says "use the server or the
   VPN" instead of handing out a code that fails silently. STUN keeps nothing and needs no account. Our own STUN server
   replaces it once there is a VPS.
4. **No VPS yet.** Port mapping and codes should cover most home connections at no cost. A VPS comes when a playtest
   hits a host that cannot map its port, or when testers want a server that is always up. Until then the container
   runs on the developer's PC (port forwarded by hand when needed).
5. **No password at first.** Approval checks version and player cap; a password comes when someone needs one.

## Verification
- EditMode suite green, plus the new tests. `verify.ps1 -All` after S1 (asked for first): joining by address unchanged
  for every scenario, solo and host + client.
- S2: discovery step in a scenario. S3 and S4: a manual run between two networks (host, code, join, wrong version,
  wrong password, full, host quits, rejoin) and against the VPS server.
