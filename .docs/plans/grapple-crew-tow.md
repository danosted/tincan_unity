Status: Draft

# Grapple hook tows crew: fuel runs to the sky islands

An idea to test (developer, 2026-10-15). Nothing here is decided yet.

## Why
Put fuel canisters on the spawned sky islands, so fuel becomes a resource you go out and collect. A player holds one
item at a time, so a run needs two players: one with the harvester tool, one with the grapple hook to get both of them
back aboard. The hook becomes the way to bring crew aboard, not only yourself.

## Two ways to bring the second player along
1. **Pull a player.** Fire the hook at another player; reeling drags them towards the holder.
2. **Latch on.** A player grabs hold of another player and is dragged along while that player reels in with the hook.

They differ in who acts: in 1 the hook holder does it all (the other player can be passive or even downed); in 2 the
passenger takes part and the hook works as it does today.

## Things to check before choosing
- **Prediction.** The hook runs on the server and on the predicting owner through `IHumanoidMotionContributor`
  (`.docs/plans/grapple-hook.md`, Design). Both ideas move player B because of player A's input, which B's client does
  not have ahead of time. B's own prediction would fight the pull and correct a lot. Options: let the server drive B
  while towed (no prediction for that time), or accept corrections. Worth a short spike in host + client first.
- **Anchor on a moving body.** Today an anchor is fixed to the transform it hit. A player as the anchor (idea 1) or as
  the thing that follows (idea 2) moves every tick, and both peers have to agree on where it is.
- **Breaking it off.** Can the towed player let go? What happens when the line snaps or the holder lets go mid-air?
- **Arrival and climb.** The hop and the climb over the rail are tuned for one body; the towed player needs the same,
  or ends up hanging under the deck edge.
- **The resource loop.** The harvester tool does not exist yet, and with one item in hand it is not clear how the
  harvested canisters get back to the ship (carried after the trip, stowed, delivered by the tool). Needs its own plan;
  it likely builds on the fuel feature (`Features/Airship/Fuel/`) and on `.docs/plans/sky-islands.md`.

## Out of bounds: the cloud reset today
`Features/CloudBoundary/CloudBoundaryUseCase.cs` (`ResetFallenCharacters`): on the server, a character that falls
`CharacterResetDepth` (10 m) below the cloud surface is placed on the nearest airship's boarding pose. It was made to keep
play sessions intact. The reset only moves the body (`HumanoidMovementUseCase.ResetCharacter`). It does not drop what you
carry, there is no delay, and nothing is lost. **So today, jumping off an island takes you home for free, canister in
hand.** That makes the hook unnecessary for the trip back, and a crew-tow mechanic has nothing to solve until falling
costs something.

## Matrix: fun and complexity risk
Risk: **L** small and contained, **M** new state or new networking, **H** touches prediction or several features.

### Bringing crew back aboard
| Option | Fun, pros | Fun, cons | Complexity risk |
|---|---|---|---|
| T0: no tow, each player hooks themselves (a second hook, or racks on islands) | Simple and readable; nobody waits for someone else | No co-op moment; the two-player run is not needed | **L**: already exists |
| T1: pull a player | Big rescue moments ("fished out of the sky"); works on a passive player; pairs with a downed state | The towed player has no say; griefing (yanking crew off the deck); funny once, maybe tiresome | **H**: B moves from A's input, so B's client prediction fights the pull; the anchor moves every tick; B needs the arrival hop and the climb |
| T2: latch on to the hook holder | Both players act; piggyback is fun and readable; "hold on!" tension | Needs a latch control (a new input context); the latching player does little in the air | **M–H**: maybe A becomes B's moving platform (the movement already carries players with platforms), but B sees A one interpolation delay late, so expect some jitter; letting go and the line snapping need rules |

### Out of bounds (falling into the clouds)
| Option | Fun, pros | Fun, cons | Complexity risk |
|---|---|---|---|
| O0: keep the free teleport | Never frustrating; sessions stay intact | Falling means nothing; jumping off an island is a free ride home, so the hook and the tow are not needed | **L**: nothing to do |
| O1: teleport with a cost, the carried item is lost and the respawn waits a few seconds | The trip back matters; canisters are at stake; small change | The waiting player has a short dead time | **L–M**: drop or destroy what is carried on reset, a timer, a respawn wait on the HUD |
| O2: drifting in the clouds, crew can rescue you, then O1 after a timeout | The most co-op drama; gives T1 its best use | More rules; if both players fall, nobody can rescue; can drag on | **H**: a new character state, UI, rescue rules; depends on T1 |
| O3: the ship pays, the respawn costs fuel from the tank | Ties straight into the fuel loop; a shared cost, not a personal one | Abstract; a small cost is easy to ignore | **L**: the fuel attribute already exists; one event handler |

### How they fit together
- O0 with anything: the tow has no purpose. Changing the out of bounds comes first.
- O1 or O3 with T0: the cheapest test of "do fuel runs to the islands feel good".
- O1 with T2: the co-op version, at medium risk.
- O2 with T1: the most fun on paper, the most work, with the highest prediction risk.

## Decided in `.docs/plans/overboard-and-rescue.md` (developer, 2026-10-15)
- **Out of bounds:** a falling player chooses between O1 (respawn aboard, lose what you carry) and a new option, a
  leash: you hang behind the ship, climb your own rope, and later the crew reels you in at a winch. O2 and O3 were not
  chosen. The values (respawn wait, what happens to items) are config, set later.
- **Towing:** neither T1 nor T2 for now. The leash takes the place of hooking a falling player. Lock-on towing may
  come back later (listed as open there).

## Open questions for the developer
- Is the second player required by design on a fuel run, or only the easy way?
- Can the hook also pull other things (a canister, a loose item), or only players?
- The resource loop itself: the harvester tool, and how harvested canisters get back to the ship.
