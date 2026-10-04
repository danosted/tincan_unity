#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Core.Input;
using TinCan.Core.Interaction;
using TinCan.Core.Possession;
using TinCan.Core.UI;
using TinCan.Core.UI.Commands;
using TinCan.Features.FreeCamera;
using TinCan.Features.Helm;
using TinCan.Features.Shipyard;
using TinCan.Features.Weapons.Cannon;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Input > Build Assets: everything around Assets/Input/TinCanControls.inputactions, as reviewable code.
    /// The actions asset itself (maps, actions, default bindings) is edited in Unity's Input Actions editor; this builder
    /// reads it and (re)writes one <see cref="InputActionId"/> per action, the command assets, every context (condition,
    /// priority, blocking, slots, routes), which ability inputs each action presses, the <see cref="InputConfig"/>, and the
    /// installer fields that point at them. Safe to rerun: assets are updated in place, so references stay valid.
    /// Model: .docs/INPUT.md.
    /// </summary>
    public static class InputAssetBuilder
    {
        private const string Root = "Assets/Input/";
        private const string ActionsAsset = Root + "TinCanControls.inputactions";
        private const string Actions = Root + "Actions/";
        private const string Commands = Root + "Commands/";
        private const string Contexts = Root + "Contexts/";
        private const string AbilityInputs = "Assets/Abilities/Inputs/";
        private const string Installers = "Assets/Resources/Installers/";
        private const string Menus = "Assets/UI/Menus/";

        /// <summary>Display name and meaning of each action, by Map/Action. An action missing here is built with its own name.</summary>
        private static readonly Dictionary<string, (string Display, string Meaning, bool Rebindable)> Meanings = new()
        {
            ["Global/Cancel"] = ("Back / Menu", "Back out: close the menu, or open the main menu.", true),
            ["Global/SwitchPossession"] = ("Switch Control", "Cycle to the next thing you may control.", true),
            ["Camera/Look"] = ("Look", "Turn the camera of whatever you control.", false),
            ["Humanoid/Move"] = ("Move", "Walk.", true),
            ["Humanoid/Jump"] = ("Jump", "Jump.", true),
            ["Humanoid/Sprint"] = ("Sprint", "Run while held.", true),
            ["Humanoid/Interact"] = ("Interact", "Use what you look at: pick up, pour, man a cannon.", true),
            ["Humanoid/Primary"] = ("Use", "Use the held item or ability.", true),
            ["Humanoid/Secondary"] = ("Secondary Use", "Secondary use of the held item.", true),
            ["Airship/Throttle"] = ("Throttle", "Speed up or slow down the airship.", true),
            ["Airship/Yaw"] = ("Turn", "Turn the airship.", true),
            ["Airship/Pitch"] = ("Pitch", "Nose the airship up or down.", true),
            ["Airship/Leave"] = ("Leave Helm", "Let go of the helm.", true),
            ["Gunner/Aim"] = ("Aim Cannon", "Swing the cannon's barrel.", false),
            ["Gunner/Fire"] = ("Fire Cannon", "Fire the cannon you are manning.", true),
            ["Gunner/Leave"] = ("Leave Cannon", "Step away from the cannon.", true),
            ["FreeCamera/Move"] = ("Fly", "Fly the free camera (spectator, a dev tool: not in the Controls menu).", false),
            ["DevTools/ToggleNetOverlay"] = ("Net Overlay", "Show or hide the net harness readout.", false),
            ["Shipyard/Point"] = ("Shipyard Cursor", "Where the cursor points in the shipyard.", false),
            ["Shipyard/Orbit"] = ("Shipyard Orbit", "Turn the shipyard camera while Orbit is held.", false),
            ["Shipyard/OrbitHold"] = ("Orbit Camera", "Hold to orbit the shipyard camera around the ship.", true),
            ["Shipyard/Zoom"] = ("Shipyard Zoom", "Zoom the shipyard camera.", false),
            ["Shipyard/Place"] = ("Place Part", "Place the selected part where the cursor points.", true),
            ["Shipyard/DeleteMode"] = ("Delete Mode", "Toggle delete mode in the shipyard: click a highlighted part to delete it.", true),
            ["Shipyard/Rotate"] = ("Turn Part", "Turn the selected part a quarter turn.", true),
            ["Shipyard/NextPart"] = ("Next Part", "Select the next part.", true),
            ["Shipyard/PreviousPart"] = ("Previous Part", "Select the previous part.", true),
            ["Shipyard/Undo"] = ("Undo", "Undo the last change to the design.", true),
            ["Shipyard/Redo"] = ("Redo", "Redo the change just undone.", true),
            ["Shipyard/LevelUp"] = ("Level Up", "Build one level higher in the shipyard.", true),
            ["Shipyard/LevelDown"] = ("Level Down", "Build one level lower in the shipyard.", true),
        };

        [MenuItem("TinCan/Dev/Input/Build Assets")]
        public static void Build()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAsset)
                        ?? throw new InvalidOperationException($"Missing {ActionsAsset}.");

            var ids = BuildActionIds(asset);
            InputActionId Id(string path) => ids.TryGetValue(path, out var id) ? id : throw new InvalidOperationException($"{ActionsAsset} has no action {path}.");

            // Commands
            var openMenu = Command<OpenMenuCommand>("Command_OpenMenu", "Open the main menu (offline, or in your own body).");
            var menuBack = Command<MenuBackCommand>("Command_MenuBack", "Step back one menu; the last Back closes it.");
            var switchPossession = Command<SwitchPossessionCommand>("Command_SwitchPossession", "Control the next thing you may control.");
            var toggleCursor = Command<ToggleCursorCommand>("Command_ToggleCursor", "Free or recapture the cursor while flying the free camera.");
            var toggleOverlay = Command<ToggleNetOverlayCommand>("Command_ToggleNetOverlay", "Show or hide the net harness readout.");
            var shipyardMenu = Command<OpenShipyardMenuCommand>("Command_OpenShipyardMenu", "Open the shipyard menu: save, load, new, launch, exit.");

            // Contexts (priority: higher sees an action first)
            var global = Context<GlobalInputContext>("Context_Global", c =>
            {
                c.Cancel = Id("Global/Cancel");
                c.SwitchPossession = Id("Global/SwitchPossession");
                c.Configure(InputContextActivation.Always, 0);
                c.SetContents(None(), NoActions(), new[]
                {
                    new InputRoute(c.Cancel, openMenu),
                    new InputRoute(c.SwitchPossession, switchPossession),
                }, "Always on, lowest: Cancel opens the main menu when nothing above wanted it; Tab switches control.");
            });
            var camera = Context<CameraInputContext>("Context_Camera", c =>
            {
                c.Look = Id("Camera/Look");
                c.Configure(InputContextActivation.Always, 100);
                c.SetContents(None(), NoActions(), NoRoutes(), "Mouse look for whatever you control. Blocked by menus and by stations that aim with the mouse.");
            });
            var humanoid = Context<HumanoidInputContext>("Context_Humanoid", c =>
            {
                c.Move = Id("Humanoid/Move");
                c.Jump = Id("Humanoid/Jump");
                c.Sprint = Id("Humanoid/Sprint");
                c.Interact = Id("Humanoid/Interact");
                c.Primary = Id("Humanoid/Primary");
                c.Secondary = Id("Humanoid/Secondary");
                c.Configure(InputContextActivation.WhilePossessing, 200, PossessedActorKind.Humanoid);
                c.SetContents(None(), NoActions(), NoRoutes(), "On foot, in your own body. Read by HumanoidMovementUseCase; Sprint, Interact and Primary are also ability bits.");
            });
            var freeCamera = Context<FreeCameraInputContext>("Context_FreeCamera", c =>
            {
                c.Move = Id("FreeCamera/Move");
                c.Configure(InputContextActivation.WhilePossessing, 300, PossessedActorKind.Other);
                c.SetContents(None(), NoActions(), new[] { new InputRoute(Id("Global/Cancel"), toggleCursor) },
                    "Flying the spectator camera. Read by FreeCameraMovementUseCase; Cancel frees the cursor.");
            });
            var gunner = Context<GunnerInputContext>("Context_Gunner", c =>
            {
                c.Aim = Id("Gunner/Aim");
                c.Fire = Id("Gunner/Fire");
                c.Leave = Id("Gunner/Leave");
                c.Configure(InputContextActivation.WhilePossessedHasTag, 400, tag: Load<GameplayTag>("Assets/Abilities/Tags/State.Occupying.Cannon.asset"));
            });
            SetContents(gunner, new InputContext[] { humanoid, camera }, NoActions(), NoRoutes(),
                "Manning a cannon (State.Occupying.Cannon). Silences walking and looking: the mouse swings the barrel (GunnerAimUseCase). Fire and Leave are the Primary and Interact ability bits.");
            var helmsman = Context<HelmsmanInputContext>("Context_Helmsman", c =>
            {
                c.Throttle = Id("Airship/Throttle");
                c.Yaw = Id("Airship/Yaw");
                c.Pitch = Id("Airship/Pitch");
                c.Leave = Id("Airship/Leave");
                c.Configure(InputContextActivation.WhilePossessedHasTag, 400, tag: Load<GameplayTag>("Assets/Abilities/Tags/State.Occupying.Helm.asset"));
            });
            SetContents(helmsman, new InputContext[] { humanoid }, NoActions(), NoRoutes(),
                "At the helm (State.Occupying.Helm). Silences walking but not looking: the helmsman looks around while steering. The axes travel in the predicted input (HelmInputUseCase); Leave is the Interact ability bit.");
            var menu = Context<InputContext>("Context_Menu", c =>
            {
                c.Configure(InputContextActivation.WhileMenuOpen, 900, blocksAllLower: true);
                c.SetContents(None(), NoActions(), new[] { new InputRoute(Id("Global/Cancel"), menuBack) },
                    "A menu is open: everything below is silent; Cancel steps back.");
            });
            var devTools = Context<InputContext>("Context_DevTools", c =>
            {
                c.Configure(InputContextActivation.Always, 950);
                c.SetContents(None(), NoActions(), new[] { new InputRoute(Id("DevTools/ToggleNetOverlay"), toggleOverlay) },
                    "Only while the net harness runs (contributed by its installer): F3 toggles the readout.");
            });
            var shipyard = Context<ShipyardInputContext>("Context_Shipyard", c =>
            {
                c.Point = Id("Shipyard/Point");
                c.Orbit = Id("Shipyard/Orbit");
                c.OrbitHold = Id("Shipyard/OrbitHold");
                c.Zoom = Id("Shipyard/Zoom");
                c.Place = Id("Shipyard/Place");
                c.DeleteMode = Id("Shipyard/DeleteMode");
                c.Rotate = Id("Shipyard/Rotate");
                c.NextPart = Id("Shipyard/NextPart");
                c.PreviousPart = Id("Shipyard/PreviousPart");
                c.Undo = Id("Shipyard/Undo");
                c.Redo = Id("Shipyard/Redo");
                c.LevelUp = Id("Shipyard/LevelUp");
                c.LevelDown = Id("Shipyard/LevelDown");
                c.Configure(InputContextActivation.WhileOpened, 500, blocksAllLower: true);
                c.SetContents(None(), NoActions(), new[] { new InputRoute(Id("Global/Cancel"), shipyardMenu) },
                    "The shipyard is open (contributed by its installer): build with the mouse, everything below is silent; Cancel opens the shipyard menu. Read by ShipyardUseCase.");
            });
            var rebinding = Context<InputContext>("Context_Rebinding", c =>
            {
                c.Configure(InputContextActivation.WhileRebinding, 1000, blocksAllLower: true);
                c.SetContents(None(), NoActions(), NoRoutes(), "The Controls menu waits for a key: every action is silent until it has one.");
            });

            // Ability inputs: bit order is the config's list order (unchanged: Sprint 0, Primary 1, Interact 2).
            var sprint = AbilityInput("Input_Sprint", Id("Humanoid/Sprint"));
            var primary = AbilityInput("Input_Primary", Id("Humanoid/Primary"), Id("Gunner/Fire"));
            var interact = AbilityInput("Input_Interact", Id("Humanoid/Interact"), Id("Gunner/Leave"), Id("Airship/Leave"));

            var config = Asset<InputConfig>(Root + "InputConfig.asset", c =>
            {
                c.Actions = asset;
                c.ActionIds = ids.Values.ToList();
                c.Contexts = new List<InputContext> { global, camera, humanoid, menu, rebinding };
                c.GameplayInputs = new List<GameplayInput> { sprint, primary, interact };
            });

            // Installers: core input, and the features that contribute a context.
            var installer = Asset<InputFeatureInstaller>(Installers + "InputFeatureInstaller.asset", _ => { });
            SetField(installer, "_config", config);
            SetField(Load<Object>(Installers + "FreeCameraFeatureInstaller.asset"), "_controls", freeCamera);
            SetField(Load<Object>(Installers + "CannonFeatureInstaller.asset"), "_controls", gunner);
            SetField(Load<Object>(Installers + "HelmFeatureInstaller.asset"), "_controls", helmsman);
            SetField(Load<Object>(Installers + "NetTestHarnessFeatureInstaller.asset"), "_controls", devTools);
            var shipyardInstaller = AssetDatabase.LoadAssetAtPath<Object>(Installers + "ShipyardFeatureInstaller.asset");
            if (shipyardInstaller != null) SetField(shipyardInstaller, "_controls", shipyard);

            BuildControlsMenu();

            AssetDatabase.SaveAssets();
            Debug.Log($"[InputAssetBuilder] {ids.Count} actions, 10 contexts, 6 commands, 3 ability inputs and the Controls menu built.");
        }

        /// <summary>Menu_Controls (one row per key, Reset, Back), reachable from Menu_Main's Controls row.</summary>
        private static void BuildControlsMenu()
        {
            var controls = Asset<MenuDefinition>(Menus + "Menu_Controls.asset", menu =>
            {
                var serialized = new SerializedObject(menu);
                serialized.FindProperty("_menuId").stringValue = "controls";
                serialized.FindProperty("_title").stringValue = "Controls";
                var items = serialized.FindProperty("_items");
                items.arraySize = 3;
                SetItem(items.GetArrayElementAtIndex(0), "keys", "Keys", MenuItemKind.Bindings);
                SetItem(items.GetArrayElementAtIndex(1), "reset", "Reset to Defaults", MenuItemKind.Command, ResetBindingsMenuCommand.Id);
                SetItem(items.GetArrayElementAtIndex(2), "back", "Back", MenuItemKind.Back);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            var main = Load<MenuDefinition>(Menus + "Menu_Main.asset");
            if (main.Items.Any(item => item.ItemId == "controls")) return;

            var mainSerialized = new SerializedObject(main);
            var mainItems = mainSerialized.FindProperty("_items");
            int at = Math.Max(0, mainItems.arraySize - 1); // before Quit
            mainItems.InsertArrayElementAtIndex(at);
            SetItem(mainItems.GetArrayElementAtIndex(at), "controls", "Controls", MenuItemKind.Submenu, submenu: controls);
            mainSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(main);
        }

        private static void SetItem(SerializedProperty item, string id, string label, MenuItemKind kind, string command = "", MenuDefinition? submenu = null)
        {
            item.FindPropertyRelative(nameof(MenuItemDefinition.ItemId)).stringValue = id;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Label)).stringValue = label;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Kind)).enumValueIndex = (int)kind;
            item.FindPropertyRelative(nameof(MenuItemDefinition.CommandId)).stringValue = command;
            item.FindPropertyRelative(nameof(MenuItemDefinition.Submenu)).objectReferenceValue = submenu;
            item.FindPropertyRelative(nameof(MenuItemDefinition.DefaultValue)).stringValue = string.Empty;
        }

        private static Dictionary<string, InputActionId> BuildActionIds(InputActionAsset asset)
        {
            var ids = new Dictionary<string, InputActionId>();
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    string path = $"{map.name}/{action.name}";
                    var (display, meaning, rebindable) = Meanings.TryGetValue(path, out var known) ? known : (action.name, string.Empty, true);
                    ids[path] = Asset<InputActionId>($"{Actions}{map.name}.{action.name}.asset",
                        id => id.Configure(action.id.ToString(), path, display, meaning, rebindable));
                }
            }
            return ids;
        }

        private static T Command<T>(string name, string description) where T : InputCommand =>
            Asset<T>(Commands + name + ".asset", command => SetField(command, "_description", description));

        private static T Context<T>(string name, Action<T> configure) where T : InputContext =>
            Asset(Contexts + name + ".asset", configure);

        private static void SetContents(InputContext context, IEnumerable<InputContext> blocks, IEnumerable<InputActionId> actions,
            IEnumerable<InputRoute> routes, string description)
        {
            context.SetContents(blocks, actions, routes, description);
            EditorUtility.SetDirty(context);
        }

        private static GameplayInput AbilityInput(string name, params InputActionId[] actions)
        {
            var input = Load<GameplayInput>(AbilityInputs + name + ".asset");
            input.SetActions(actions);
            EditorUtility.SetDirty(input);
            return input;
        }

        private static InputContext[] None() => Array.Empty<InputContext>();
        private static InputActionId[] NoActions() => Array.Empty<InputActionId>();
        private static InputRoute[] NoRoutes() => Array.Empty<InputRoute>();

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                EnsureDirectory(Path.GetDirectoryName(path)!.Replace('\\', '/'));
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing asset {path}.");

        private static void SetField(Object target, string field, Object? value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetField(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureDirectory(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
