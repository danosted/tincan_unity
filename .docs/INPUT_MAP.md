# Input map

Generated from the input assets by **TinCan > Dev > Input > Write Input Map**. Do not edit it; rerun the menu
item after changing an action, a context, a route or a handler (`InputMapTests` fails while this file is stale).
The model is in [INPUT.md](INPUT.md).

## Contexts

Highest priority first. Every frame each context whose condition holds is live unless a live context above
silences it; an action outside every live context reads as released.

| Priority | Context | Live when | Silences | Read by | From |
|---|---|---|---|---|---|
| 1000 | Context_Rebinding | waiting for a key | everything below | (routes only) | core (InputConfig) |
| 950 | Context_DevTools | always | - | (routes only) | NetTestHarnessFeatureInstaller |
| 900 | Context_Menu | a menu is open | everything below | (routes only) | core (InputConfig) |
| 400 | Context_Gunner | possessed actor has State.Occupying.Cannon | Context_Humanoid, Context_Camera | GunnerAimUseCase | CannonFeatureInstaller |
| 400 | Context_Helmsman | possessed actor has State.Occupying.Helm | Context_Humanoid | HelmInputUseCase | HelmFeatureInstaller |
| 300 | Context_FreeCamera | possessing Other | - | FreeCameraMovementUseCase | FreeCameraFeatureInstaller |
| 200 | Context_Humanoid | possessing Humanoid | - | HumanoidMovementUseCase, TargetOutlinePresenter | core (InputConfig) |
| 100 | Context_Camera | always | - | FreeCameraMovementUseCase, PlayerLookUseCase | core (InputConfig) |
| 0 | Context_Global | always | - | - | core (InputConfig) |

### Context_Rebinding

The Controls menu waits for a key: every action is silent until it has one.


### Context_DevTools

Only while the net harness runs (contributed by its installer): F3 toggles the readout.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| DevTools/ToggleNetOverlay | `f3` | Show or hide the net harness readout. | no |

| Pressing | Runs | Handled by |
|---|---|---|
| DevTools/ToggleNetOverlay | Command_ToggleNetOverlay | ToggleNetOverlayInputHandler |

### Context_Menu

A menu is open: everything below is silent; Cancel steps back.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| Global/Cancel | `escape` | Back out: close the menu, or open the main menu. | yes |

| Pressing | Runs | Handled by |
|---|---|---|
| Global/Cancel | Command_MenuBack | MenuBackInputHandler |

### Context_Gunner

Manning a cannon (State.Occupying.Cannon). Silences walking and looking: the mouse swings the barrel (GunnerAimUseCase). Fire and Leave are the Primary and Interact ability bits.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| Gunner/Aim | `mouse delta` | Swing the cannon's barrel. | no |
| Gunner/Fire | `mouse leftButton` | Fire the cannon you are manning. | yes |
| Gunner/Leave | `e` | Step away from the cannon. | yes |

### Context_Helmsman

At the helm (State.Occupying.Helm). Silences walking but not looking: the helmsman looks around while steering. The axes travel in the predicted input (HelmInputUseCase); Leave is the Interact ability bit.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| Airship/Throttle | `s` / `w` | Speed up or slow down the airship. | yes |
| Airship/Yaw | `a` / `d` | Turn the airship. | yes |
| Airship/Pitch | `space` / `leftShift` | Nose the airship up or down. | yes |
| Airship/Leave | `e` | Let go of the helm. | yes |

### Context_FreeCamera

Flying the spectator camera. Read by FreeCameraMovementUseCase; Cancel frees the cursor.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| FreeCamera/Move | `w` / `s` / `a` / `d` | Fly the free camera (spectator, a dev tool: not in the Controls menu). | no |
| Global/Cancel | `escape` | Back out: close the menu, or open the main menu. | yes |

| Pressing | Runs | Handled by |
|---|---|---|
| Global/Cancel | Command_ToggleCursor | ToggleCursorInputHandler |

### Context_Humanoid

On foot, in your own body. Read by HumanoidMovementUseCase; Sprint, Interact and Primary are also ability bits.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| Humanoid/Move | `w` / `s` / `a` / `d` | Walk. | yes |
| Humanoid/Jump | `space` | Jump. | yes |
| Humanoid/Sprint | `leftShift` | Run while held. | yes |
| Humanoid/Interact | `e` | Use what you look at: pick up, pour, man a cannon. | yes |
| Humanoid/Primary | `mouse leftButton` | Use the held item or ability. | yes |
| Humanoid/Secondary | `mouse rightButton` | Secondary use of the held item. | yes |

### Context_Camera

Mouse look for whatever you control. Blocked by menus and by stations that aim with the mouse.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| Camera/Look | `mouse delta` | Turn the camera of whatever you control. | no |

### Context_Global

Always on, lowest: Cancel opens the main menu when nothing above wanted it; Tab switches control.

| Action | Default keys | Meaning | Rebindable |
|---|---|---|---|
| Global/Cancel | `escape` | Back out: close the menu, or open the main menu. | yes |
| Global/SwitchPossession | `tab` | Cycle to the next thing you may control. | yes |

| Pressing | Runs | Handled by |
|---|---|---|
| Global/Cancel | Command_OpenMenu | OpenMenuInputHandler |
| Global/SwitchPossession | Command_SwitchPossession | SwitchPossessionInputHandler |

## Ability inputs

Bits of the predicted input state, in the input config's order (the same on every peer). A bit is set while
any of its actions is pressed in a live context.

| Bit | Ability input | Pressed by |
|---|---|---|
| 0 | Input_Sprint | Humanoid/Sprint |
| 1 | Input_Primary | Humanoid/Primary, Gunner/Fire |
| 2 | Input_Interact | Humanoid/Interact, Gunner/Leave, Airship/Leave |
