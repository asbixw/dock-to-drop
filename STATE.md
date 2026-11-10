# State: 2026-11-11, MAC PLAYTEST #2 fixes applied (static compile only)

**Done this pass**
- Fix #1 (`85dc2be`): `MirrorView.Start` now tries `Unlit/Texture`, then `Standard`, and skips material creation if neither exists; `Unlit/Texture` (fileID 10752) added to GraphicsSettings Always Included Shaders.
- Fix #2 (`0c1632b`): perimeter tree loops skip `|x| < 14.5 m` when `z > 217.5`, keeping trees out of the deadhead lane/shoulder.
- Fix #3 (`fd164f4`): trip panel height 170 and ETA line moved to -118 so the assigned and ETA lines no longer overlap.
- Fix #4 (`021ecff`): waypoint targets offset laterally off A-14; screen clamp keeps the marker clear of the TAB PHONE hint.
- Fix #5 (`c54e297`): `DriverBody.Visible` always applies renderer state; CineShot re-enables body renderers after composed frames.
- Fix #6 (`6494f76`): `deadheadOnTime` requires `deadheadMiles >= assigned - 0.05` as well as the clock.
- Older playtest #1 (`d12a8b8`): autoplay deadhead now uses 20x time compression for a real 32/60-mile run, logs distance every 5 s real, no longer forces the gate/arrival; `-uishot` staging sets gatePassed, loadVerified, deadheadOnTime and hook flags.
- Compile `COMPILE OK`; scripts mirrored byte-identical; Play Mode still unverified on this box.

**Known risks**
- 20x autoplay may be physics-unstable at low frame rates; it is an automation path only.
- Mirror shader fileID 10752 inferred from a Unity material; needs a player build to confirm.
- `DriverBody.Visible` now writes renderer state on every set; small cost, no visual change expected.

**Exact next step.** Build on Mac and run the playtest again: mirrors, trip HUD, waypoint, driver mesh, debrief on-time, autoplay distance log.
