Status: Approved (2026-10-31); U0-U4 done 2026-11-01 (see Progress); first in [core-systems-roadmap.md](core-systems-roadmap.md)

# UI and menus: layers, one owner per screen, and tests for what is drawn

## Why
Menus and panels draw over each other in the wrong order, and several features fight over the one menu stack. The
headless model has tests, but nothing tests what is actually on screen: which layer is on top, what happens when two
features want a menu at once, or who owns the cursor. The settings, pings and hosting plans all add screens, so the
base must hold first.

## Today (verified 2026-10-31)
**Surfaces**
- `MenuOverlayView` and `HudOverlayView` (`App/Views/`): code-built UI Toolkit, each on its own `UIDocument` under the
  `GameLifetimeScope` prefab, both on `Settings/UI/DefaultPanelSettings`. Sort order: menu 10, HUD 0.
- `ShipyardPanelView` creates its own `UIDocument` in code on the same panel settings with `sortingOrder = 10`: the
  same order as the menu, so which one draws on top is not defined.
- `AttributeBarView`: world-space uGUI canvas (`Prefabs/UI/AttributeBar.prefab`, on damage points).
- `NetHarnessOverlayView`: IMGUI `OnGUI` (dev only, F3), drawn over everything.
- The chart and the compass are 3D objects, not UI.
- Sort orders are numbers typed in a prefab and in code, and nothing says which layer a number belongs to.

**One menu stack, seven openers.** `MenuUseCase` keeps one stack. These push onto it without knowing about each other:
`MainMenuBootstrap`, `OpenMenuInputHandler` (Esc), `OverboardMenuUseCase` (pushes itself back on top every tick
until a choice is made), `OpenLeashMenuInputHandler`, `ShipFittingMenuUseCase`, `ShipyardMenuInputHandler`,
`VoyageEndScreenPresenter` (clears the stack and reopens itself unless another menu is up). Conflicts that follow
from the code:
- While overboard, Esc's menu is covered again on the next tick, so the pause menu cannot be used.
- Losing the voyage while overboard: the end screen waits behind the overboard menu ("another menu is the player's
  choice"), and then a `CloseAll` throws away whatever else was stacked.
- A fitting chooser opening while another menu is up pushes on top of it; Back returns into the other menu.

**The cursor has five writers:** `MenuOverlayView`, `PossessionCursorResponder`, `ToggleCursorInputHandler` (free
camera), `ShipyardStage`, plus whatever ran last. Nothing decides between them.

**Tests:** the model is covered (`MenuUseCaseTests`, `MenuBindingRowsTests`, `MenuCommand*Tests`,
`MainMenu*Tests`, `HudUseCaseTests`, `OverboardMenuUseCaseTests`, the HUD presenters). `MenuOverlayViewTests` has
two tests (text field editing). Nothing tests layering, arbitration between openers, the HUD under a menu, or the cursor.

**Known limits** (`UI_FRAMEWORK.md`): mouse only, inline styles, rows top to bottom, menus matched to commands by
string ids.

## Design

### 1. Layers, defined once
- `UiLayer` (Core.UI): `World` (world indicators), `Hud`, `Panels` (feature panels such as the shipyard), `Menus`,
  `Modal` (end screen, overboard choice, connection messages), `Toasts`, `Dev`. Each has one sort order in one place.
- `IUiLayers` (Core.UI) gives a feature a root to draw into: `Root(UiLayer)`. Two ways to build it (Open question 1):
  (a) one `UIDocument` with a container per layer, in order; (b) one `UIDocument` per layer, its sort order taken from
  `UiLayer`.
- Feature panels stop creating their own documents with typed numbers; the shipyard panel moves first.
- The dev overlay moves from `OnGUI` to the `Dev` layer, so it follows the same rules.
- An architecture test fails on a `UIDocument` or `sortingOrder` set outside `IUiLayers`.

### 2. Menus are requested, not pushed
- A feature asks for a menu with an owner and a kind: `MenuRequest(owner, menu, MenuKind.Modal | Screen | Panel,
  pinned)`. `Modal` beats `Screen` beats `Panel`, and within a kind the newest wins.
- `pinned` replaces the self-reopening loops: a pinned request cannot be dismissed with Esc, and stays until its owner
  withdraws it (the overboard choice until chosen, the end screen until a new voyage).
- `MenuArbitrationProcessor` (pure) decides what is shown from the open requests; `MenuUseCase` keeps one stack per
  request, so Back works inside a request and never falls into another feature's menu.
- Esc goes to the top request: Back if it can, otherwise it opens the pause menu as a `Screen` under any pinned `Modal`
  (decision: Open question 2).
- The seven openers move over one by one; `IMenuSystem.Open` stays for a while as a shim that makes an unpinned
  `Screen` request.

### 3. What sits behind a menu
Per kind: `Modal` and `Screen` dim the scene and hide the HUD's non-essential lines; `Panel` keeps the HUD. A rule
table, not code in each view.

### 4. One cursor owner
- `ICursorPolicy` (Core.UI) is the only writer of `Cursor.lockState` / `Cursor.visible`. It derives the state from
  requests: a menu or panel is open → free; possessed and nothing open → locked; the shipyard while looking → locked;
  the free-camera toggle → its request.
- An architecture test fails on any other write to `Cursor`.

### 5. A theme instead of inline styles
One USS stylesheet with tokens (colours, spacing, font sizes) loaded on every layer; the views use classes. The look
stays a placeholder; the point is that a real UI pass later changes one file. UXML only for hand-made screens; generated
rows stay code-built.

### 6. Keyboard and gamepad navigation (later)
Focus order and Submit/Cancel through the Menu input context. Needed for gamepad play; not part of the first slices.

## Testing
- **Model (EditMode, no package):** arbitration (two openers, kinds, pinned, Esc, Back stays inside a request), the
  cursor policy, the layer rules for what hides behind what. Each conflict listed under Today becomes a test first.
- **Views (EditMode, UI Toolkit test framework):** `com.unity.ui.test-framework` (6.4 docs list `RuntimePanelSimulator`
  and `RuntimeUITestFixture`; the version matching Unity 6000.4 to check at install) renders a view into a simulated
  panel with clicks, typing and frame updates. Tests: the menu view draws the snapshot's rows; a click invokes the row;
  a slider and a choice write their value (for [player-settings.md](player-settings.md)); a `Modal` covers a `Panel`;
  the HUD dims under a `Screen`.
- **Architecture tests:** no `UIDocument`, `sortingOrder` or `Cursor` writes outside the UI core; no `OnGUI` outside
  `DevTools/Editor`.
- **Scenarios:** a `UiProbes` scenario library answers "top request", "HUD visible", "cursor locked" on each peer.
  Steps are added to `OverboardRespawn` (pause while overboard) and a voyage scenario (lose while overboard: end
  screen on top, overboard choice still reachable after it).
- **A look by eye:** after each slice, a Game view capture of the main menu, the pause menu over the HUD, the shipyard
  panel with its menu, and the overboard choice.

## Slices
0. **U0 Repro list:** the developer's examples of the layering bugs (a screenshot or a sentence each), turned into
   failing tests or scenario steps. No code yet.
1. **U1 Layers and the cursor:** `UiLayer`, `IUiLayers`, the cursor policy, the shipyard panel and the dev overlay
   moved, the architecture tests.
2. **U2 Menu requests:** the arbitration processor, per-request stacks, pinned requests, the seven openers moved, the
   HUD rules.
3. **U3 View tests:** the test framework package (after a yes), view tests for the menu and HUD views.
4. **U4 Theme:** the USS tokens, views on classes.
5. **U5 Navigation:** keyboard and gamepad focus.

Settings ([player-settings.md](player-settings.md) S2), pings ([crew-pings-and-markers.md](crew-pings-and-markers.md) S1)
and hosting ([hosting-and-joining.md](hosting-and-joining.md)) build their screens on U1 and U2.

## Decisions (developer, 2026-10-31)
1. **One `UIDocument` with a container per layer.** Draw order is the order of the children, so no sort numbers can
   collide (the bug class this plan fixes). Documents on one panel settings asset already share a panel, so separate
   documents isolate less than they seem; one panel is also one simulated panel in tests. A feature hides its element
   instead of switching off a document. World-space UI (the attribute bars) stays separate.
2. **Esc while a pinned modal is up opens the pause menu over it.** Quit and Settings are always reachable.
3. **Add `com.unity.ui.test-framework`** (test assemblies only; nothing ships in the player). Check the version that
   matches Unity 6000.4 at install. If it proves flaky, fall back to model tests and hand-built panels.
4. **U0 from the code (2026-10-31, revised):** the developer chose to start with the conflicts listed under Today as
   the repro list instead of waiting for screenshots; examples seen in play can still be added as tests later.

Open: a UI art direction. Until there is one, U4 stays a token sheet with the current grey look.

## Progress
**U0, U1, U2 done (2026-11-01).** What was built and where it differs from the design above:
- Layers: `Core/UI/UiLayer.cs`, `IUiLayers.cs`, `UiLayers.cs` (one document, made on first use; panel settings on
  `UiFeatureInstaller`). The menu and HUD overlays, the shipyard panels and the net harness overlay draw into it; the
  `UIDocument`s are gone from the `GameLifetimeScope` prefab and `ShipyardConfig.PanelSettings` is gone.
- Menu requests: `MenuRequest`, `MenuKind`, `MenuArbitrationProcessor`, per-request stacks in `MenuUseCase`;
  `MenuContext.Close()` closes the row's own request. All seven openers and the closing commands moved. `Open` is the
  shim; `CloseAll` now closes every unpinned request. Esc on a pinned modal: `MenuBackInputHandler` requests the pause
  menu as a `Modal` (the newest modal wins), so it sits over the pinned one, not under it.
- Backdrop rules (`MenuBackdropRules`): differs from section 3. `Screen` dims and hides the HUD; `Modal` dims and
  **keeps** the HUD, because the HUD has no "essential" marking and the overboard countdown and the voyage line belong
  with those choices. The dim backdrop also takes the pointer, so nothing under a menu can be clicked.
- Cursor: `ICursorPolicy` / `CursorPolicyUseCase` / `CursorPolicyProcessor`, writing only through `UnityCursorDevice`
  and only on a change. The enum is `CursorState` (Unity already has a `CursorMode`). The policy learns about menus
  from `MenuCursorPresenter` rather than taking `IMenuSystem`: the direct dependency made a resolve cycle through the
  shipyard (commands → shipyard → stage → cursor), which `FeatureProfiles_CanBuildTheirServices` cannot see (it
  registers, never builds); only the shipyard scenarios caught it. `PossessionCursorResponder` was
  on no prefab and was deleted.
- Architecture rules: `ScreenUi_IsDrawnIntoTheLayers`, `TheCursor_HasOneWriter`, `OnGUI_OnlyInEditorCode` (any
  `Editor` folder, since property drawers in `Core/*/Editor` use `OnGUI`).
- Tests: the plan's conflicts are in `MenuArbitrationProcessorTests`; `CursorPolicyUseCaseTests`; overlay layer and HUD
  cases in `MenuOverlayViewTests` (a real panel, no click simulation yet: that is U3).
- Scenarios: `UiScenarioLibrary` (`TopMenu`, `HudShown`, `HudHidden`, `CursorLocked`, `CursorFree`) in every scenario;
  Esc steps in `OverboardRespawn` and `VoyageLoop`. "Lose while overboard" is an EditMode test only: no test scene
  loads both Respawn and Voyage.

**U3 done (2026-11-01, with player-settings S2).** `com.unity.ui.test-framework` 6.4.0 is a built-in package for
Unity 6000.4 (not on the public registry), referenced by the EditMode test assembly only (`Unity.UI.TestFramework.Runtime`,
`.Editor`). `MenuOverlayViewClickTests : UITestFixture` clicks rows, the choice arrows and the slider in a simulated
panel, and checks that a menu's backdrop keeps clicks from a panel underneath. It found a real bug at once: the
choice arrows stepped from the value the row was built with.

**U4 done (2026-11-01).** `Assets/UI/Theme/TinCanTheme.uss`: tokens on `.tc-root` (surfaces, text, accents and
states, spacing, radii, font sizes) and classes that use only tokens, loaded by `UiLayers` from `UiFeatureInstaller`.
The menu, HUD, shipyard panels and the dev readout use classes; nothing sets a colour or font size in code
(`ScreenUi_TakesItsLookFromTheTheme`). Text inputs are a dark well with light text (they were the button colour with
white text). The HUD meter is three bands (good, fair, bad) instead of a red-to-green blend, so the colours are
tokens. Tests: `UiThemeTests`. Still a grey placeholder look: there is no art direction yet.

Next: U5 (keyboard and gamepad navigation).

## Verification
- EditMode suite green, plus the new model, view and architecture tests.
- `verify.ps1 -Scenario OverboardRespawn` and the voyage scenario with the new steps, solo and host + client (asked for
  first).
- Human: the captures above, Esc in every state, the cursor in the shipyard, at a station, after a menu closes.
