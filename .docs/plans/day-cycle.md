Status: Approved

# Day cycle: a low northern sun, the light that follows it, and the clouds judged under it

## The goal
The light of the north as autumn turns to winter: a sun that rises in the south-east, skims low along the southern
horizon and sets in the south-west, with long golden light all day, a long blue hour, and real darkness. The light
and the sky follow the sun. Under that sky, the current (volumetric) and the new (stylized mesh) clouds are compared
(developer, 2026-11-14). This is L3 of [`look-slice.md`](look-slice.md) (the painted sky), grown into a moving day.

## Decisions (developer, 2026-11-14)
- **The clock is headless, with pluggable drivers.** It knows nothing about rendering: a driver turns seconds into an
  hour of the day, and the sun's path turns the hour into a sun position. Drivers swap without touching the clock, so
  several can be tried (a fixed cycle, a fixed hour; later the voyage, synced network time, weather).
- **First driver: a fixed cycle**, short (5 minutes a day) to get a feel for it.
- **Darkness is in**: the day runs through the blue hour into night.
- **Lab first**: the cloud lab gets the moving day and a comparison sheet; the game comes after the picks.

## Parts
- **`TinCan.Features.DayCycle`** (`Assets/Scripts/Features/DayCycle/`), headless: `IDayClockDriver` (seconds to hour),
  `FixedCycleDayDriver`, `FixedHourDayDriver`; `SunPath` (latitude, declination, which way is north) and
  `SunPathProcessor` (the solar position formula; the hour the sun reaches an elevation); `DayClock` (a driver and a
  path: seconds to `SunPosition`); `DayCycleConfig` (the driver, picked in the Inspector, and the path).
  Defaults: latitude 63 N, declination -15 (early November): the sun peaks at about 12 degrees, rises about 08:00,
  sets about 16:00.
- **Atmosphere: the light by the sun's height.** `DayLightKey` (sun colour and strength, ambient, fog toward and away
  from the sun, the sky's zenith, the pink band opposite a low sun (the belt of Venus), and how far fixed-colour
  surfaces darken at night), a list of them on the `LookProfile` (`Day`; empty, the look stays static), and
  `DayLightProcessor` (blend the keys by elevation, lay the result over the look). Keys are built from the palette by
  **TinCan > Dev > Look > Build Atmosphere**, only when the list is empty.
- **The painted sky**, `Shaders/PaintedSky.shader` (`TinCan/Environment/Painted Sky`): zenith to the air at the
  horizon, the belt of Venus opposite a low sun, a sun disc with a soft halo; colours from `SkyShaderGlobals`. Replaces
  the lab's stand-in sky.
- **Night on the stylized clouds:** their shadow colour is fixed, so they would glow at night. `_TinCanNightShade`
  (in `TinCanAtmosphere.hlsl`, 0 by day and when unset) darkens them toward a tint.
- **The lab:** while `Lab_Clouds` is open in edit mode, `CloudLabDay` runs the config's driver and applies the sun,
  light, air and sky each editor frame. Pick **Fixed Hour** on the config to stop and scrub.
- **The sheet:** **TinCan > Dev > Look > Day Sheet** (`DaySheet.cs`) renders `Logs/look/day.png`: columns are moments
  of the day (midnight, blue hour, sunrise, morning, noon, sunset, blue hour), rows the volumetric clouds, the
  stylized clouds, and the view away from the sun.

## Steps
1. **D1 (this step, lab):** the parts above.
2. **D2 (game):** an installer for the clock (driver from synced network time on every peer, nothing replicated),
   the Atmosphere presenter taking the sun and the day's light from it, the painted sky in the play scenes, a
   scenario. After the developer's picks from the sheet.
3. **Later:** more drivers (the voyage: dawn at cast-off), a moon and stars for the night, the weather owning the
   light ([`cloud-sea.md`](cloud-sea.md)).

## Open
- The keys' colours, the sun path numbers, the cycle length: from the sheet and the running lab.
- Which cloud system wins under the moving sun.
