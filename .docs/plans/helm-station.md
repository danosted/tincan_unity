Status: Draft (parked 2026-09-29: today's possession-based helm works; revisit if piloting becomes a problem)

# Piloting the airship: possession stays (parked)

## Decision
The helm keeps using **possession**. Piloting was briefly planned as a crew **station** like the cannon (`IStation`:
stay in your body, an occupy ability, the ship steered from the player's own input). The developer chose possession:
- **A clear actor boundary.** The helm or ship is its own actor, with its own abilities, attributes and GAS
  controller. The player takes it over instead of collecting temporary grants.
- **Its own input scheme and UI,** switched by what is possessed, not by tags.
- **AI can take over the same actor** later through the same interface, without a fake humanoid in a seat.

The **cannon stays on the occupancy model.** It is a shallow actor, and occupancy fits it. Where that cut-off lies
(shallow station versus possessed actor) is decided per feature.

## If piloting becomes a problem: the path
Today's possession ties control to network ownership. Taking the helm transfers ownership of the whole airship
`NetworkObject` to the pilot's client, which writes `AirshipInputState` into the ship's network variable. Steering is
server-simulated with one-way latency. The possessor is a client id, so only players can possess, and the pilot's body
is left behind. The direction, in slices:

1. **Possession as a server-side controller assignment,** not an ownership transfer:
   - a controller identity that can be a player or (later) an AI;
   - the possessor's input routed to the server;
   - a seat, so the possessor's body stays at the helm.
2. **A generic client-side prediction core,** extracted from the humanoid's (`Core/Humanoid/HumanoidPrediction.cs`,
   `HumanoidInputBuffer`, the input and acknowledgement RPCs in `HumanoidPlayer`):
   - an `IPredictedActor<TInput, TState>` contract: capture state, restore state, step one tick;
   - redundant unreliable input sent to the server, accepted only from the current possessor (not "owner");
   - a per-tick input buffer on the server;
   - state and last processed sequence sent back to the possessor;
   - history and replay on a mismatch;
   - correction smoothing;
   - proxies keep interpolating.

   The ship suits this: it moves kinematically (`AirshipControllerView.Simulate` sets its transform), so a replay is
   cheap and deterministic. Humanoid prediction is already platform-local, so riders cope with a ship the pilot
   predicts slightly ahead. Offsets on server-spawned objects are visible to the pilot only.
3. **The ship as a predicted possessable actor,** with its own input struct.
4. **A helm fixture** possessed from its seat (its own feature assembly and installer), replacing
   `AirshipControlPanel` in the airship prefab.
5. **A `HelmSteer` scenario,** solo and host + client with lag: take the helm, steer, release. This also covers the
   untested "ship clears its input on release" path in `AirshipNetworkMediator.AuthoritativeSetPossessor`.
6. **Later, separately:** migrate the humanoid onto the generic prediction core, with its prediction tests as the
   safety net, so there is one prediction system.

Open questions to settle then:
- the camera while piloting (the player's orbit camera or a helm view camera);
- the pitch keys (Jump / Sprint today, a test mapping);
- AI scope: only the controller abstraction first; no AI helmsman is planned.
