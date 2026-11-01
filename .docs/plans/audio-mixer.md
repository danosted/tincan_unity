Status: Approved (2026-10-31); third in [core-systems-roadmap.md](core-systems-roadmap.md)

# Audio: a mixer, channels, a pooled one-shot player and volume settings

## Why
Sound is about to grow: the cues need a proper sound pass, the cloud sea wants wind and ambience, and ship parts want
their own loops. Today nothing can be balanced or turned down, because there is no mixer and sounds are fired without
a route. Doing it now, while there are four clips, is cheap.

## Today (verified 2026-10-31)
- Four placeholder clips in `Assets/Audio/Cues/`; two looping cue prefabs (`CueFX_HissLoop`, `CueFX_RepairLoop`) with
  an `AudioSource`. No `.mixer` asset.
- One-shots go through `GameplayCuePresenter.PlaySound` → `AudioSource.PlayClipAtPoint`. That builds and destroys a
  GameObject per sound (an allocation on every sound) and cannot set a mixer group.
- `PlaySoundCueAction` and `SpawnPrefabCueAction` (`Core/Gas/Cues/Actions/`) are the only ways gameplay makes sound.
- The listener is added at runtime to the possessed camera pivot (`LookView.EnsureAudioListener`); a station's camera
  turns it off and on (`StationViewPresenter`). A client that joins while a sound plays warns for about a second
  (accepted, `gameplay-cues.md`).

## Design

### Mixer
- `Assets/Audio/TinCanMixer.mixer`: `Master` → `World` (gameplay sound in 3D), `Ui` (menu, HUD toasts), `Ambience`
  (wind, cloud beds), `Music` (empty for now), `Voice` (empty; reserved for voice chat if it ever comes).
- Each group's volume is an exposed parameter (`Vol_Master`, `Vol_World`, ...). Snapshots (a muffled "overboard" or
  "menu open" mix) are a later slice.

### Channels and the one-shot player
- `AudioChannel` enum in `Core/Domain/Audio/` (`World`, `Ui`, `Ambience`, `Music`, `Voice`).
- `IAudioPlayer` (Core.Domain): `PlayOneShot(AudioClip, AudioChannel, Vector3? position, float volume, float
  pitchJitter)`. No position means 2D.
- New core assembly `TinCan.Audio` with `AudioFeatureInstaller` (`Profile_Base`, `Profile_Test_Core`) and
  `AudioConfig` (the mixer, the group for each channel, pool size, 3D rolloff defaults).
- `AudioOneShotPool` implements `IAudioPlayer`: a fixed pool of `AudioSource`s under one root, routed to the
  channel's group; the oldest voice is stolen when it is full. No allocation per sound.
- `PlaySoundCueAction` gets a `Channel` field (default `World`); `GameplayCuePresenter.PlaySound` calls `IAudioPlayer`.
  The looping cue prefabs get their output group set in the asset (an Editor operation).
- Contract for features: a feature plays sound through `IAudioPlayer` or a cue, and puts an `AudioSource` in its own
  prefab only for loops, with an output group set. An EditMode architecture test can check that every prefab
  `AudioSource` has an output group.

### Volume settings
- `AudioVolumeBinder` (in `TinCan.Audio`) reads `audio.master`, `audio.world`, `audio.ui`, `audio.ambience`,
  `audio.music` from `IPlayerSettings` ([player-settings.md](player-settings.md)) and sets the exposed parameters.
- `VolumeToDecibelsProcessor` (pure, tested): linear 0..1 to dB on a log curve; 0 is -80 dB.
- Unity ignores `AudioMixer.SetFloat` during `Awake`; the binder applies in `Start`/first tick (to check against the
  installed version).
- Without the settings service the mixer keeps its authored levels.

### Headless
A dedicated server and batch-mode bots play nothing: the installer registers a silent `IAudioPlayer` in batch mode.

## Slices
1. **S1 Mixer and player:** the mixer asset, `AudioChannel`, `IAudioPlayer`, the pool, the cue action channel, the
   loop prefabs routed, the silent player in batch mode. Tests: pool reuse and voice stealing, channel to group lookup,
   the batch-mode switch.
2. **S2 Volumes:** the binder and the processor, the Audio settings page. Needs player-settings S1 and S2.
3. **Later:** snapshots (overboard muffle, menu duck), an ambience bed for the cloud sea, footsteps on deck.

## Decisions (developer, 2026-10-31: the recommended options)
1. **The group list as above.** A machinery group under `World` waits until there are machinery sounds to duck.
2. **One rolloff curve for every `World` sound**, set in `AudioConfig`. A cue can get its own curve when one needs it.

## Verification
- EditMode suite green, plus the new tests and the prefab output-group check.
- `verify.ps1 -UpTo Tests`; one play run of `RepairLoop` (it plays a loop and one-shots on host and client) to check
  that the cue probes still pass.
- Human: listen to the cues, move each volume slider, check that 0 is silent and that MPPM Player 2 has its own levels.
