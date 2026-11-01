Status: Approved (2026-10-31)

# Core systems roadmap

## Why
A sweep on 2026-10-31 looked for systems a co-op game needs that the core does not have yet. Each system got its own
plan; this file only fixes the order and the reasons. Each plan's slices are still built and verified one at a time.

## Order
1. **[ui-and-menus.md](ui-and-menus.md), U0 to U2.** It is buggy now (layers drawing over each other, features
   fighting over one menu stack, five places setting the cursor), and every plan below adds screens on top of it.
2. **[player-settings.md](player-settings.md), S1 and S2.** Small, every player feels it, and it needs the menu rows
   from step 1.
3. **[audio-mixer.md](audio-mixer.md), S1 and S2.** Cheap while there are four clips. Its volumes need settings.
4. **[crew-pings-and-markers.md](crew-pings-and-markers.md).** Needs player names (from settings) and the world
   indicator layer (its own S1).
5. **[player-vitality.md](player-vitality.md).** Option B (knocked down, never killed), gas pockets as the only
   source, behind an installer so a playtest can switch it off.
6. **[crew-animation.md](crew-animation.md), S1 and S2.** Unity's Starter Assets humanoid as a placeholder body.
7. **[network-object-pooling.md](network-object-pooling.md), Phase 0.** One perf run. It can run at any point; the pool
   is built only if the numbers ask for it.
8. **[hosting-and-joining.md](hosting-and-joining.md), last.** Joining by address works for playtests now. No
   vendor's online infrastructure: connection approval, LAN discovery, router port mapping and address codes, and our
   own dedicated server as the fallback.

## Dependencies between the plans
- Settings S2, pings and hosting build their screens on UI U1 and U2.
- Audio volumes (audio S2) need settings S1 and S2.
- Pings need names: settings provides the name, and the owner sends it after spawn until hosting's connection
  approval takes it over.
- Vitality, animation and pooling depend on none of the others.

## Not in this roadmap
The open work in older plans keeps its own place: [main-game-state.md](main-game-state.md),
[first-voyage.md](first-voyage.md) Milestone B, [architecture-review.md](architecture-review.md) slice 6,
[performance-budgets.md](performance-budgets.md) and the drafts.
