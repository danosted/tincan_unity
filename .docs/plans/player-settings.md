Status: Approved (2026-10-31); second in [core-systems-roadmap.md](core-systems-roadmap.md)

# Player settings: one store, one Settings menu, features add their own

## Why
A player can change their keys and nothing else. Look sensitivity, field of view, volume, VSync and a player name are
Inspector values or do not exist. Every playtest asks for them, and the audio plan ([audio-mixer.md](audio-mixer.md))
and the crew pings plan ([crew-pings-and-markers.md](crew-pings-and-markers.md)) need somewhere to keep volumes and a
name.

## Today (verified 2026-10-31)
- Key bindings are the only stored preference: `IInputBindingStore` → `PlayerPrefsInputBindingStore`
  (`Core/Input/`), one JSON string under `TinCan.InputBindings`, per machine and per MPPM clone. The Controls menu
  expands a `MenuItemKind.Bindings` row into one row per key (`MenuUseCase`).
- Look sensitivity is a serialized field on `LookView` (`Core/Humanoid/LookView.cs`), read by `PlayerLookUseCase`.
  The cannon has its own `CannonConfig.AimSensitivity`; the free camera has its own `RotationSensitivity`.
- Field of view is `FirstPersonCameraConfig.FieldOfView`, applied by `FirstPersonViewRig`.
- Frame rate and VSync are not set by the game (the server's frame cap is `NGONetworkService.ServerFrameRate`). The
  Editor's Game view VSync has been mistaken for input lag before.
- Menu rows are `Command`, `TextField`, `Toggle`, `Submenu`, `Back`, `Bindings`, `Binding`, `Note`. Values are
  strings, kept per session only (`UI_FRAMEWORK.md`).

## Design

### A setting is data, contributed by whoever owns it
- `SettingDefinition` (ScriptableObject, `Core/Domain/Settings/`): key (`look.sensitivity`), label, group (Controls,
  View, Audio, Graphics, Player), kind (`Slider` with min, max, step; `Toggle`; `Choice` with options; `Text` with max
  length), default.
- Core settings live in the settings installer's list. A feature contributes its own through
  `FeatureInstaller.IExtension<SettingDefinition>`, the way input contexts and cue notifies are contributed: the
  first-person camera adds `view.fov`, the cannon could add `cannon.aim_sensitivity`. A setting exists only where its
  feature is loaded.

### One service, read through a contract
- `IPlayerSettings` (Core.Domain): `float GetFloat(key)`, `bool GetBool(key)`, `string GetString(key)`, `Set(key,
  value)`, `ResetGroup(group)`, `event Changed(key)`. Unknown keys log once and return the default.
- `PlayerSettingsUseCase` in a new core assembly `TinCan.Settings` with `SettingsFeatureInstaller` (listed in
  `Profile_Base` and `Profile_Test_Core`; no change to `ProjectLifetimeScope`). It clamps and validates values against
  their definition (`SettingValueProcessor`, pure, tested), saves on change (debounced) and loads on start.
- `ISettingsStore` holds one JSON object of the values that differ from their default (see Open question 1).
- Consumers read the contract and listen to `Changed`; they keep their Inspector value as the fallback when no settings
  service is registered (test profiles stay as they are):
  - `PlayerLookUseCase`: sensitivity from `look.sensitivity` and `look.invert_y`.
  - `FirstPersonViewRig`: `view.fov`.
  - `GraphicsSettingsApplier` (in `TinCan.Settings`): `graphics.vsync`, `graphics.frame_cap`, `graphics.fullscreen`,
    `graphics.quality`. Skipped in batch mode and on a dedicated server.
  - Audio volumes: applied by the audio plan's volume binder.
  - `player.name`: read by the crew pings plan's nameplates.

### The Settings menu
- A `Menu_Settings` asset with one submenu per group; the existing Controls menu becomes the Controls group's page.
- A new placeholder kind `MenuItemKind.Settings` (field: group) expands at snapshot time into one row per setting in that
  group, like `Bindings` does. New generated row kinds `Slider` and `Choice`; `Toggle` and `TextField` are reused.
  Rows write straight to `IPlayerSettings`; a "Reset to defaults" row per page.
- `MenuOverlayView` (`App/Views/`) learns to draw a slider and a choice (left/right). It stays the placeholder view.
- Reachable from the main menu and the in-game menu.

### Runs that must not read a player's settings
Scenario, perf and bot runs must not change with what a developer saved (a slow sensitivity would bend a bot route).
A launch argument `-defaultsettings` (read through `LaunchArguments`) makes the store read nothing and write nothing;
`verify.ps1` and `perf.ps1` pass it.

## Slices
1. **S1 Core service:** `SettingDefinition`, `IPlayerSettings`, the use case, the store, the installer, the launch
   argument. Sensitivity, invert Y and FOV move over. EditMode tests: the value processor, load/save round trip,
   unknown keys, a feature's contribution, defaults-only mode.
2. **S2 Menu:** the `Settings` row kind, `Slider` and `Choice` rows, `Menu_Settings`, the view widgets. Tests in the
   `MenuBindingRowsTests` style. Builds on [ui-and-menus.md](ui-and-menus.md) U1 and U2 (layers, menu requests).
3. **S3 Graphics and player:** the graphics applier, `player.name`.
4. Audio volumes land with [audio-mixer.md](audio-mixer.md) S2.

## Decisions (developer, 2026-10-31)
1. **PlayerPrefs, one JSON string**, like the bindings: already separate per MPPM clone, no file handling.
2. **Key bindings stay in their own store** for now; one interface behind both can come later.
3. **One `look.sensitivity`** that scales each view's tuned value (look, cannon aim, free camera), so a station keeps
   its own feel.

## Progress
**S1 and S2 done (2026-11-01).** Where it differs from the design above:
- `TinCan.Settings` is a core assembly, so its installer loads in every scene and no profile lists it (the
  `FeatureProfiles_ListNoCoreInstallers` rule). Consumers still keep their tuned value when no settings service is
  passed in (their test constructors), and a key with no loaded definition reads as "no change" in `LookSettings`.
- Values are not trimmed (a name is typed a letter at a time), only cut to the maximum length.
- `IPlayerSettings` also has `Has(key)` and `Definitions` (the menu reads them).
- Invert Y applies to every mouse view (look, cannon aim, free camera), not only the body's look.
- `-defaultsettings` is set by `NetHarnessPlayerTagsMenu.Run` (every scenario and bot run, so `verify.ps1` gets it
  without a change) and by `perf.ps1`.
- The menu: `MenuItemKind.Settings` (placeholder with `SettingsGroup`), generated `Slider` and `Choice` rows
  (`MenuItemRow` carries `Min`, `Max`, `Step`, `Options`), `MenuSettingsRows`, `Menu_Settings` with Controls (look
  settings, then Key bindings) and View pages; the main menu's Controls row became Settings. Audio, Graphics and
  Player pages come with S3 and the audio plan.
- View tests use `com.unity.ui.test-framework` (UI plan U3): `MenuOverlayViewClickTests`.
- Assets: **TinCan > Dev > Settings > Build Assets** (`DevTools/Editor/SettingsAssetBuilder.cs`).

Next: S3 (graphics applier, `player.name`).

## Verification
- EditMode suite green, plus the new tests.
- `verify.ps1 -UpTo Tests`. One play run of a scenario that looks around (`HookAboard`) to show that
  `-defaultsettings` keeps it unchanged.
- Human: change each setting in the menu, restart, confirm it stuck; MPPM Player 2 keeps its own.
