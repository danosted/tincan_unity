#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Every scenario the harness can run by name (<c>-scenario &lt;name&gt;</c>). Scenarios are code, versioned and
    /// reviewed with the feature they test. Add one per feature slice; see .docs/NETWORK_TEST_HARNESS.md.
    /// </summary>
    public static class ScenarioCatalog
    {
        /// <summary>
        /// Proof scenario for the harness itself, on a feature that already works: the server hands the subject a
        /// net and spawns a can in front of it; the subject swings with real input; both sides confirm the catch.
        /// </summary>
        public static readonly ScenarioEntry NetCatch = new(
            new Scenario.Builder("NetCatch")
                .InScene(TestScenes.NetCatch)
                .Describe("Subject swings a net at a can spawned in reach; the can disappears and the jerry-can supply grows.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(2f, "settle after spawn")
                    .Do("RecordJerryCanSupply")
                    .Do("GiveSubjectNet")
                    .Wait(0.5f, "net replicates")
                    .Do("SpawnCanAtSubjectNet"))
                .Act(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Carrying.Net")
                    .WaitUntil("CanInNetReach", 10f)
                    .Checkpoint("can-in-reach")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("NoCanInNetReach", 5f)
                    .Checkpoint("caught"))
                .Assert(s => s
                    .WaitUntil("JerryCanSupplyIncreased", 15f)
                    .Expect("JerryCanSupplyIncreased"))
                .Build(),
            builder => builder.Register<NetCatchScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Items grant and revoke abilities on the right peers. The server hands the subject the net. The subject sees
        /// it replicate, gets GA_SwingNet granted locally (prediction) and swings. The server sees the swing, swaps the
        /// net for a jerry can, and checks that the swing ability and net tag are gone while starting abilities survive.
        /// The subject checks the same on its side, then the server empties the subject's hands.
        /// </summary>
        public static readonly ScenarioEntry EquipCycle = new(
            new Scenario.Builder("EquipCycle")
                // Its items (net, jerry can) belong to the minigame and fuel features, which Test_NetCatch loads.
                .InScene(TestScenes.NetCatch)
                .Describe("Equip net -> swing -> swap to jerry can -> unequip; grants and visuals follow on server and owner.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("SubjectHolds", "none")
                    .Do("EquipSubject", "ITEM_CatchingNet"))
                .Act(s => s
                    .WaitUntil("SubjectHolds", 5f, "ITEM_CatchingNet")
                    .WaitUntil("SubjectHasAbility", 3f, "GA_SwingNet")
                    .WaitUntil("SubjectHasTag", 3f, "State.Carrying.Net")
                    .Expect("SubjectVisualShown", "Carry_Net")
                    .Checkpoint("net-held")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("SubjectHolds", 10f, "ITEM_JerryCan")
                    .WaitUntil("SubjectLacksAbility", 3f, "GA_SwingNet")
                    .WaitUntil("SubjectLacksTag", 3f, "State.Carrying.Net")
                    .WaitUntil("SubjectHasTag", 3f, "State.Carrying.JerryCan")
                    .Expect("SubjectHasAbility", "GA_Sprint")
                    .Expect("SubjectVisualShown", "Carry_JerryCan")
                    .Expect("SubjectVisualHidden", "Carry_Net")
                    .Checkpoint("can-held")
                    .WaitUntil("SubjectHolds", 10f, "none")
                    .WaitUntil("SubjectLacksTag", 3f, "State.Carrying.JerryCan")
                    .Expect("SubjectVisualHidden", "Carry_JerryCan"))
                .Assert(s => s
                    .WaitUntil("SubjectHasAbility", 5f, "GA_SwingNet")
                    .WaitUntil("SubjectHasTag", 10f, "State.Net.Swinging")
                    .Do("EquipSubject", "ITEM_JerryCan")
                    .Expect("SubjectLacksAbility", "GA_SwingNet")
                    .Expect("SubjectLacksTag", "State.Carrying.Net")
                    .Expect("SubjectHasTag", "State.Carrying.JerryCan")
                    .Expect("SubjectHasAbility", "GA_Sprint")
                    .Wait(3f, "subject observes the can")
                    .Do("UnequipSubject")
                    .Expect("SubjectHolds", "none")
                    .Expect("SubjectLacksTag", "State.Carrying.JerryCan"))
                .Build(),
            builder => builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// The mandatory core boots on its own: in Test_Core, whose profile loads only the base features, a player spawns
        /// on host and client with empty hands and its core starting ability (sprint), on its own peer and on the server.
        /// Guards the composition itself: core installers always load, and no core service needs a feature.
        /// </summary>
        public static readonly ScenarioEntry CoreBoot = new(
            new Scenario.Builder("CoreBoot")
                .InScene(TestScenes.Core)
                .Describe("Core-only scene: the player spawns on host and client, holds nothing and can sprint.")
                .Timeout(60f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn"))
                .Act(s => s
                    .Expect("SubjectHolds", "none")
                    .WaitUntil("SubjectHasAbility", 5f, "GA_Sprint")
                    .Checkpoint("booted"))
                .Assert(s => s
                    .Expect("SubjectHolds", "none")
                    .WaitUntil("SubjectHasAbility", 5f, "GA_Sprint"))
                .Build(),
            builder => builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Ship damage end to end, without the repair tool: the server breaks part 0 near the spawn. The subject sees the
        /// marker, the ship's damaged tag and the HUD line. The server sees the fuel leak (even though nobody drives),
        /// then restores the part, and both sides see the leak, tag, marker and HUD line go away.
        /// </summary>
        public static readonly ScenarioEntry ShipDamage = new(
            new Scenario.Builder("ShipDamage")
                .InScene(TestScenes.ShipDamage)
                .Describe("Break part 0 -> marker, damaged tag, HUD and fuel leak on both peers -> restore -> all clear.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("PointHealthy", "0")
                    .Expect("LeakRateAtMost", "0")
                    .Do("RecordFuel")
                    .Do("BreakPoint", "0"))
                .Act(s => s
                    .WaitUntil("PointBroken", 5f, "0")
                    .WaitUntil("MarkerShown", 3f, "0")
                    .WaitUntil("ShipHasTag", 3f, "State.Ship.Damaged")
                    .WaitUntil("PointHasTag", 3f, "0:State.Damaged")
                    .WaitUntil("HudShows", 3f, "Hull breaches")
                    .WaitUntil("CueActive", 3f, "Cue.Ship.Part.Broken")
                    .WaitUntil("CueActive", 3f, "Cue.Ship.Leak")
                    .WaitUntil("CueCount", 3f, "Cue.Ship.Part.Break:Execute:1")
                    .Checkpoint("part-broken")
                    .WaitUntil("PointHealthy", 30f, "0")
                    .WaitUntil("MarkerHidden", 3f, "0")
                    .WaitUntil("ShipLacksTag", 3f, "State.Ship.Damaged")
                    .WaitUntil("PointLacksTag", 3f, "0:State.Damaged")
                    .WaitUntil("HudHidden", 3f, "Hull breaches")
                    .WaitUntil("CueInactive", 3f, "Cue.Ship.Leak")
                    .WaitUntil("CueCount", 3f, "Cue.Ship.Part.Broken:Removed:1")
                    .WaitUntil("HudShows", 3f, "Part repaired")
                    .Expect("CueCount", "Cue.Ship.Part.Break:Execute:1")
                    .Checkpoint("part-restored"))
                .Assert(s => s
                    .WaitUntil("LeakRateAbove", 3f, "0")
                    .WaitUntil("FuelDroppedBy", 10f, "0.5")
                    .Wait(3f, "subject observes the break")
                    .Do("RestorePoint", "0")
                    .WaitUntil("LeakRateAtMost", 3f, "0")
                    .Expect("ShipLacksTag", "State.Ship.Damaged")
                    .Expect("PointHealthy", "0")
                    .WaitUntil("CueCount", 3f, "Cue.Ship.Part.Broken:Removed:1")
                    .Expect("CueCount", "Cue.Ship.Part.Break:Execute:1"))
                .Build(),
            builder => builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// The whole repair loop with real input: the server breaks part 0, hands the subject the repair tool and stands it
        /// next to the part (players share one spawn point, so the client would otherwise stand on the host's head). The subject gets GA_RepairShip granted locally, holds Primary, and the server —
        /// seeing State.Repairing from the replicated input — repairs the part it faces until it is whole. Both sides
        /// then see the part healthy, the marker gone and the leak stopped.
        /// </summary>
        public static readonly ScenarioEntry RepairLoop = new(
            new Scenario.Builder("RepairLoop")
                .InScene(TestScenes.ShipDamage)
                .Describe("Break part 0 -> give the repair tool -> hold Primary facing it -> part whole, leak stopped, on both peers.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("BreakPoint", "0")
                    .Do("EquipSubject", "ITEM_RepairTool")
                    .Do("PlaceSubjectAtPoint", "0"))
                .Act(s => s
                    .WaitUntil("SubjectHolds", 5f, "ITEM_RepairTool")
                    .WaitUntil("SubjectHasAbility", 3f, "GA_RepairShip")
                    .WaitUntil("SubjectHasTag", 3f, "State.Carrying.RepairTool")
                    .WaitUntil("PointBroken", 5f, "0")
                    .Wait(0.5f, "teleport settles; facing follows the look input again")
                    .WaitUntil("SubjectFacesPoint", 5f, "0")
                    .Expect("SubjectVisualShown", "Carry_RepairTool")
                    .Checkpoint("tool-ready")
                    .Hold(6f, ScriptedAction.Primary)
                    .WaitUntil("PointHealthy", 3f, "0")
                    .WaitUntil("MarkerHidden", 3f, "0")
                    .WaitUntil("SubjectLacksTag", 3f, "State.Repairing")
                    .WaitUntil("CueInactive", 3f, "Cue.Player.Repairing")
                    .Expect("CueCount", "Cue.Player.Repairing:Active:1")
                    .WaitUntil("CueCount", 3f, "Cue.Ship.Part.Broken:Removed:1")
                    .Checkpoint("repaired"))
                .Assert(s => s
                    .WaitUntil("SubjectHasTag", 15f, "State.Repairing")
                    .WaitUntil("CueActive", 3f, "Cue.Player.Repairing")
                    .WaitUntil("PointHealthy", 12f, "0")
                    .WaitUntil("LeakRateAtMost", 3f, "0")
                    .Expect("ShipLacksTag", "State.Ship.Damaged")
                    .Expect("PointLacksTag", "0:State.Damaged"))
                .Build(),
            builder =>
            {
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Late join sees gameplay tags: the host breaks part 0 while no client is connected (the client joins after a
        /// delay), so the part's State.Damaged and the ship's State.Ship.Damaged were set before the client existed. The
        /// joining client must see both tags, not only the replicated health and marker. Solo runs check the host only.
        /// </summary>
        public static readonly ScenarioEntry ShipDamageLateJoin = new(
            new Scenario.Builder("ShipDamageLateJoin")
                .InScene(TestScenes.ShipDamage)
                .Describe("Host breaks part 0 before the client joins -> the late client sees the part's and ship's damage tags.")
                .Timeout(90f)
                .JoinLate(12f)
                .Arrange(s => s
                    .WaitUntil("PointHealthy", 20f, "0")
                    .Do("BreakPoint", "0")
                    .WaitUntil("PointHasTag", 3f, "0:State.Damaged")
                    .WaitUntil("ShipHasTag", 3f, "State.Ship.Damaged")
                    .Expect("NoRemotePlayers")
                    .WaitUntil("SubjectReady", 60f))
                .Act(s => s
                    .WaitUntil("PointBroken", 10f, "0")
                    .WaitUntil("MarkerShown", 3f, "0")
                    .WaitUntil("PointHasTag", 3f, "0:State.Damaged")
                    .WaitUntil("ShipHasTag", 3f, "State.Ship.Damaged")
                    .WaitUntil("CueActive", 3f, "Cue.Ship.Part.Broken")
                    .WaitUntil("CueActive", 3f, "Cue.Ship.Leak")
                    .Expect("EntitiesIdentified")
                    .Checkpoint("late-join-broken"))
                .Assert(s => s
                    .Wait(5f, "subject checks what it joined into")
                    .Expect("PointHasTag", "0:State.Damaged")
                    .Expect("ShipHasTag", "State.Ship.Damaged")
                    .Expect("EntitiesIdentified"))
                .Build(),
            builder => builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Aim pitch travels in the input: the subject stands behind broken part 0, which floats at about eye height, and
        /// turns its camera. Looking 30 degrees down, a narrow EyeAim scan misses the part; looking level, it finds it,
        /// on the subject's own peer and on the server, which only knows the pitch from the replicated input. The server
        /// checks both poses in order, so it cannot pass on the default pitch of 0.
        /// </summary>
        public static readonly ScenarioEntry AimPitch = new(
            new Scenario.Builder("AimPitch")
                .InScene(TestScenes.ShipDamage)
                .Describe("Look down 30 deg -> EyeAim scan misses part 0; look level -> it hits, on the owner and on the server.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("BreakPoint", "0")
                    .Do("PlaceSubjectAtPoint", "0"))
                .Act(s => s
                    .WaitUntil("PointBroken", 5f, "0")
                    .Wait(0.5f, "teleport settles; facing follows the look input again")
                    .WaitUntil("SubjectFacesPoint", 5f, "0")
                    .Do("SetSubjectPitch", "30")
                    .WaitUntil("SubjectAimPitch", 3f, "30")
                    .Expect("EyeScanMisses", "0")
                    .Checkpoint("looking-down")
                    .Wait(3f, "hold the pose while the server checks")
                    .Do("SetSubjectPitch", "0")
                    .WaitUntil("SubjectAimPitch", 3f, "0")
                    .Expect("EyeScanHits", "0")
                    .Checkpoint("looking-level")
                    .Wait(3f, "hold the pose while the server checks"))
                .Assert(s => s
                    .WaitUntil("SubjectAimPitch", 20f, "30")
                    .Expect("EyeScanMisses", "0")
                    .WaitUntil("SubjectAimPitch", 10f, "0")
                    .Expect("EyeScanHits", "0"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Interaction through targeting, with the real key: the subject stands by the repair tool rack and turns its
        /// camera to it; its prompt (TD_Interact on its own peer) shows the rack. It presses Interact (a predicted input
        /// bit): the server, acquiring the target itself from that tick's pose, hands out the tool; a second press puts it
        /// back. No client-chosen target is involved.
        /// </summary>
        public static readonly ScenarioEntry InteractRack = new(
            new Scenario.Builder("InteractRack")
                .InScene(TestScenes.ShipDamage)
                .Describe("Face the tool rack -> prompt shows it -> press Interact -> server gives the tool -> press again -> returned.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("SubjectHolds", "none")
                    .Do("PlaceSubjectNear", "RepairToolRack"))
                .Act(s => s
                    .WaitUntil("SubjectNear", 45f, "RepairToolRack")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "RepairToolRack")
                    .WaitUntil("InteractTargetIs", 5f, "RepairToolRack")
                    .Checkpoint("facing-rack")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_RepairTool")
                    .Wait(0.5f, "let go")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "none"))
                .Assert(s => s
                    .WaitUntil("SubjectHolds", 20f, "ITEM_RepairTool")
                    .Expect("InteractTargetIs", "RepairToolRack")
                    .WaitUntil("SubjectHolds", 10f, "none"))
                .Build(),
            builder =>
            {
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Designed events POC: the server starts the HullStress catalog event; after its "Groan" phase it breaks parts 0
        /// and 2 through the ship-damage handlers, which both peers see through normal replication. The server repairs them
        /// and the event succeeds on its BrokenPartsAtMost(0) condition. Event state itself is server-local for now.
        /// </summary>
        public static readonly ScenarioEntry HullStressEvent = new(
            new Scenario.Builder("HullStressEvent")
                .InScene(TestScenes.ShipDamage)
                .Describe("Server starts HullStress -> parts 0 and 2 break on both peers -> repaired -> event succeeds.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("PointHealthy", "0")
                    .Expect("PointHealthy", "2")
                    .Do("StartEvent", "1")
                    .Expect("EventPhase", "Groan")
                    .WaitUntil("EventPhase", 6f, "Break")
                    .Expect("PointBroken", "0")
                    .Expect("PointBroken", "2")
                    .Expect("HudShows", "Event"))
                .Act(s => s
                    .WaitUntil("PointBroken", 10f, "0")
                    .WaitUntil("PointBroken", 3f, "2")
                    .WaitUntil("MarkerShown", 3f, "0")
                    .Checkpoint("event-broke-parts"))
                .Assert(s => s
                    .Wait(1f, "subject saw the breaks")
                    .Do("RestorePoint", "0")
                    .Expect("EventPhase", "Break")
                    .Do("RestorePoint", "2")
                    .WaitUntil("EventOutcome", 3f, "Succeeded")
                    .Expect("EventPhase", "none"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<DesignedEventsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The cannon with real input: the subject walks up to the cannon, presses Interact to man it (it stays in its
        /// body: State.Occupying.Cannon, which turns the Gunner input context on), aims the barrel along its rest
        /// direction, and the server puts a target on the arc the barrel now points along. One Fire press shoots; the
        /// server's per-tick sweep hits the target and shoots it down, and the subject's peer drew the ball. Then the mouse
        /// swings the barrel to its yaw limit (held there, not wrapped) while the body keeps facing where it was, and Leave
        /// steps away. Plans: cannon-and-hazards.md, input-contexts.md.
        /// </summary>
        public static readonly ScenarioEntry CannonShot = new(
            new Scenario.Builder("CannonShot")
                .InScene(TestScenes.Cannon)
                .Describe("Man the cannon -> target on the barrel's arc -> Fire -> target shot down, ball drawn -> aim to the limit, body still -> Leave.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("HazardField", "off")
                    .Do("PlaceSubjectAtCannon"))
                .Act(s => s
                    .WaitUntil("SubjectAtCannon", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceCannon")
                    .WaitUntil("InteractTargetIs", 5f, "CannonStation")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Cannon")
                    .WaitUntil("CannonManned", 5f)
                    .Do("AimCannon", "0")
                    .Checkpoint("manned")
                    .WaitUntil("HazardsVisible", 15f, "1")
                    .Hold(0.3f, ScriptedAction.GunnerFire)
                    .WaitUntil("BallsShown", 5f, "1")
                    .WaitUntil("HazardsVisible", 10f, "0")
                    .Checkpoint("shot-down")
                    // The Gunner context: the mouse swings the barrel to its limit (never wrapping) and the body holds still.
                    .Do("RecordSubjectFacing")
                    .Hold(1f, ScriptedAction.GunnerAimRight)
                    .Expect("GunnerAimYaw", "105")
                    .Expect("SubjectFacingHeld")
                    .Checkpoint("aimed-to-limit")
                    .Hold(0.3f, ScriptedAction.GunnerLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Cannon"))
                .Assert(s => s
                    .WaitUntil("CannonManned", 60f)
                    .Wait(1.5f, "the barrel follows the subject's aim")
                    .Do("SpawnTargetOnArc", "35")
                    .WaitUntil("HazardsDestroyed", 15f, "1")
                    .WaitUntil("CannonFree", 15f))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<CannonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        public static readonly ScenarioEntry HazardStrike = new(
            new Scenario.Builder("HazardStrike")
                .InScene(TestScenes.Cannon)
                .Describe("A hazard drifts at the ship from starboard -> it hits -> the ship loses health on every peer, the hazard is gone.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("HazardField", "off")
                    .Do("SpawnDriftingHazard", "30"))
                .Act(s => s
                    .WaitUntil("ShipHealthBelow", 30f, "1000")
                    .Checkpoint("hit-seen"))
                .Assert(s => s
                    .WaitUntil("HazardHits", 30f, "1")
                    .WaitUntil("ShipHealthBelow", 5f, "1000")
                    .WaitUntil("HazardsVisible", 5f, "0"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<CannonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The server arrives, the subject restarts from its end screen (a server RPC from the client), the server sinks
        // the ship. Lanes run side by side and meet on the replicated voyage phase.
        public static readonly ScenarioEntry VoyageLoop = new(
            new Scenario.Builder("VoyageLoop")
                .InScene(TestScenes.Voyage)
                .Describe("Voyage underway -> arrive -> end screen on both peers -> the subject presses Restart -> briefing, " +
                          "ship whole -> sink the ship -> Lost on both peers.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("VoyagePhase", 5f, "Briefing")
                    .Do("VoyageCastOff")
                    .WaitUntil("VoyagePhase", 5f, "Underway")
                    .Do("VoyageArriveNow"))
                .Act(s => s
                    .WaitUntil("VoyagePhase", 60f, "Arrived")
                    .WaitUntil("MenuShown", 5f, "voyage_arrived")
                    .Checkpoint("arrived")
                    .Do("RecordVoyage")
                    .Do("PressMenuItem", "restart")
                    .WaitUntil("VoyageAdvanced", 10f)
                    .WaitUntil("VoyagePhase", 60f, "Lost")
                    .WaitUntil("MenuShown", 5f, "voyage_lost")
                    .Checkpoint("lost"))
                .Assert(s => s
                    .WaitUntil("VoyagePhase", 60f, "Briefing")
                    .WaitUntil("ShipHealthFull", 5f)
                    .Do("VoyageCastOff")
                    .WaitUntil("VoyagePhase", 5f, "Underway")
                    .Do("SinkShip")
                    .WaitUntil("VoyagePhase", 5f, "Lost"))
                .Build(),
            builder => builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        private static readonly ScenarioEntry[] All = { NetCatch, EquipCycle, CoreBoot, ShipDamage, RepairLoop, ShipDamageLateJoin, AimPitch, InteractRack, HullStressEvent, CannonShot, HazardStrike, VoyageLoop };

        public static System.Collections.Generic.IReadOnlyList<ScenarioEntry> Entries => All;

        public static string Names => string.Join(", ", All.Select(entry => entry.Scenario.Name));

        public static bool TryGet(string? name, out ScenarioEntry entry)
        {
            entry = All.FirstOrDefault(candidate => string.Equals(candidate.Scenario.Name, name, StringComparison.OrdinalIgnoreCase))!;
            return entry != null;
        }
    }
}
