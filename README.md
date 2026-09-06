# TinCan - Co-op Multiplayer FPS

TinCan is a Unity co-op airship game built with Netcode for GameObjects (NGO) and VContainer for dependency
injection. It is developed with heavy AI assistance: AI agents write much of the code, humans guide the
architecture, review, and playtest.

## Quick start

1. **Set up** (Windows, from the repo root; installs PowerShell 7, the Unity CLI and the pinned Editor):
   ```powershell
   .\.tools\setup.cmd
   ```
   Details and the upgrade script are in [`.tools/README.md`](.tools/README.md).
2. **Open** this folder as a Unity project. Run `git lfs install` once if you have not.
3. **Play it in 60 seconds:** open `Assets/Scenes/drm_cloud_environment.unity`, press Play, click **Start Host**
   in the menu. "No cameras rendering" before that is expected; the camera lives on the player prefab. For a
   second player use Multiplayer Play Mode and **Join** with `127.0.0.1`.

## Where to read next

- **Humans start at [`.docs/README.md`](.docs/README.md).** It has the 60-second mental model, a code map,
  task recipes, a build-a-feature tutorial, and the daily commands.
- **AI assistants start at [`AGENTS.md`](AGENTS.md).** It points at the same docs plus the rules AI must follow
  here. Vendor files such as `.github/copilot-instructions.md` only point to it.
