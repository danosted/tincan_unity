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
        /// LAN discovery: the host answers discovery while it serves the game, and the subject's peer (the client in host
        /// + client, the host itself solo) browses and finds a game of its own version on the game port, by broadcast or
        /// loopback. Plan: hosting-and-joining.md (S2).
        /// </summary>
        public static readonly ScenarioEntry LanDiscovery = new(
            new Scenario.Builder("LanDiscovery")
                .InScene(TestScenes.Core)
                .Describe("Host serves -> the subject's peer browses the LAN -> finds a game of its version on port 7777.")
                .Timeout(60f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("LanServing", 5f))
                .Act(s => s
                    .Do("BrowseLan")
                    .WaitUntil("LanGameFound", 10f, "7777")
                    .Do("StopBrowsingLan")
                    .Checkpoint("found"))
                .Assert(s => s
                    .Expect("LanServing"))
                .Build(),
            builder => builder.Register<SessionScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

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
                    .WaitUntil("CueCount", 3f, "Cue.Ship.Part.Break:Execute:1")
                    .Checkpoint("part-broken")
                    .WaitUntil("PointHealthy", 30f, "0")
                    .WaitUntil("MarkerHidden", 3f, "0")
                    .WaitUntil("ShipLacksTag", 3f, "State.Ship.Damaged")
                    .WaitUntil("PointLacksTag", 3f, "0:State.Damaged")
                    .WaitUntil("HudHidden", 3f, "Hull breaches")
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
        /// next to the part (players share one spawn point, so the client would otherwise stand on the host's head). The subject gets GA_RepairShip granted locally and pulls Primary; the server —
        /// seeing State.Repairing from the replicated input — opens the faced part's repair check, and a Perfect makes it whole (R1). Both sides
        /// then see the part healthy, the marker gone and the leak stopped.
        /// </summary>
        public static readonly ScenarioEntry RepairLoop = new(
            new Scenario.Builder("RepairLoop")
                .InScene(TestScenes.ShipDamage)
                .Describe("Break part 0 -> give the repair tool -> pull Primary facing it -> Perfect repair check -> part whole, leak stopped, on both peers.")
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
                    .Hold(0.4f, ScriptedAction.Primary)
                    .WaitUntil("SkillCheckRunning", 10f)
                    .Checkpoint("check-open")
                    .WaitUntil("SkillCheckInPerfect", 5f)
                    .Tap(ScriptedAction.SkillCheckPress)
                    .WaitUntil("PointHealthy", 5f, "0")
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
                    .WaitUntil("SkillChecksGraded", 3f, "Perfect:1")
                    .WaitUntil("LeakRateAtMost", 3f, "0")
                    .Expect("ShipLacksTag", "State.Ship.Damaged")
                    .Expect("PointLacksTag", "0:State.Damaged"))
                .Build(),
            builder =>
            {
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipDamageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<SkillCheckScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
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
                    .WaitUntil("TargetOutlined", 2f, "RepairToolRack")
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
                builder.Register<TargetOutlineScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
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
        /// direction, and the server puts a target on the line the barrel now points along. One Fire press charges the beam; the
        /// server's one sweep hits the target and shoots it down, and the subject's peer drew the beam. Then the mouse
        /// swings the turret past its back (the yaw wraps) while the body keeps facing where it was, and Leave steps away.
        /// Plans: cannon-and-hazards.md, input-contexts.md, hazards-and-boons.md (C1), ray-cannons.md (R1).
        /// </summary>
        public static readonly ScenarioEntry CannonShot = new(
            new Scenario.Builder("CannonShot")
                .InScene(TestScenes.Cannon)
                .Describe("Man the cannon -> target on the barrel's line -> Fire -> the beam charges and fires -> target shot down, beam drawn -> turn past the back, body still -> Leave.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtCannon"))
                .Act(s => s
                    .WaitUntil("SubjectAtCannon", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceCannon")
                    .WaitUntil("InteractTargetIs", 5f, "CannonStation")
                    .WaitUntil("TargetOutlined", 2f, "CannonStation")
                    .Checkpoint("facing-cannon")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Cannon")
                    .WaitUntil("CannonManned", 5f)
                    .Do("AimCannon", "0")
                    .Checkpoint("manned")
                    .WaitUntil("HazardsVisible", 15f, "1")
                    .Hold(0.3f, ScriptedAction.GunnerFire)
                    .WaitUntil("BeamsShown", 5f, "1")
                    .WaitUntil("HazardsVisible", 10f, "0")
                    .Checkpoint("shot-down")
                    // A turret: an aim past the back wraps (190 is -170), and the server's barrel follows it there. The
                    // mouse sweep per frame depends on frame rate, so the mouse wrap itself is an EditMode test
                    // (GunnerAimUseCaseTests); here the mouse swings the barrel and the body holds still.
                    .Do("RecordSubjectFacing")
                    .Do("AimCannonAt", "190 0")
                    .Expect("GunnerAimYaw", "-170")
                    .WaitUntil("CannonYaw", 5f, "-170")
                    .Checkpoint("aimed-past-the-back")
                    .Hold(1f, ScriptedAction.GunnerAimRight)
                    .Expect("SubjectFacingHeld")
                    .Do("AimCannon", "0")
                    .Hold(0.3f, ScriptedAction.GunnerLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Cannon"))
                .Assert(s => s
                    .WaitUntil("CannonManned", 60f)
                    .Wait(1.5f, "the barrel follows the subject's aim")
                    .Do("SpawnTargetOnBeam", "35")
                    .WaitUntil("HazardsDestroyed", 15f, "1")
                    .WaitUntil("CannonFree", 15f))
                .Build(),
            builder =>
            {
                builder.Register<TargetOutlineScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<CannonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        public static readonly ScenarioEntry HazardStrike = new(
            new Scenario.Builder("HazardStrike")
                .InScene(TestScenes.Cannon)
                .Describe("A hazard on the deck -> it touches the ship -> the ship loses health on every peer, the hazard is gone.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("SpawnHazardOnDeck"))
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

        /// <summary>
        /// A misfire: the subject mans the starboard cannon, turns the turret inboard and down at its own deck (yaw 180,
        /// the lowest elevation), and fires. The beam hits our own ship (the deck or the port cannon) and the ship loses
        /// health on every peer (GE_SpiritBeamHit, applied to the ship). Plans: hazards-and-boons.md (C1), ray-cannons.md (R1).
        /// </summary>
        public static readonly ScenarioEntry CannonMisfire = new(
            new Scenario.Builder("CannonMisfire")
                .InScene(TestScenes.Cannon)
                .Describe("Man the cannon -> turn it inboard at our own deck -> Fire -> the beam hits our ship -> ship health drops on every peer.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtCannon"))
                .Act(s => s
                    .WaitUntil("SubjectAtCannon", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceCannon")
                    .WaitUntil("InteractTargetIs", 5f, "CannonStation")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Cannon")
                    .WaitUntil("CannonManned", 5f)
                    .Do("AimCannonAt", "180 -15")
                    .Wait(0.5f, "the barrel turns inboard")
                    .Checkpoint("aimed-at-own-deck")
                    .Hold(0.3f, ScriptedAction.GunnerFire)
                    .WaitUntil("BeamsShown", 5f, "1")
                    .WaitUntil("ShipHealthBelow", 10f, "1000")
                    .WaitUntil("CueCount", 3f, "Cue.Hit:Execute:1")
                    .Checkpoint("misfire-seen")
                    .Hold(0.3f, ScriptedAction.GunnerLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Cannon"))
                .Assert(s => s
                    .WaitUntil("ShipHealthBelow", 60f, "1000")
                    .WaitUntil("CannonFree", 30f))
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
                    .Expect("HudShown")
                    .Checkpoint("arrived")
                    // The end screen is pinned: Esc opens the pause menu over it, Esc again comes back to it.
                    .Tap(ScriptedAction.Cancel)
                    .WaitUntil("TopMenu", 2f, "main")
                    .Tap(ScriptedAction.Cancel)
                    .WaitUntil("TopMenu", 2f, "voyage_arrived")
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

        /// <summary>
        /// A late joiner boards the ship: the host is aboard before the client connects (the client joins after a delay),
        /// and the client's player must start at the ship's boarding pose, not at the player prefab's spawn point. The
        /// client checks where it stands; the server checks the same in its own view. A solo run checks the host.
        /// Plan: crew-gate-and-boarding.md.
        /// </summary>
        public static readonly ScenarioEntry LateJoinBoarding = new(
            new Scenario.Builder("LateJoinBoarding")
                .InScene(TestScenes.Voyage)
                .Describe("Host aboard -> the client joins late -> its player starts on the ship at the boarding point, on both peers.")
                .Timeout(90f)
                .JoinLate(12f)
                .Arrange(s => s
                    .Expect("NoRemotePlayers")
                    .WaitUntil("SubjectReady", 60f))
                .Act(s => s
                    .WaitUntil("SubjectReady", 30f)
                    .WaitUntil("SubjectAboard", 10f, "4")
                    .Checkpoint("boarded"))
                .Assert(s => s
                    .WaitUntil("SubjectAboard", 10f, "4"))
                .Build(),
            builder => builder.Register<BoardingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Sky islands, a new layout every voyage: the server begins a voyage, and each peer builds the islands around the
        /// ship from the voyage's replicated seed (nothing else is sent): exactly the ones the layout wants, clear of the
        /// ship, solid to a ray. The server then begins another voyage, and both peers replace them with the new seed's.
        /// Plan: sky-islands.md.
        /// </summary>
        public static readonly ScenarioEntry IslandsPerVoyage = new(
            new Scenario.Builder("IslandsPerVoyage")
                .InScene(TestScenes.Voyage)
                .Describe("Voyage begins -> islands from its seed on both peers, clear of the ship and solid -> next voyage -> " +
                          "both rebuild from the new seed.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("VoyagePhase", 5f, "Briefing"))
                .Act(s => s
                    .WaitUntil("IslandsFromVoyage", 20f)
                    .Expect("IslandsMatchLayout")
                    .Expect("IslandsClearOfShip")
                    .Expect("IslandSolid")
                    .Do("LookAtNearestIsland")
                    .Wait(0.5f, "the view turns")
                    .Checkpoint("first-layout")
                    .Do("RecordIslandSeed")
                    .WaitUntil("IslandSeedChanged", 40f)
                    .WaitUntil("IslandsFromVoyage", 20f)
                    .Expect("IslandsMatchLayout")
                    .Expect("IslandsClearOfShip")
                    .Expect("IslandSolid")
                    .Do("LookAtNearestIsland")
                    .Wait(0.5f, "the view turns")
                    .Checkpoint("second-layout"))
                .Assert(s => s
                    .WaitUntil("IslandsFromVoyage", 20f)
                    .Expect("IslandsMatchLayout")
                    .Expect("IslandsClearOfShip")
                    .Expect("IslandSolid")
                    .Do("RecordIslandSeed")
                    .Wait(5f, "subject sees the first layout")
                    .Do("VoyageBegin")
                    .WaitUntil("IslandSeedChanged", 10f)
                    .WaitUntil("IslandsFromVoyage", 20f)
                    .Expect("IslandsMatchLayout")
                    .Expect("IslandsClearOfShip")
                    .Expect("IslandSolid"))
                .Build(),
            builder =>
            {
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<SkyIslandsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Island rock is solid to the ship: the server puts an island a few metres into the ship's bow, as if it had flown
        /// in. The server pushes the ship out and hurts it once; the subject sees the ship's health drop. Plan:
        /// sky-islands.md (S2).
        /// </summary>
        public static readonly ScenarioEntry IslandRam = new(
            new Scenario.Builder("IslandRam")
                .InScene(TestScenes.Voyage)
                .Describe("An island a few metres into the bow -> the server pushes the ship out and hurts it once -> " +
                          "the subject sees the hull damaged.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("ShipHealthFull")
                    .Do("PlaceIslandIntoBow"))
                .Act(s => s
                    .WaitUntil("ShipHurt", 10f)
                    .Checkpoint("rammed"))
                .Assert(s => s
                    .WaitUntil("ShipHitIsland", 5f)
                    .WaitUntil("ShipOutOfRock", 5f)
                    .Expect("ShipHurt")
                    .Wait(3f, "the ship rests against the rock")
                    .Expect("ShipOutOfRock"))
                .Build(),
            builder =>
            {
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<SkyIslandImpactScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Boon gates: a voyage lays gates off its route; the server then puts one around the ship, which takes its boon
        /// (GE_Boon_SpeedBurst, a Duration effect on the ship) exactly once. The subject sees the boon's cue active and the
        /// gate used. Plan: hazards-and-boons.md (B1).
        /// </summary>
        public static readonly ScenarioEntry BoonGate = new(
            new Scenario.Builder("BoonGate")
                .InScene(TestScenes.Voyage)
                .Describe("Voyage -> gates laid off the route -> a gate around the ship -> the speed boon on every peer, the gate used, taken once.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("BoonGatesLaid", 5f, "1")
                    .Do("SpawnBoonGateOnShip"))
                .Act(s => s
                    .WaitUntil("CueActive", 10f, "Cue.Boon.SpeedBurst")
                    .WaitUntil("BoonGatesUsedHere", 5f, "1")
                    .Checkpoint("boon-seen"))
                .Assert(s => s
                    .WaitUntil("BoonsTaken", 30f, "1")
                    .WaitUntil("CueActive", 5f, "Cue.Boon.SpeedBurst")
                    .Wait(1f, "the ship stays inside the used gate")
                    .Expect("BoonsTaken", "1"))
                .Build(),
            builder =>
            {
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<BoonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });
        /// <summary>
        /// The engine boost with real input (B3): the subject walks up to the engine telegraph and presses Interact, which
        /// opens its skill check on the subject's peer (walking and looking go quiet). It presses while the needle is in
        /// the Perfect part; the server takes that grade once and puts the Perfect boost on the ship, whose cue every peer
        /// sees. Plan: hazards-and-boons.md (B3).
        /// </summary>
        public static readonly ScenarioEntry EngineBoost = new(
            new Scenario.Builder("EngineBoost")
                .InScene(TestScenes.Voyage)
                .Describe("Walk to the engine telegraph -> Interact -> the gauge runs here -> press in the Perfect part -> the Perfect boost on every peer, graded once.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectNear", "EngineTelegraph"))
                .Act(s => s
                    .WaitUntil("SubjectNear", 45f, "EngineTelegraph")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "EngineTelegraph")
                    .WaitUntil("InteractTargetIs", 5f, "EngineTelegraph")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SkillCheckRunning", 10f)
                    .Checkpoint("gauge-open")
                    .WaitUntil("SkillCheckInPerfect", 5f)
                    .Tap(ScriptedAction.SkillCheckPress)
                    .WaitUntil("CueActive", 10f, "Cue.Boon.EnginePerfect"))
                .Assert(s => s
                    .WaitUntil("SkillChecksGraded", 30f, "Perfect:1")
                    .WaitUntil("CueActive", 5f, "Cue.Boon.EnginePerfect")
                    .Wait(1f, "no second grade")
                    .Expect("SkillChecksGraded", "Perfect:1"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<SkillCheckScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });
        /// <summary>
        /// The button combo with real input (B3, the second kind): the subject walks up to the boiler and presses
        /// Interact, which opens its combo on the subject's peer. One wrong step restarts the row (it does not fail it),
        /// then the four right steps finish it: a Good, since a step went wrong. The server takes that grade once and puts
        /// the Good boost on the ship, whose cue every peer sees. Plan: hazards-and-boons.md (B3).
        /// </summary>
        public static readonly ScenarioEntry BoilerCombo = new(
            new Scenario.Builder("BoilerCombo")
                .InScene(TestScenes.Voyage)
                .Describe("Walk to the boiler -> Interact -> the combo runs here -> a wrong step restarts it -> the four steps -> Good, the Good boost on every peer, graded once.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectNear", "Boiler"))
                .Act(s => s
                    .WaitUntil("SubjectNear", 45f, "Boiler")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "Boiler")
                    .WaitUntil("InteractTargetIs", 5f, "Boiler")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SkillCheckRunning", 10f)
                    .Do("TapComboNext").Wait(0.2f, "one step a frame")
                    .Do("TapComboWrong").Wait(0.2f, "the row restarts")
                    .Checkpoint("combo-open")
                    .Do("TapComboNext").Wait(0.2f, "step 1")
                    .Do("TapComboNext").Wait(0.2f, "step 2")
                    .Do("TapComboNext").Wait(0.2f, "step 3")
                    .Do("TapComboNext").Wait(0.2f, "step 4")
                    .WaitUntil("CueActive", 10f, "Cue.Boon.EngineGood"))
                .Assert(s => s
                    .WaitUntil("SkillChecksGraded", 30f, "Good:1")
                    .WaitUntil("CueActive", 5f, "Cue.Boon.EngineGood")
                    .Wait(1f, "no second grade")
                    .Expect("SkillChecksGraded", "Good:1")
                    .Expect("SkillChecksGraded", "Perfect:0"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<SkillCheckScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });
        /// <summary>
        /// A relay challenge (B4a): the server starts <c>CH_EngineRelay</c> (the engine telegraph, then the boiler, each by
        /// a different player than the step before); its start cue fires once on every peer. The host's own player does the telegraph's step without input, which
        /// lights the boiler on every peer. The subject (another player in host + client; alone, the same one) walks to it,
        /// presses Interact and plays the combo without a slip: the step and so the challenge are Perfect, and the
        /// challenge's Perfect effect goes on the ship, whose cue every peer sees. Plan: hazards-and-boons.md (B4a).
        /// </summary>
        public static readonly ScenarioEntry ChallengeRelay = new(
            new Scenario.Builder("ChallengeRelay")
                .InScene(TestScenes.Voyage)
                .Describe("Start the engine relay -> the host does the telegraph -> the boiler lights on every peer -> the subject plays it clean -> the challenge is Perfect, its effect on every peer.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("StartChallenge", "CH_EngineRelay")
                    .WaitUntil("CueCount", 5f, "Cue.Boon.ChallengeStart:Execute:1")
                    .WaitUntil("ChallengeLit", 5f, "Engine telegraph")
                    .Do("FinishChallengeStep", "SC_EngineBoost:Perfect")
                    .Do("PlaceSubjectNear", "Boiler"))
                .Act(s => s
                    .WaitUntil("CueCount", 45f, "Cue.Boon.ChallengeStart:Execute:1")
                    .WaitUntil("SubjectNear", 45f, "Boiler")
                    .WaitUntil("ChallengeLit", 10f, "Boiler")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "Boiler")
                    .WaitUntil("InteractTargetIs", 5f, "Boiler")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SkillCheckRunning", 10f)
                    .Checkpoint("relay-step")
                    .Do("TapComboNext").Wait(0.2f, "step 1")
                    .Do("TapComboNext").Wait(0.2f, "step 2")
                    .Do("TapComboNext").Wait(0.2f, "step 3")
                    .Do("TapComboNext").Wait(0.2f, "step 4")
                    .WaitUntil("CueActive", 10f, "Cue.Boon.ChallengePerfect"))
                .Assert(s => s
                    .WaitUntil("ChallengesEnded", 40f, "Perfect:1")
                    .WaitUntil("CueActive", 5f, "Cue.Boon.ChallengePerfect")
                    .Wait(1f, "no second grade")
                    .Expect("ChallengesEnded", "Perfect:1")
                    .Expect("SkillChecksGraded", "Perfect:1"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<SkillCheckScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });
        /// <summary>
        /// The helm with real input: the subject walks up to the wheel and presses Interact to take it. It stays in its body
        /// on the helm seat (State.Occupying.Helm turns the Helmsman context on, which silences walking but not looking).
        /// Throttle and turn keys travel in its predicted input; the server moves and turns the ship from them. Leave lets go
        /// and the ship's input drops to idle. Plan: helm-station.md.
        /// </summary>
        public static readonly ScenarioEntry HelmSteer = new(
            new Scenario.Builder("HelmSteer")
                .InScene(TestScenes.Helm)
                .Describe("Take the helm -> throttle and turn -> ship moves and turns on the server, body stays on the seat -> Leave -> ship input idle.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtHelm"))
                .Act(s => s
                    .WaitUntil("SubjectAtHelm", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceHelm")
                    .WaitUntil("InteractTargetIs", 5f, "HelmStation")
                    .WaitUntil("TargetOutlined", 2f, "HelmStation")
                    .Checkpoint("facing-helm")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Helm")
                    .WaitUntil("HelmManned", 5f)
                    .Checkpoint("manned")
                    .Wait(1f, "the server records the ship's pose")
                    .Hold(4f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnRight)
                    .Expect("SubjectOnHelmSeat")
                    .Checkpoint("steered")
                    .Hold(0.3f, ScriptedAction.HelmLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Helm"))
                .Assert(s => s
                    .WaitUntil("HelmManned", 60f)
                    .Do("RecordShipPose")
                    .WaitUntil("ShipMoved", 20f, "3")
                    .WaitUntil("ShipTurned", 10f, "3")
                    .WaitUntil("HelmFree", 30f)
                    .Wait(0.5f, "the ship takes its next tick")
                    .Expect("ShipInputIdle"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<HelmScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetOutlineScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        public static readonly ScenarioEntry DesignedShipFlies = new(
            new Scenario.Builder("DesignedShipFlies")
                .InScene(TestScenes.Shipyard)
                .Describe("The host builds the ship from the starter design, every peer builds the same -> stand on its deck -> take its helm (placed by the design) -> ship moves and turns -> Leave -> still on the ship.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtHelm"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 20f, "4")
                    .WaitUntil("ShipTopSpeed", 10f, "15.56")
                    .WaitUntil("SubjectOnShip", 45f)
                    .WaitUntil("SubjectAtHelm", 5f)
                    .Wait(0.5f, "teleport settles")
                    .Checkpoint("on-deck")
                    .Do("FaceHelm")
                    .WaitUntil("InteractTargetIs", 5f, "HelmStation")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Helm")
                    .WaitUntil("HelmManned", 5f)
                    .Wait(1f, "the server records the ship's pose")
                    .Hold(4f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnRight)
                    .Expect("SubjectOnHelmSeat")
                    .Checkpoint("steered")
                    .Hold(0.3f, ScriptedAction.HelmLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Helm")
                    .Wait(1f, "the body settles on the deck")
                    .Expect("SubjectOnShip"))
                .Assert(s => s
                    .WaitUntil("HelmManned", 60f)
                    .Do("RecordShipPose")
                    .WaitUntil("ShipMoved", 20f, "3")
                    .WaitUntil("ShipTurned", 10f, "3")
                    .WaitUntil("HelmFree", 30f)
                    .Expect("ShipTopSpeed", "15.56")
                    .Expect("ShipDesignBuilt", "5"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<HelmScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        public static readonly ScenarioEntry ShipyardRoundTrip = new(
            new Scenario.Builder("ShipyardRoundTrip")
                .InScene(TestScenes.Shipyard)
                .Describe("The host opens the shipyard on the starter, sets two mount plates down on its deck, saves, starts a new ship, loads the saved one back, launches it -> every peer rebuilds the ship in play from it.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Do("ShipyardOpen")
                    .Expect("ShipyardParts", "5")
                    .Do("ShipyardPlaceDown", "hull.mount 2 -2")
                    .Do("ShipyardPlaceDown", "hull.mount -2 -2")
                    .Expect("ShipyardParts", "7")
                    .Wait(0.5f, "the preview renders")
                    .Checkpoint("shipyard")
                    .Wait(0.5f, "the screenshot is taken at the end of a frame")
                    .Do("ShipyardSave", "Scenario Ship")
                    .Do("ShipyardNew")
                    .Expect("ShipyardParts", "3")
                    .Do("ShipyardLoad", "Scenario Ship")
                    .Expect("ShipyardParts", "7")
                    .Do("ShipyardLaunch")
                    .Do("ShipyardForget", "Scenario Ship"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "6")
                    .Checkpoint("rebuilt"))
                .Assert(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "7"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipyardScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The temporary ship picker (.docs/plans/ship-picker-temporary.md). (-1.5, 1.5) m in ship space is plank floor on both the
        // starter and the raft (both decks at -0.5 m). Once the subject has checked the raft it jumps; the server sees its feet
        // rise (replicated) and picks the starter back.
        public static readonly ScenarioEntry ShipPickerSwap = new(
            new Scenario.Builder("ShipPickerSwap")
                .InScene(TestScenes.Shipyard)
                .Describe("The subject stands on the starter's deck -> the host picks the built-in raft -> every peer rebuilds the ship as the raft and the subject is still aboard -> the host picks the starter back -> rebuilt again, still aboard.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtCell", "-1.5,0,1.5,0")
                    .WaitUntil("SubjectOnShip", 10f)
                    .Wait(1f, "teleport settles")
                    .Do("PickShip", "Raft")
                    .WaitUntil("ShipFlies", 30f, "Raft"))
                .Act(s => s
                    .WaitUntil("ShipFlies", 45f, "Raft")
                    .Wait(1f, "the rebuilt ship settles")
                    .Expect("SubjectOnShip")
                    .Checkpoint("raft")
                    .Hold(0.2f, ScriptedAction.Jump)
                    .WaitUntil("ShipFlies", 45f, "Starter")
                    .Wait(1f, "the rebuilt ship settles")
                    .Expect("SubjectOnShip")
                    .Checkpoint("starter"))
                .Assert(s => s
                    .Expect("SubjectOnShip")
                    .WaitUntil("SubjectFeetAbove", 45f, "-0.2")
                    .Wait(1.5f, "the subject lands on the raft")
                    .Expect("SubjectOnShip")
                    .Do("PickShip", "Starter")
                    .WaitUntil("ShipFlies", 30f, "Starter")
                    .Wait(1f, "the rebuilt ship settles")
                    .Expect("SubjectOnShip"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipPickerScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The look's atmosphere (.docs/plans/look-slice.md, L2), in the one test area that loads it. Presentation, so each
        // peer checks its own: the Atmosphere owns fog, ambient, sun and post. Cloud whiteout is AtmosphereProcessorTests'.
        public static readonly ScenarioEntry LookApplies = new(
            new Scenario.Builder("LookApplies")
                .InScene(TestScenes.Look)
                .Describe("Host and client spawn in the look's test area -> on each peer the Atmosphere owns the air: banded fog on, plain fog and ambient from the look, the sun at the look's colour and intensity, post effects on the local camera.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("LookApplied", 15f))
                .Act(s => s
                    .WaitUntil("LookApplied", 45f)
                    .Wait(1f, "a few frames of the look")
                    .Expect("LookApplied")
                    .Checkpoint("look"))
                .Assert(s => s
                    .Wait(2f, "the subject checks its own view")
                    .Expect("LookApplied"))
                .Build(),
            builder => builder.Register<LookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        public static readonly ScenarioEntry DayMoves = new(
            new Scenario.Builder("DayMoves")
                .InScene(TestScenes.Look)
                .Describe("Host and client spawn in the look's test area with the day running -> on each peer the sun shines from where the day says, at the frame's colour, under the painted sky -> a few seconds later it has turned.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("DayApplied", 15f))
                .Act(s => s
                    .WaitUntil("DayApplied", 45f)
                    .Do("MarkSun")
                    .Wait(3f, "the day moves on")
                    .WaitUntil("SunMoved", 10f)
                    .Expect("DayApplied")
                    .Checkpoint("day"))
                .Assert(s => s
                    .Do("MarkSun")
                    .Wait(3f, "the day moves on")
                    .Expect("SunMoved")
                    .Expect("DayApplied"))
                .Build(),
            builder => builder.Register<DayCycleScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        // The drawn cloud sea's holes (.docs/plans/cloud-sea.md, "The drawn sea and its holes"): the server owns them,
        // every peer works out the same hole from the replicated list and the shared clock, for the game and the shader.
        public static readonly ScenarioEntry CloudHoleOpens = new(
            new Scenario.Builder("CloudHoleOpens")
                .InScene(TestScenes.Look)
                .Describe("Host and client spawn in the look's test area -> the server opens a hole in the cloud sea beside the ship -> on each peer it is fully open, the cloud surface at its centre has dropped by the hole depth, and the sea shader has the holes.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Do("OpenScenarioHole")
                    .WaitUntil("ScenarioHoleOpen", 10f))
                .Act(s => s
                    .WaitUntil("ScenarioHoleOpen", 45f)
                    .Checkpoint("hole"))
                .Assert(s => s
                    .Wait(2f, "the subject checks its own peer")
                    .Expect("ScenarioHoleOpen"))
                .Build(),
            builder => builder.Register<CloudSeaScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        // The web of rifts (.docs/plans/cloud-sea-fog.md, F3): a pattern from the world seed and the shared clock, so
        // every peer cuts the same rifts into the game's surface and the drawn sea, with nothing replicated.
        public static readonly ScenarioEntry CloudWebReal = new(
            new Scenario.Builder("CloudWebReal")
                .InScene(TestScenes.Look)
                .Describe("Host and client spawn in the look's test area -> on each peer, near the ship, the cloud surface in a rift of the web has dropped by the hole depth, on a cloud island it is at the swell, and the sea shader has the web.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f))
                .Act(s => s
                    .WaitUntil("WebRiftIsReal", 20f)
                    .Checkpoint("web"))
                .Assert(s => s
                    .Wait(2f, "the subject checks its own peer")
                    .Expect("WebRiftIsReal"))
                .Build(),
            builder => builder.Register<CloudSeaScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        public static readonly ScenarioEntry SoarFromDive = new(
            new Scenario.Builder("SoarFromDive")
                .InScene(TestScenes.Look)
                .Describe("Host and client spawn in the look's test area -> the ship, nobody at the helm, settles at its rest height on the cloud -> the server points its nose 20 deg down into the cloud for 6 s, then 25 deg up -> the dive's speed launches it at least 30 m above the cloud on every peer.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipAtRest", 30f)
                    .Do("PitchShip", "-20")
                    .Wait(6f, "the dive through the cloud")
                    .Do("PitchShip", "25")
                    .WaitUntil("ShipAboveCloud", 25f, "30"))
                .Act(s => s
                    .WaitUntil("ShipAboveCloud", 90f, "30")
                    .Checkpoint("launch"))
                .Assert(s => s
                    .WaitUntil("ShipAboveCloud", 90f, "30"))
                .Build(),
            builder => builder.Register<CloudFlightScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Island life (plan island-life.md): the islands round the test ship stand with their scenery, trees and caves.
        /// The server stands the subject by a tree on an island; the subject gathers a log from it with the real key (the
        /// canopy loses a lobe on every peer); then the server stands it in a cave, under the rock, where it ties the log
        /// into a bundle and sets a campfire down on the chamber's floor.
        /// </summary>
        public static readonly ScenarioEntry IslandForage = new(
            new Scenario.Builder("IslandForage")
                .InScene(TestScenes.Islands)
                .Describe("Islands stand with trees, rocks and caves -> stand by a tree on an island -> gather firewood from it " +
                          "(a log less on every peer) -> stand in a cave, under its roof -> set a campfire down inside.")
                .Timeout(150f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .WaitUntil("IslandTreesStand", 40f)
                    .Expect("SubjectHolds", "none")
                    .Do("PlaceSubjectAtIslandTree"))
                .Act(s => s
                    .WaitUntil("SubjectNearIslandTree", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Expect("IslandSceneryStands")
                    .Do("FaceIslandTree")
                    .WaitUntil("InteractTargetIs", 5f, IslandTreeTarget)
                    .WaitUntil("IslandTreeLogs", 5f, "3")
                    .Checkpoint("at-a-tree")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_Firewood")
                    .WaitUntil("IslandTreeLogs", 5f, "2")
                    .Checkpoint("gathered")
                    .WaitUntil("SubjectInCave", 45f)
                    .Wait(1f, "teleport settles")
                    .Checkpoint("in-a-cave")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_CampfireBundle")
                    .Do("AimAtGround", "2")
                    .WaitUntil("PlacementPreview", 5f, "Campfire,Fits")
                    .Wait(0.5f, "let go")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("CampfirePlaced", 5f)
                    .Checkpoint("campfire-in-a-cave"))
                .Assert(s => s
                    .WaitUntil("SubjectHolds", 45f, "ITEM_Firewood")
                    .Expect("IslandTreeLogs", "2")
                    .Do("PlaceSubjectInCave")
                    .WaitUntil("SubjectInCave", 20f)
                    .WaitUntil("CampfirePlaced", 30f)
                    .Expect("SubjectInCave"))
                .Build(),
            builder =>
            {
                builder.Register<IslandLifeScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<CampingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>The name of a tree's firewood part (IslandDetailsAssetBuilder.TreeTargetName, an Editor type).</summary>
        private const string IslandTreeTarget = "Branches";

        /// <summary>
        /// The camp loop with the real keys, on an island (plan island-life.md): in the middle clearing of the island
        /// nearest the test ship, the subject ties the log it holds into a campfire bundle (Primary), aims at the ground
        /// and sets the bundle down where the preview says it fits (Primary); the campfire stands unlit on every peer and
        /// a log fed to it lights it. Then it sets a tent roll down beside it, pitched, and packs it away again. The server
        /// hands over each item once the replicated outcome of the last step arrives.
        /// </summary>
        public static readonly ScenarioEntry CampLoop = new(
            new Scenario.Builder("CampLoop")
                .InScene(TestScenes.Islands)
                .Describe("In an island clearing: tie a log into a campfire bundle -> set it down where the preview fits -> " +
                          "light it with a log -> set a tent down beside it -> pack the tent away again.")
                .Timeout(150f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Expect("SubjectHolds", "none")
                    .Do("PlaceSubjectInAClearing")
                    .Do("EquipSubject", "ITEM_Firewood"))
                .Act(s => s
                    .WaitUntil("SubjectInAClearing", 45f)
                    .Wait(0.5f, "teleport settles")
                    .WaitUntil("SubjectHolds", 10f, "ITEM_Firewood")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_CampfireBundle")
                    .Do("AimAtGround", "2.5")
                    .WaitUntil("PlacementPreview", 5f, "Campfire,Fits")
                    .Checkpoint("campfire-preview")
                    .Wait(0.5f, "let go")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("CampfirePlaced", 5f)
                    .Expect("FireOut")
                    .WaitUntil("SubjectHolds", 20f, "ITEM_Firewood")
                    .Do("FaceNearest", CampfireTarget)
                    .WaitUntil("InteractTargetIs", 5f, CampfireTarget)
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("FireLit", 5f)
                    .Checkpoint("fire-lit")
                    .WaitUntil("SubjectHolds", 20f, "ITEM_TentRoll")
                    .Do("AimAtGround", "3,120")
                    .WaitUntil("PlacementPreview", 5f, "Tent,Fits")
                    .Checkpoint("tent-preview")
                    .Wait(0.5f, "let go")
                    .Hold(0.3f, ScriptedAction.Primary)
                    .WaitUntil("TentPitched", 5f)
                    .WaitUntil("SubjectHolds", 5f, "none")
                    .Checkpoint("tent-pitched")
                    .Do("FaceNearest", TentTarget)
                    .WaitUntil("InteractTargetIs", 5f, TentTarget)
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_TentRoll")
                    .WaitUntil("TentGone", 5f))
                .Assert(s => s
                    .WaitUntil("SubjectHolds", 45f, "ITEM_CampfireBundle")
                    .WaitUntil("CampfirePlaced", 30f)
                    .WaitUntil("SubjectHolds", 10f, "none")
                    .Expect("FireOut")
                    .Do("EquipSubject", "ITEM_Firewood")
                    .WaitUntil("FireLit", 30f)
                    .WaitUntil("SubjectHolds", 10f, "none")
                    .Do("EquipSubject", "ITEM_TentRoll")
                    .WaitUntil("TentPitched", 30f)
                    .WaitUntil("TentGone", 30f)
                    .Expect("SubjectHolds", "ITEM_TentRoll")
                    .Expect("FireLit"))
                .Build(),
            builder =>
            {
                builder.Register<CampingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>The names of the placed campfire's and tent's targets (CampingAssetBuilder, an Editor type).</summary>
        private const string CampfireTarget = "Campfire";
        private const string TentTarget = "TentSpot";

        public static readonly ScenarioEntry MountFitting = new(            new Scenario.Builder("MountFitting")
                .InScene(TestScenes.Shipyard)
                .Describe("Face a free socket on the designed ship -> press Interact -> the fitting menu opens on the subject's peer only -> pick the cannon -> the server mounts it on that socket for every peer.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtSocket"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 20f, "4")
                    .WaitUntil("SubjectOnShip", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "ShipSocketTarget")
                    .WaitUntil("InteractTargetIs", 5f, "ShipSocketTarget")
                    .Checkpoint("facing-socket")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("FittingMenuOpen", 5f)
                    .Checkpoint("menu")
                    .Do("ChooseFitting", "weapon.cannon")
                    .WaitUntil("SocketFitted", 10f, "weapon.cannon")
                    .Wait(0.5f, "the screenshot is taken at the end of a frame")
                    .Checkpoint("mounted"))
                .Assert(s => s
                    .WaitUntil("SocketFitted", 60f, "weapon.cannon"))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The galleon has 15 parts; one of them (the helm) is spawned rather than built. Its starboard flight rises aft from the
        // main deck at (3, 3.5) m to the quarterdeck's front at z -1.5 m, whose walking surface is 2.5 m in ship space.
        public static readonly ScenarioEntry ShipStairs = new(
            new Scenario.Builder("ShipStairs")
                .InScene(TestScenes.Shipyard)
                .Describe("The host gives the ship the built-in galleon -> every peer builds it -> stand at the foot of the starboard stairs on the main deck, facing aft -> walk forward -> up the stairs onto the quarterdeck, 3 m higher, still on the ship.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("FlyDesign", "Galleon")
                    .WaitUntil("ShipDesignBuilt", 30f, "14")
                    .Wait(1f, "the rebuilt ship settles")
                    .Do("PlaceSubjectAtCell", "3,0,4.5,180"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "13")
                    .WaitUntil("SubjectOnShip", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceShipYaw", "180")
                    .Checkpoint("foot-of-stairs")
                    .Hold(4f, ScriptedAction.MoveForward)
                    .WaitUntil("SubjectFeetAbove", 5f, "2.3")
                    .Expect("SubjectOnShip")
                    .Checkpoint("quarterdeck"))
                .Assert(s => s
                    .WaitUntil("SubjectFeetAbove", 60f, "2.3"))
                .Build(),
            builder => builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        // The galleon's built-in loadout (Settings/ShipSockets/Galleon.fittings.json): four cannons, the tool rack and more. The
        // tool rack comes from ShipDamage, which Test_Shipyard does not load, so here it is skipped and the cannons mount.
        public static readonly ScenarioEntry GalleonLoadout = new(
            new Scenario.Builder("GalleonLoadout")
                .InScene(TestScenes.Shipyard)
                .Describe("The host gives the ship the built-in galleon -> every peer builds it -> the server mounts the galleon's loadout from its fittings file -> its four cannons stand on their sockets on every peer (the tool rack's feature is not loaded here, so it is skipped).")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("FlyDesign", "Galleon"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "13")
                    .WaitUntil("FittingsMounted", 15f, "4")
                    .Expect("SocketFitted", "weapon.cannon")
                    .Wait(0.5f, "the screenshot is taken at the end of a frame")
                    .Checkpoint("fitted"))
                .Assert(s => s
                    .WaitUntil("FittingsMounted", 60f, "4"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The galleon (Settings/ShipDesigns/Galleon.ship.json, .docs/plans/frame-only-ships.md): a hold (floor -3.5 m) under the
        // main deck (-0.5 m), leaning planked walls between them, a flight of frame stairs from the hatch's aft edge (z 0.5 m)
        // down to the hold floor (z 5.5 m). 15 parts, one (the helm) spawned rather than built. The subject starts in the hold
        // forward of the stairs (z 7 m), facing aft, and walks up through the hatch.
        public static readonly ScenarioEntry FrameStories = new(
            new Scenario.Builder("FrameStories")
                .InScene(TestScenes.Shipyard)
                .Describe("The host gives the ship the built-in galleon -> every peer builds its stories, walls, railings and stairs -> stand on the hold floor at the foot of the hatch stairs, facing aft -> walk forward -> up through the hatch onto the main deck, 3 m higher, still on the planks of the ship.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("FlyDesign", "Galleon")
                    .WaitUntil("ShipDesignBuilt", 30f, "14")
                    .WaitUntil("ShipFrameBuilt", 10f, "4")
                    .Wait(1f, "the rebuilt ship settles")
                    .Do("PlaceSubjectAtCell", "0,-3,7,180,1"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "13")
                    .WaitUntil("ShipFrameBuilt", 10f, "4")
                    .WaitUntil("SubjectOnShip", 45f)
                    .Wait(0.5f, "teleport settles")
                    .WaitUntil("SubjectStandsOn", 5f, "Planks0")
                    .Do("FaceShipYaw", "180")
                    .Checkpoint("in-the-hold")
                    .Hold(4f, ScriptedAction.MoveForward)
                    .WaitUntil("SubjectFeetAbove", 5f, "-0.7")
                    .Wait(0.5f, "the body settles")
                    .Expect("SubjectStandsOn", "Planks1")
                    .Expect("SubjectOnShip")
                    .Checkpoint("main-deck"))
                .Assert(s => s
                    .WaitUntil("SubjectFeetAbove", 60f, "-0.7"))
                .Build(),
            builder => builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        // The shipyard's frame tools (.docs/plans/ship-frames.md, S2): a planked deck drawn out from the starter's starboard side:
        // its stern corner (node 2 at (2.5, -4.5) m), two new corners 4 m out, back to its waist corner (node 3 at (3, 2) m),
        // closing a loop with the side between them. Saved, loaded, launched.
        public static readonly ScenarioEntry FrameRoundTrip = new(
            new Scenario.Builder("FrameRoundTrip")
                .InScene(TestScenes.Shipyard)
                .Describe("The host opens the shipyard on the starter, switches to frame mode, clicks out from the deck's starboard side and back onto it -> a planked face beside the deck -> saves, starts a new ship, loads it back with its frame -> launches it -> every peer builds the frame deck.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Do("ShipyardOpen")
                    .Do("ShipyardMode", "Frame")
                    .Do("ShipyardFrameClick", "2.5 -4.5")
                    .Do("ShipyardFrameClick", "6.5 -4.5")
                    .Do("ShipyardFrameClick", "6.5 2")
                    .Do("ShipyardFrameClick", "3 2")
                    .Expect("ShipyardFrame", "9 10 2")
                    .Wait(0.5f, "the preview renders")
                    .Checkpoint("drawn")
                    .Wait(0.5f, "the screenshot is taken at the end of a frame")
                    .Do("ShipyardSave", "Frame Scenario Ship")
                    .Do("ShipyardNew")
                    .Expect("ShipyardFrame", "4 4 1")
                    .Do("ShipyardLoad", "Frame Scenario Ship")
                    .Expect("ShipyardFrame", "9 10 2")
                    .Do("ShipyardLaunch")
                    .Do("ShipyardForget", "Frame Scenario Ship"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "4")
                    .WaitUntil("ShipFrameBuilt", 30f, "2")
                    .Checkpoint("rebuilt"))
                .Assert(s => s
                    .WaitUntil("ShipFrameBuilt", 30f, "2"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipyardScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // Parts on the frame (.docs/plans/ship-frames.md, S4; .docs/plans/frame-only-ships.md): FrameRoundTrip's deck beside
        // the starter's, then a mount plate set down on it at (5, -1.5) m, hung on that face at frame units (50, -15). Saved,
        // loaded, launched; every peer builds it there.
        public static readonly ScenarioEntry FramePartPlace = new(
            new Scenario.Builder("FramePartPlace")
                .InScene(TestScenes.Shipyard)
                .Describe("The host draws a planked square beside the starter's deck in frame mode -> in parts mode, sets a mount plate down on it -> saves, starts a new ship, loads it back -> launches it -> every peer builds the plate where it was set, on the frame deck.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Do("ShipyardOpen")
                    .Do("ShipyardMode", "Frame")
                    .Do("ShipyardFrameClick", "2.5 -4.5")
                    .Do("ShipyardFrameClick", "6.5 -4.5")
                    .Do("ShipyardFrameClick", "6.5 2")
                    .Do("ShipyardFrameClick", "3 2")
                    .Expect("ShipyardFrame", "9 10 2")
                    .Do("ShipyardMode", "Parts")
                    .Do("ShipyardPlaceDown", "hull.mount 5 -1.5")
                    .Expect("ShipyardParts", "6")
                    .Wait(0.5f, "the preview renders")
                    .Checkpoint("placed")
                    .Wait(0.5f, "the screenshot is taken at the end of a frame")
                    .Do("ShipyardSave", "Frame Part Scenario Ship")
                    .Do("ShipyardNew")
                    .Do("ShipyardLoad", "Frame Part Scenario Ship")
                    .Expect("ShipyardParts", "6")
                    .Do("ShipyardLaunch")
                    .Do("ShipyardForget", "Frame Part Scenario Ship"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "5")
                    .WaitUntil("ShipFrameBuilt", 30f, "2")
                    .WaitUntil("ShipPartAt", 30f, "hull.mount 50 -15")
                    .Checkpoint("rebuilt"))
                .Assert(s => s
                    .WaitUntil("ShipPartAt", 30f, "hull.mount 50 -15"))
                .Build(),
            builder =>
            {
                builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipyardScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The raft (Settings/ShipDesigns/Raft.ship.json, .docs/plans/ship-frames.md): a deck generated from a frame, 3 parts
        // (one, the helm, spawned rather than built) and one story's planks and beams. The subject starts on the starboard
        // waist at (3, 0, 0) m and walks aft across the seam onto the stern planks.
        public static readonly ScenarioEntry FrameDeck = new(
            new Scenario.Builder("FrameDeck")
                .InScene(TestScenes.Shipyard)
                .Describe("The host gives the ship the built-in raft -> every peer builds its parts and generates its frame deck -> stand on the generated planks, away from every part -> walk aft across the seam between two plank faces -> still standing on planks, on the ship.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .WaitUntil("ShipDesignBuilt", 20f, "5")
                    .Wait(1.5f, "settle after spawn")
                    .Do("FlyDesign", "Raft")
                    .WaitUntil("ShipDesignBuilt", 30f, "3")
                    .WaitUntil("ShipFrameBuilt", 10f, "2")
                    .Wait(1f, "the rebuilt ship settles")
                    .Do("PlaceSubjectAtCell", "3,0,0,180"))
                .Act(s => s
                    .WaitUntil("ShipDesignBuilt", 30f, "3")
                    .WaitUntil("ShipFrameBuilt", 10f, "2")
                    .WaitUntil("SubjectOnShip", 45f)
                    .Wait(0.5f, "teleport settles")
                    .WaitUntil("SubjectStandsOn", 5f, "Planks")
                    .Do("FaceShipYaw", "180")
                    .Checkpoint("on-planks")
                    .Hold(0.8f, ScriptedAction.MoveForward)
                    .Wait(1f, "the body settles")
                    .Expect("SubjectStandsOn", "Planks")
                    .Expect("SubjectFeetAbove", "-0.6")
                    .Expect("SubjectOnShip")
                    .Checkpoint("walked-aft"))
                .Assert(s => s
                    .WaitUntil("ShipFrameBuilt", 60f, "2")
                    .Expect("ShipDesignBuilt", "3"))
                .Build(),
            builder => builder.Register<ShipDesignsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        // Off the starboard rail (the test ship's deck spans x -4.2..6.3, top y -3.49) and 5 m above the deck, aiming at
        // the deck's middle: the line clears the 1 m rail even after a short fall.
        public static readonly ScenarioEntry HookAboard = new(
            new Scenario.Builder("HookAboard")
                .InScene(TestScenes.GrappleHook)
                .Describe("Hold the grappling hook, fall beside the ship -> fire at the deck and reel -> back aboard, line seen on both peers, then let go.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("EquipSubject", "ITEM_GrappleHook")
                    .Do("PlaceSubjectAtShipLocal", "15,1.51,0"))
                .Act(s => s
                    .WaitUntil("SubjectHolds", 5f, "ITEM_GrappleHook")
                    .WaitUntil("SubjectHasTag", 3f, "State.Carrying.GrappleHook")
                    .Expect("SubjectVisualShown", "Carry_GrappleHook")
                    .WaitUntil("SubjectNearShipLocal", 10f, "15,1.51,0,4")
                    .Do("AimAtShipLocal", "1.05,-3.49,0")
                    .WaitUntil("HookAim", 2f, "InRange")
                    .Expect("SubjectPose")
                    .Hold(3f, ScriptedAction.Primary)
                    .WaitUntil("SubjectOnShip", 5f)
                    .WaitUntil("SubjectUnhooked", 3f)
                    .Checkpoint("aboard"))
                .Assert(s => s
                    .WaitUntil("SubjectHooked", 30f)
                    .WaitUntil("HookLineDrawn", 3f)
                    .Expect("SubjectPose")
                    .Wait(0.3f, "reeling")
                    .Expect("SubjectPose")
                    .Wait(0.3f, "reeling")
                    .Expect("SubjectPose")
                    .Wait(0.6f, "reeling")
                    .Expect("SubjectPose")
                    .WaitUntil("SubjectOnShip", 10f)
                    .WaitUntil("SubjectUnhooked", 5f)
                    .Expect("SubjectHolds", "ITEM_GrappleHook"))
                .Build(),
            builder =>
            {
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // Below the starboard rail and 30 m under the deck, where a fall into the clouds would end. Scripted runs switch the
        // cloud reset off, so the server sends the subject overboard through the same handler the cloud boundary calls.
        public static readonly ScenarioEntry OverboardRespawn = new(
            new Scenario.Builder("OverboardRespawn")
                .InScene(TestScenes.Respawn)
                .Describe("Hold the hook, fall overboard -> the choice opens on the subject's own screen -> respawn aboard, empty-handed (the rules), on both peers.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("EquipSubject", "ITEM_GrappleHook")
                    .Do("PlaceSubjectAtShipLocal", "15,-30,0")
                    .Do("SendSubjectOverboard"))
                .Act(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Overboard")
                    .WaitUntil("OverboardMenuOpen", 3f)
                    .WaitUntil("RespawnHudShows", 3f, "Overboard")
                    .Expect("HudShown")
                    .Expect("CursorFree")
                    .Checkpoint("overboard")
                    // Esc over the pinned choice opens the pause menu on top (the next tick must not cover it), and
                    // Esc again returns to the choice (.docs/plans/ui-and-menus.md, U2).
                    .Tap(ScriptedAction.Cancel)
                    .WaitUntil("TopMenu", 2f, "main")
                    .Wait(0.5f, "a few overboard ticks")
                    .Expect("TopMenu", "main")
                    // Keyboard and gamepad through the Menu context (U5): the focus starts on the first row and moves.
                    .Expect("FocusedRow", "host")
                    // A direction is a value, not a press: hold it briefly (shorter than the repeat delay) for one step.
                    .Hold(0.15f, ScriptedAction.MenuDown)
                    .WaitUntil("FocusedRow", 2f, "join")
                    .Tap(ScriptedAction.Cancel)
                    .WaitUntil("TopMenu", 2f, "Overboard")
                    .Checkpoint("paused and back")
                    // The choice's first row is Respawn: Submit uses it, as Enter or gamepad South would.
                    .Expect("FocusedRow", TinCan.Features.Respawn.OverboardMenus.RespawnItem)
                    .Tap(ScriptedAction.MenuSubmit)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Overboard")
                    .WaitUntil("SubjectAboard", 5f)
                    .Expect("SubjectHolds", "none")
                    .Expect("TopMenu", "none")
                    .WaitUntil("CursorLocked", 2f)
                    .Checkpoint("aboard"))
                .Assert(s => s
                    .WaitUntil("SubjectOverboard", 30f)
                    .WaitUntil("SubjectNotOverboard", 30f)
                    .WaitUntil("SubjectAboard", 5f)
                    .Expect("SubjectHolds", "none")
                    .Expect("SubjectLacksTag", "State.Overboard"))
                .Build(),
            builder =>
            {
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<RespawnScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // Slice 2: overboard, take the leash, climb it back aboard (the leash point is the winch fixture on the starboard
        // side; its rope starts 12 m long). Keeps what you carry. The host draws the client's rope from the leashed tag.
        public static readonly ScenarioEntry LeashClimb = new(
            new Scenario.Builder("LeashClimb")
                .InScene(TestScenes.Respawn)
                .Describe("Fall overboard holding the hook -> hang on the leash -> climb it -> aboard on deck, still holding the hook, on both peers.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("EquipSubject", "ITEM_GrappleHook")
                    .Do("PlaceSubjectAtShipLocal", "15,-30,0")
                    .Do("SendSubjectOverboard"))
                .Act(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Overboard")
                    .WaitUntil("OverboardMenuOpen", 3f)
                    .Do("ChooseLeash")
                    .WaitUntil("SubjectHasTag", 5f, "State.Leashed")
                    .WaitUntil("RespawnHudShows", 3f, "On the leash: hold forward to climb, Interact to respawn")
                    .WaitUntil("LeashRopeDrawn", 3f)
                    .Checkpoint("leashed")
                    .Hold(9f, ScriptedAction.MoveForward)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Leashed")
                    .WaitUntil("SubjectAboard", 5f)
                    .Expect("SubjectHolds", "ITEM_GrappleHook")
                    .Checkpoint("aboard"))
                .Assert(s => s
                    .WaitUntil("SubjectLeashed", 30f)
                    .WaitUntil("LeashRopeDrawn", 3f)
                    .WaitUntil("SubjectNotOverboard", 30f)
                    .WaitUntil("SubjectAboard", 5f)
                    .Expect("SubjectHolds", "ITEM_GrappleHook"))
                .Build(),
            builder =>
            {
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<RespawnScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // Slice 3: the subject hangs on the leash without climbing; the server cranks the winch (as Interact on it does)
        // and the rope reels them aboard.
        public static readonly ScenarioEntry WinchRescue = new(
            new Scenario.Builder("WinchRescue")
                .InScene(TestScenes.Respawn)
                .Describe("Fall overboard -> hang on the leash, no climbing -> the winch is cranked -> reeled aboard, on both peers.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("PlaceSubjectAtShipLocal", "15,-30,0")
                    .Do("SendSubjectOverboard"))
                .Act(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Overboard")
                    .WaitUntil("OverboardMenuOpen", 3f)
                    .Do("ChooseLeash")
                    .WaitUntil("SubjectHasTag", 5f, "State.Leashed")
                    .Checkpoint("leashed")
                    .WaitUntil("SubjectLacksTag", 20f, "State.Leashed")
                    .WaitUntil("SubjectAboard", 5f)
                    .Checkpoint("winched-aboard"))
                .Assert(s => s
                    .WaitUntil("SubjectLeashed", 30f)
                    .Wait(1f, "let the leash catch them")
                    .Do("CrankWinch")
                    .WaitUntil("SubjectNotOverboard", 20f)
                    .WaitUntil("SubjectAboard", 5f))
                .Build(),
            builder =>
            {
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<RespawnScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        // The leash on the Galleon (Test_Galleon: the Galleon with the game's profile). The winch is
        // mounted on the starboard balcony off the quarterdeck (ship space (5,2,-6), its rope hanging past x 5.9), so the
        // subject falls beside and below it, climbs the rope in open air and lands on the quarterdeck, not inside a wall.
        public static readonly ScenarioEntry GalleonLeashClimb = new(
            new Scenario.Builder("GalleonLeashClimb")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: fall overboard by the starboard balcony -> hang on its winch's leash -> climb -> aboard on the quarterdeck, on both peers.")
                .Timeout(150f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .Do("PlaceSubjectAtShipLocal", "9,-8,-6")
                    .Do("SendSubjectOverboard"))
                .Act(s => s
                    .WaitUntil("SubjectHasTag", 10f, "State.Overboard")
                    .WaitUntil("OverboardMenuOpen", 3f)
                    .Do("ChooseLeash")
                    .WaitUntil("SubjectHasTag", 5f, "State.Leashed")
                    .WaitUntil("LeashRopeDrawn", 3f)
                    .Checkpoint("leashed")
                    .Hold(9f, ScriptedAction.MoveForward)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Leashed")
                    .WaitUntil("SubjectAboard", 5f)
                    .Checkpoint("aboard"))
                .Assert(s => s
                    .WaitUntil("SubjectLeashed", 30f)
                    .WaitUntil("SubjectNotOverboard", 30f)
                    // Checked at once: the subject still holds forward, so it walks off the landing spot right after.
                    .Expect("SubjectNearShipLocal", "3.5,3.5,-6,1.5")
                    .WaitUntil("SubjectAboard", 5f))
                .Build(),
            builder =>
            {
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<RespawnScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The compass at the helm follows the ship's route: a voyage lays a course to its destination, the server plots a
        /// route through a point off to starboard, the subject takes the helm and sees the compass point there, then the
        /// server puts that waypoint on the ship and the compass moves on to the destination. Plan: navigation-compass.md (S1).
        /// </summary>
        public static readonly ScenarioEntry CompassCourse = new(
            new Scenario.Builder("CompassCourse")
                .InScene(TestScenes.Navigation)
        /// <summary>
        /// The crew body (plan: crew-animation.md, S2) on the Galleon, where the game's profile loads it. The subject
        /// stands on the main deck facing the ship's centre, runs forward, stops, jumps. Its own peer checks the body is
        /// hidden from its first-person camera and stands, flies and lands again; the server sees it run, fly and stand.
        /// </summary>
        public static readonly ScenarioEntry CrewBodyMoves = new(
            new Scenario.Builder("CrewBodyMoves")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: the crew body stands -> runs while forward is held -> stands -> jumps -> lands, on both peers.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .Do("PlaceSubjectAtShipLocal", "0,0.55,6"))
                .Act(s => s
                    .WaitUntil("SubjectNearShipLocal", 45f, "0,0.55,6,1")
                    .Wait(1f, "teleport settles")
                    .Expect("SubjectBodyHiddenFromOwner")
                    .WaitUntil("SubjectBodyGait", 5f, "Idle")
                    .Checkpoint("standing")
                    .Hold(1f, ScriptedAction.MoveForward)
                    .WaitUntil("SubjectBodyGait", 3f, "Idle")
                    .Tap(ScriptedAction.Jump, 0.05f)
                    .WaitUntil("SubjectBodyGait", 1f, "Airborne")
                    .WaitUntil("SubjectBodyGait", 3f, "Idle")
                    .Checkpoint("landed"))
                .Assert(s => s
                    .WaitUntil("SubjectBodyGait", 60f, "Run")
                    .WaitUntil("SubjectBodyGait", 10f, "Airborne")
                    .WaitUntil("SubjectBodyGait", 5f, "Idle"))
                .Build(),
            builder =>
            {
                builder.Register<CrewBodyScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<RespawnScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

                .Describe("Voyage -> course to the destination on both peers, compass at the helm -> a route via starboard -> " +
                          "take the helm, compass points starboard -> waypoint reached -> compass points at the destination.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("RouteEndsAtDestination", 5f)
                    .Do("PlotSideRoute")
                    .Do("PlaceSubjectAtHelm"))
                .Act(s => s
                    .WaitUntil("SubjectAtHelm", 45f)
                    .WaitUntil("RouteWaypoints", 10f, "2")
                    .WaitUntil("ActiveWaypoint", 5f, "0")
                    .WaitUntil("CompassAtHelm", 5f)
                    .WaitUntil("CompassPointsAtActive", 5f)
                    .Expect("CompassArrowSide", "starboard")
                    .Expect("RouteEndsAtDestination")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceHelm")
                    .WaitUntil("InteractTargetIs", 5f, "HelmStation")
                    .Checkpoint("compass-starboard")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Helm")
                    .WaitUntil("ActiveWaypoint", 30f, "1")
                    .WaitUntil("CompassPointsAtActive", 5f)
                    .Checkpoint("compass-destination")
                    .Hold(0.3f, ScriptedAction.HelmLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Helm"))
                .Assert(s => s
                    .WaitUntil("HelmManned", 60f)
                    .Do("ReachFirstWaypoint")
                    .WaitUntil("ActiveWaypoint", 5f, "1")
                    .WaitUntil("HelmFree", 30f))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<HelmScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<NavigationScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The navigator plots with real input: the subject takes the chart table (its view switches to the table's
        /// camera), sweeps the cursor to starboard and clicks, which bends the course to the destination through a new
        /// waypoint; holding Raise lifts it; right-click removes it; Leave. The server checks each step in the replicated
        /// route, which only the player at the table may change. Plan: navigation-compass.md (S2).
        /// </summary>
        public static readonly ScenarioEntry ChartPlot = new(
            new Scenario.Builder("ChartPlot")
                .InScene(TestScenes.Navigation)
                .Describe("Take the chart table -> click to starboard: a waypoint before the destination on both peers -> " +
                          "raise it -> remove it -> leave.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("RouteEndsAtDestination", 5f)
                    .Do("PlaceSubjectAtChartTable"))
                .Act(s => s
                    .WaitUntil("SubjectAtChartTable", 45f)
                    .WaitUntil("RouteEndsAtDestination", 10f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceChartTable")
                    .WaitUntil("InteractTargetIs", 5f, "ChartTable")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Chart")
                    .WaitUntil("ChartTableManned", 5f)
                    .WaitUntil("ChartShowsWaypoints", 5f, "1")
                    .Hold(0.25f, ScriptedAction.NavigatorCursorRight)
                    .Tap(ScriptedAction.NavigatorPlace)
                    .WaitUntil("RouteWaypoints", 5f, "2")
                    .Expect("RouteEndsAtDestination")
                    .WaitUntil("ChartShowsWaypoints", 5f, "2")
                    .Checkpoint("plotted")
                    .Hold(1f, ScriptedAction.NavigatorRaise)
                    .WaitUntil("FirstWaypointAboveShip", 5f, "30")
                    .Checkpoint("raised")
                    .Tap(ScriptedAction.NavigatorRemove)
                    .WaitUntil("RouteWaypoints", 5f, "1")
                    .Hold(0.3f, ScriptedAction.NavigatorLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Chart"))
                .Assert(s => s
                    .WaitUntil("ChartTableManned", 60f)
                    .WaitUntil("RouteWaypoints", 30f, "2")
                    .Expect("FirstWaypointToStarboard")
                    .WaitUntil("FirstWaypointAboveShip", 20f, "30")
                    .WaitUntil("RouteWaypoints", 20f, "1")
                    .Expect("RouteEndsAtDestination")
                    .WaitUntil("ChartTableFree", 30f))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<NavigationScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Sky islands on the chart: the subject sits at the chart table and sees the seeded islands as rock, and the
        /// voyage's destination. The server marks the nearest hidden landmark discovered (as a flyby would); the
        /// discovery replicates and the landmark appears on the subject's chart. Plan: navigation-compass.md (S3).
        /// </summary>
        public static readonly ScenarioEntry ChartIslands = new(
            new Scenario.Builder("ChartIslands")
                .InScene(TestScenes.Navigation)
                .Describe("Take the chart table -> islands as rock and the destination on the chart -> a landmark is " +
                          "discovered on the server -> it appears on the subject's chart -> leave.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("RouteEndsAtDestination", 5f)
                    .Do("PlaceSubjectAtChartTable"))
                .Act(s => s
                    .WaitUntil("SubjectAtChartTable", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceChartTable")
                    .WaitUntil("InteractTargetIs", 5f, "ChartTable")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Chart")
                    .WaitUntil("ChartShowsRocks", 10f, "1")
                    .WaitUntil("ChartShowsPoints", 5f, "1")
                    .WaitUntil("DiscoveredPoints", 30f, "1")
                    .WaitUntil("ChartShowsPoints", 5f, "2")
                    .Checkpoint("islands-charted")
                    .Hold(0.3f, ScriptedAction.NavigatorLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Chart"))
                .Assert(s => s
                    .WaitUntil("ChartTableManned", 60f)
                    .Do("DiscoverNearestLandmark")
                    .WaitUntil("DiscoveredPoints", 5f, "1")
                    .WaitUntil("ChartTableFree", 30f))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<NavigationScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Hazard zones: a voyage lays them along its route from its seed. The subject at the chart table sees them as
        /// hazard columns. Plan: navigation-compass.md (S4). (The server once thinned its hazard field outside the zones;
        /// the field went in H0, hazards-and-boons.md.)
        /// </summary>
        public static readonly ScenarioEntry ChartHazards = new(
            new Scenario.Builder("ChartHazards")
                .InScene(TestScenes.Navigation)
                .Describe("Voyage -> hazard zones along the route on the subject's chart -> leave.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 45f)
                    .Wait(1.5f, "settle after spawn")
                    .Do("VoyageBegin")
                    .WaitUntil("RouteEndsAtDestination", 5f)
                    .Do("PlaceSubjectAtChartTable"))
                .Act(s => s
                    .WaitUntil("SubjectAtChartTable", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceChartTable")
                    .WaitUntil("InteractTargetIs", 5f, "ChartTable")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Chart")
                    .WaitUntil("ChartShowsHazards", 10f, "1")
                    .Checkpoint("hazards-charted")
                    .Hold(0.3f, ScriptedAction.NavigatorLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Chart"))
                .Assert(s => s
                    .WaitUntil("ChartTableManned", 60f)
                    .WaitUntil("ChartTableFree", 30f))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<VoyageScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<NavigationScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The Galleon as the game builds it (Test_Galleon): its loadout mounts the chart table in the starboard forward
        /// hold; the subject takes it, looks through the table's camera in first person and plots a waypoint on the empty
        /// route (the game has no voyage, so no destination), then leaves. Plan: navigation-compass.md (S2).
        /// </summary>
        public static readonly ScenarioEntry GalleonChartTable = new(
            new Scenario.Builder("GalleonChartTable")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: the chart table in the hold -> take it -> plot a waypoint on the empty route -> leave.")
                .Timeout(150f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .Do("PlaceSubjectAtChartTable"))
                .Act(s => s
                    .WaitUntil("SubjectAtChartTable", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceChartTable")
                    .Checkpoint("at-the-table")
                    .WaitUntil("InteractTargetIs", 5f, "ChartTable")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Chart")
                    .WaitUntil("ChartShowsWaypoints", 5f, "0")
                    .Hold(0.25f, ScriptedAction.NavigatorCursorLeft)
                    .Tap(ScriptedAction.NavigatorPlace)
                    .WaitUntil("RouteWaypoints", 5f, "1")
                    .WaitUntil("ChartShowsWaypoints", 5f, "1")
                    .Checkpoint("galleon-chart")
                    .Hold(0.3f, ScriptedAction.NavigatorLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Chart"))
                .Assert(s => s
                    .WaitUntil("ChartTableManned", 60f)
                    .WaitUntil("RouteWaypoints", 30f, "1")
                    .WaitUntil("ChartTableFree", 30f))
                .Build(),
            builder =>
            {
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<NavigationScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The gas squall on the Galleon (plan: gas-squall-event.md): the server starts the event; its field of networked
        /// pockets shows on both peers and on the chart table; one pocket is put on the ship, the real touch rule sets it
        /// off, and the blast breaks points beside the fittings, which the subject sees. The rest of the field is dispersed,
        /// the breaks are repaired, and the event succeeds.
        /// </summary>
        public static readonly ScenarioEntry GasSquallEvent = new(
            new Scenario.Builder("GasSquallEvent")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: GasSquall -> pockets on both peers and the chart -> one goes off on the ship -> fittings break -> repaired -> succeeds.")
                .Timeout(150f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(3f, "settle after spawn; the design builds, its damage points spawn")
                    .Do("StartEvent", "2")
                    .Expect("EventPhase", "Warn")
                    .WaitUntil("GasPocketsLive", 5f, "3")
                    .WaitUntil("EventPhase", 8f, "Field")
                    .Do("MoveGasPocketOntoShip")
                    .WaitUntil("GasPocketsDetonated", 5f, "1")
                    .WaitUntil("BrokenFittingPoints", 3f, "1"))
                .Act(s => s
                    .WaitUntil("EventAnnounced", 10f, "Gas ahead")
                    .WaitUntil("GasPocketsLive", 10f, "2")
                    .WaitUntil("ChartShowsGas", 10f, "1")
                    .WaitUntil("GasPocketsDetonated", 10f, "1")
                    .WaitUntil("BrokenFittingPoints", 10f, "1")
                    .Checkpoint("gas-broke-fittings"))
                .Assert(s => s
                    .Wait(1f, "subject saw the breaks")
                    .Do("DisperseGasField")
                    .WaitUntil("EventPhase", 5f, "Repair")
                    .Do("RestoreAllPoints")
                    .WaitUntil("EventOutcome", 5f, "Succeeded")
                    .WaitUntil("GasPocketsLive", 5f, "0")
                    .Expect("EventAnnounced", "the ship holds"))
                .Build(),
            builder =>
            {
                builder.Register<DesignedEventsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GasChallengeScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<NavigationScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The Galleon's aft cannons (the starboard one at ship (3, 2.5, -4.5), its seat 1.8 m behind at z -6.3, 2.2 m inside
        /// the stern rail): manning one puts the subject on its seat on the quarterdeck, not overboard. Before the plates
        /// moved forward the seat hung past the stern rail; the seat check now also refuses such a seat.
        /// </summary>
        public static readonly ScenarioEntry GalleonSternCannon = new(
            new Scenario.Builder("GalleonSternCannon")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: stand behind the aft starboard cannon -> man it -> seated on the quarterdeck, still aboard -> leave.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .WaitUntil("SocketFitted", 15f, "weapon.cannon")
                    .Do("PlaceSubjectAtCannon", "aft"))
                .Act(s => s
                    .WaitUntil("SubjectAtCannon", 45f, "aft")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceCannon", "aft")
                    .WaitUntil("InteractTargetIs", 5f, "CannonStation")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Cannon")
                    .WaitUntil("CannonManned", 5f, "aft")
                    .Wait(1f, "the seated body settles")
                    .Expect("SubjectNearShipLocal", "3,3.55,-6.3,0.6")
                    .Expect("SubjectOnShip")
                    .Checkpoint("seated-aft")
                    .Hold(0.3f, ScriptedAction.GunnerLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Cannon"))
                .Assert(s => s
                    .WaitUntil("CannonManned", 60f, "aft")
                    .Wait(0.5f, "the seat teleport lands")
                    .Expect("SubjectNearShipLocal", "3,3.55,-6.3,0.6")
                    .WaitUntil("CannonFree", 30f, "aft")
                    .Expect("SubjectOnShip"))
                .Build(),
            builder =>
            {
                builder.Register<CannonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The anchor on the Galleon (plan: peaceful-mode.md, P1): the subject drops it from its winch on the quarterdeck,
        /// then takes the helm and gives full throttle and a turn for a few seconds, and the ship stays where it was; the
        /// chain is down on both peers.
        /// </summary>
        public static readonly ScenarioEntry AnchorHold = new(
            new Scenario.Builder("AnchorHold")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: drop the anchor -> take the helm, full throttle and a turn -> the ship holds where it was -> leave.")
                .Timeout(150f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .WaitUntil("SocketFitted", 15f, "ship.anchor")
                    .Expect("AnchorUp")
                    .Do("PlaceSubjectAtAnchor"))
                .Act(s => s
                    .WaitUntil("SubjectAtAnchor", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceAnchor")
                    .WaitUntil("InteractTargetIs", 5f, "Anchor")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("AnchorDown", 5f)
                    .Checkpoint("anchored")
                    .WaitUntil("SubjectAtHelm", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceHelm")
                    .WaitUntil("InteractTargetIs", 5f, "HelmStation")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Helm")
                    .Hold(4f, ScriptedAction.ShipThrottleUp, ScriptedAction.ShipTurnRight)
                    .Checkpoint("throttled-anchored")
                    .Hold(0.3f, ScriptedAction.HelmLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Helm"))
                .Assert(s => s
                    .WaitUntil("AnchorDown", 60f)
                    .Do("RecordAnchoredShip")
                    .Do("PlaceSubjectAtHelm")
                    .WaitUntil("HelmManned", 60f)
                    .WaitUntil("HelmFree", 30f)
                    .Expect("ShipHeldWithin", "0.3")
                    .Expect("AnchorDown"))
                .Build(),
            builder =>
            {
                builder.Register<AnchorScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<HelmScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// Back aboard by the anchor's rope ladder (plan: peaceful-mode.md, P1): the subject drops the anchor, the server
        /// hangs them on its ladder 6 m below the deck, they hang still with no input, then hold forward and climb, and at the
        /// top they are set down on deck inboard of the winch. The server checks the landing (the subject keeps holding forward
        /// for a while and walks on across the deck, so its own lane cannot).
        /// </summary>
        public static readonly ScenarioEntry LadderClimb = new(
            new Scenario.Builder("LadderClimb")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: drop the anchor -> hang on its ladder below the deck -> hold forward -> climbed aboard.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .WaitUntil("SocketFitted", 15f, "ship.anchor")
                    .Expect("AnchorUp")
                    .Do("PlaceSubjectAtAnchor"))
                .Act(s => s
                    .WaitUntil("SubjectAtAnchor", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceAnchor")
                    .WaitUntil("InteractTargetIs", 5f, "Anchor")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("AnchorDown", 5f)
                    .Checkpoint("anchored")
                    .WaitUntil("SubjectOnLadder", 30f)
                    .Wait(1.5f, "no input: the subject hangs still")
                    .Expect("SubjectOnLadder")
                    .Checkpoint("hanging")
                    .Hold(5f, ScriptedAction.MoveForward))
                .Assert(s => s
                    .WaitUntil("AnchorDown", 60f)
                    .Wait(1f, "the ladder shows on every peer")
                    .Do("PlaceSubjectOnLadder", "6")
                    .WaitUntil("SubjectAtLadderDeck", 40f)
                    .Expect("AnchorDown"))
                .Build(),
            builder =>
            {
                builder.Register<AnchorScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The hand crank (plan: fuel-gathering.md, G3): the Galleon's tank runs dry, the subject stands at the crank on
        /// the cabin floor (ship (-3, 0.55, -4.8) faces it) and presses Primary a few times with empty hands; each press
        /// is a turn that charges the tank, so it rises from empty on the server.
        /// </summary>
        public static readonly ScenarioEntry CrankStart = new(
            new Scenario.Builder("CrankStart")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: the tank runs dry -> at the hand crank, press Primary a few times -> the tank rises from empty.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .WaitUntil("SocketFitted", 15f, "fuel.crank")
                    .Do("DrainTank", "1000")
                    .Do("RecordTankLevel")
                    .Do("PlaceSubjectAtShipLocal", "-3,0.55,-4.8"))
                .Act(s => s
                    .WaitUntil("SubjectNearShipLocal", 45f, "-3,0.55,-4.8,0.8")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "FuelCrank")
                    .WaitUntil("InteractTargetIs", 5f, "FuelCrank")
                    .Checkpoint("at-the-crank")
                    .Hold(0.2f, ScriptedAction.Primary).Wait(0.25f, "let go")
                    .Hold(0.2f, ScriptedAction.Primary).Wait(0.25f, "let go")
                    .Hold(0.2f, ScriptedAction.Primary).Wait(0.25f, "let go")
                    .Hold(0.2f, ScriptedAction.Primary).Wait(0.25f, "let go")
                    .Hold(0.2f, ScriptedAction.Primary).Wait(0.25f, "let go")
                    .Checkpoint("cranked"))
                .Assert(s => s
                    .WaitUntil("SubjectNearShipLocal", 60f, "-3,0.55,-4.8,0.8")
                    .WaitUntil("TankLevelRose", 40f, "0.3"))
                .Build(),
            builder =>
            {
                builder.Register<FuelScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The storm in the world (plan: storm-crossing.md, ST2): outside the zone the storm weighs nothing on the light; with
        /// the zone moved round the ship (half its radius in, in the bands) it weighs on every peer, and its region is set
        /// for the shaders; with the zone moved away it lifts again.
        /// </summary>
        public static readonly ScenarioEntry StormInWorld = new(
            new Scenario.Builder("StormInWorld")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: outside the storm, clear -> the zone round the ship -> the storm weighs on every peer -> away -> clear again.")
                .Timeout(90f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn")
                    .WaitUntil("ZoneSpawned", 30f)
                    .Do("PlaceZone", "out")
                    .Wait(1f, "every peer sees it away"))
                .Act(s => s
                    .WaitUntil("StormWeightBelow", 10f, "0.05")
                    .WaitUntil("StormWeightAbove", 30f, "0.5")
                    .Expect("StormRegionSet")
                    .Checkpoint("in-the-storm")
                    .WaitUntil("StormWeightBelow", 30f, "0.05"))
                .Assert(s => s
                    .Wait(3f, "the subject sees it clear first")
                    .Do("PlaceZone", "in")
                    .WaitUntil("StormWeightAbove", 10f, "0.5")
                    .Wait(4f, "every peer sees the storm")
                    .Do("PlaceZone", "out")
                    .WaitUntil("StormWeightBelow", 10f, "0.05"))
                .Build(),
            builder =>
            {
                builder.Register<EncounterScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<StormScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// The storm's cure (plans: storm-crossing.md ST5, ray-cannons.md R2) on the Galleon, at severity 1 (three hits).
        /// The subject mans the aft cannon; the server puts the storm's heart 55 m off to starboard, so the ship is in its
        /// eye. The round warns, then runs: targets come one at a time round the heart. For each of three, the subject
        /// aims at the target its peer draws and fires; the beam hits it (no collider: the storm answers the beam) and the
        /// heart's strength falls on every peer. The last hit cures the storm: the round ends and the zone rests.
        /// </summary>
        public static readonly ScenarioEntry StormCure = new(
            new Scenario.Builder("StormCure")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: man the aft cannon -> the storm's heart off to starboard -> the round runs -> hit three targets -> the storm is cured, the zone rests, on every peer.")
                .Timeout(180f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .WaitUntil("SocketFitted", 15f, "weapon.cannon")
                    .WaitUntil("ZoneSpawned", 30f)
                    .Do("PlaceSubjectAtCannon", "aft"))
                .Act(s => s
                    .WaitUntil("SubjectAtCannon", 45f, "aft")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceCannon", "aft")
                    .WaitUntil("InteractTargetIs", 5f, "CannonStation")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHasTag", 5f, "State.Occupying.Cannon")
                    .WaitUntil("CannonManned", 5f, "aft")
                    .WaitUntil("ZoneState", 30f, "Running")
                    .Checkpoint("round-runs")
                    .WaitUntil("StormTargetUpFor", 15f, "2")
                    .Do("AimCannonAtStormTarget")
                    .Wait(0.5f, "the barrel turns")
                    .Hold(0.3f, ScriptedAction.GunnerFire)
                    .WaitUntil("HeartStrengthBelow", 10f, "0.9")
                    .Checkpoint("first-hit")
                    .Wait(1.6f, "the cooldown (1.5 s); the spent target goes")
                    .WaitUntil("StormTargetUpFor", 15f, "2")
                    .Do("AimCannonAtStormTarget")
                    .Wait(0.5f, "the barrel turns")
                    .Hold(0.3f, ScriptedAction.GunnerFire)
                    .WaitUntil("HeartStrengthBelow", 10f, "0.5")
                    .Wait(1.6f, "the cooldown (1.5 s); the spent target goes")
                    .WaitUntil("StormTargetUpFor", 15f, "2")
                    .Do("AimCannonAtStormTarget")
                    .Wait(0.5f, "the barrel turns")
                    .Hold(0.3f, ScriptedAction.GunnerFire)
                    .WaitUntil("HeartStrengthBelow", 10f, "0.01")
                    .WaitUntil("ZoneState", 10f, "Resting")
                    .Checkpoint("cured")
                    .Hold(0.3f, ScriptedAction.GunnerLeave)
                    .WaitUntil("SubjectLacksTag", 5f, "State.Occupying.Cannon"))
                .Assert(s => s
                    .WaitUntil("CannonManned", 60f, "aft")
                    .Do("PlaceHeartOffCannon", "55 aft 90")
                    .WaitUntil("ZoneState", 30f, "Running")
                    .WaitUntil("ZoneState", 120f, "Resting")
                    .WaitUntil("HeartStrengthBelow", 5f, "0.01"))
                .Build(),
            builder =>
            {
                builder.Register<EncounterScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<StormScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<CannonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        /// <summary>
        /// An encounter zone's round (plan: encounters.md, E1). The server moves the zone around the ship to stand in for
        /// sailing in and out: in, the warning shows on both peers; out again during it, nothing starts; in for good, it
        /// runs once the warning ends; out, it is abandoned and rests.
        /// </summary>
        public static readonly ScenarioEntry EncounterZone = new(
            new Scenario.Builder("EncounterZone")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: the zone around the ship -> the warning -> away again, nothing starts -> around it for good -> it runs -> away -> abandoned, it rests.")
                .Timeout(120f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn")
                    .WaitUntil("ZoneSpawned", 30f)
                    .Do("PlaceZone", "out")
                    .WaitUntil("ZoneState", 5f, "Idle"))
                .Act(s => s
                    .WaitUntil("ZoneState", 30f, "Warning")
                    .WaitUntil("ZoneState", 40f, "Running")
                    .WaitUntil("ZoneState", 30f, "Resting"))
                .Assert(s => s
                    .Do("PlaceZone", "in")
                    .WaitUntil("ZoneState", 5f, "Warning")
                    .Wait(1.5f, "every peer sees the warning")
                    .Do("PlaceZone", "out")
                    .WaitUntil("ZoneState", 5f, "Idle")
                    .Wait(1f, "turned back: nothing starts")
                    .Expect("ZoneState", "Idle")
                    .Do("PlaceZone", "in")
                    .WaitUntil("ZoneState", 5f, "Warning")
                    .WaitUntil("ZoneState", 15f, "Running")
                    .Wait(1.5f, "every peer sees it run")
                    .Do("PlaceZone", "out")
                    .WaitUntil("ZoneState", 5f, "Resting")
                    .Wait(1.5f, "every peer sees it rest"))
                .Build(),
            builder => builder.Register<EncounterScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>());

        /// <summary>
        /// Fuel from the islands (plan: fuel-gathering.md, G1 and G2; the bin is ship-shield.md SH3): the subject stands at the crystal vein nearest the
        /// Galleon, holds Interact while the work runs (IA_MineCrystal's hold time), and a crystal breaks off into their
        /// hands; back aboard, they put it in the crystal bin (ship (1.2, -2.45, -5) stands in front of it) and take it out
        /// again, then feed it to the receiver (ship (-0.9, -2.45, -5.5) stands in front of it) and the drained tank rises.
        /// The vein shows one crystal fewer and the bin its count on both peers.
        /// </summary>
        public static readonly ScenarioEntry CrystalRun = new(
            new Scenario.Builder("CrystalRun")
                .InScene(TestScenes.Galleon)
                .Describe("On the Galleon: at the nearest vein, hold Interact -> a crystal in hand, the vein one fewer -> aboard, into the bin and out again -> feed it to the receiver -> the tank rises.")
                .Timeout(180f)
                .Arrange(s => s
                    .WaitUntil("SubjectReady", 60f)
                    .Wait(2f, "settle after spawn; the design builds")
                    .WaitUntil("VeinsSpawned", 60f, "1")
                    .WaitUntil("SocketFitted", 15f, "fuel.receiver")
                    .Do("DrainTank", "30")
                    .Do("RecordTankLevel")
                    .Do("PlaceSubjectAtVein"))
                .Act(s => s
                    .WaitUntil("SubjectAtVein", 45f)
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceVein")
                    .WaitUntil("InteractTargetIs", 5f, "Vein_Amber")
                    .Checkpoint("at-the-vein")
                    .Hold(3.2f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_FuelCrystal")
                    .WaitUntil("VeinCrystals", 5f, "2")
                    .Checkpoint("mined")
                    .WaitUntil("SubjectNearShipLocal", 45f, "1.2,-2.45,-5,0.8")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "CrystalBin")
                    .WaitUntil("InteractTargetIs", 5f, "CrystalBin")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "none")
                    .WaitUntil("BinCrystals", 5f, "1")
                    .Checkpoint("stored")
                    .Wait(0.5f, "let go of Interact")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "ITEM_FuelCrystal")
                    .WaitUntil("BinCrystals", 5f, "0")
                    .Checkpoint("taken")
                    .WaitUntil("SubjectNearShipLocal", 45f, "-0.9,-2.45,-5.5,0.8")
                    .Wait(0.5f, "teleport settles")
                    .Do("FaceObject", "MotorFillPort")
                    .WaitUntil("InteractTargetIs", 5f, "MotorFillPort")
                    .Hold(0.3f, ScriptedAction.Interact)
                    .WaitUntil("SubjectHolds", 5f, "none")
                    .Checkpoint("fed"))
                .Assert(s => s
                    .WaitUntil("SubjectHolds", 60f, "ITEM_FuelCrystal")
                    .Expect("VeinCrystals", "2")
                    .Do("PlaceSubjectAtShipLocal", "1.2,-2.45,-5")
                    .WaitUntil("BinCrystals", 40f, "1")
                    .WaitUntil("SubjectHolds", 20f, "ITEM_FuelCrystal")
                    .Expect("BinCrystals", "0")
                    .Do("PlaceSubjectAtShipLocal", "-0.9,-2.45,-5.5")
                    .WaitUntil("TankLevelRose", 40f)
                    .WaitUntil("SubjectHolds", 10f, "none"))
                .Build(),
            builder =>
            {
                builder.Register<GatheringScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<FuelScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ItemsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<TargetingScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<GrappleHookScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
                builder.Register<ShipSocketsScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            });

        private static readonly ScenarioEntry[] All = { NetCatch, EquipCycle, CoreBoot, LanDiscovery, ShipDamage, RepairLoop, ShipDamageLateJoin, AimPitch, InteractRack, HullStressEvent, CannonShot, HazardStrike, CannonMisfire, VoyageLoop, LateJoinBoarding, HelmSteer, IslandsPerVoyage, IslandRam, BoonGate, EngineBoost, BoilerCombo, ChallengeRelay, DesignedShipFlies, ShipyardRoundTrip, ShipPickerSwap, LookApplies, DayMoves, CloudHoleOpens, CloudWebReal, SoarFromDive, CampLoop, IslandForage, MountFitting, HookAboard, ShipStairs, GalleonLoadout, FrameDeck, FrameRoundTrip, FrameStories, FramePartPlace, OverboardRespawn, LeashClimb, WinchRescue, GalleonLeashClimb, CompassCourse, ChartPlot, ChartIslands, ChartHazards, GalleonChartTable, GasSquallEvent, GalleonSternCannon, AnchorHold, LadderClimb, CrystalRun, CrankStart, EncounterZone, CrewBodyMoves, StormInWorld, StormCure };

        public static System.Collections.Generic.IReadOnlyList<ScenarioEntry> Entries => All;

        public static string Names => string.Join(", ", All.Select(entry => entry.Scenario.Name));

        public static bool TryGet(string? name, out ScenarioEntry entry)
        {
            entry = All.FirstOrDefault(candidate => string.Equals(candidate.Scenario.Name, name, StringComparison.OrdinalIgnoreCase))!;
            return entry != null;
        }
    }
}
