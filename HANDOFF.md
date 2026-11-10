# Handoff: 2026-11-07 trailer coupling pass

Backups of the files as they were before coupling (plus the cab-pass HANDOFF/STATE) are in `backups/2026-11-07-coupling/`.
`TrailerRig.cs`, `AirLines.cs` and `unity-assets/make_coupling_audio.py` are new files with no backup.

- Compile clean (`COMPILE OK`); no Play Mode available on this box.
- Final pass (`dae53d5` + self-checks): low-fps odometer step allowance, 1 km delineator spacing, stale
  "training slice" copy removed, route-length mismatch error, mirror shader/renderer warnings, debrief log.

## 2026-11-10: stages 3–5 — mirrors/backing lesson, debrief, pause/sensitivity

- **`MirrorView.cs`** (new): each west-coast main mirror gets a 192×384 rear-facing camera that renders every
  third frame to a texture on the glass, offset outward per side and flipped horizontally. The camera hides
  `DriverBody` (layer 8) and is disabled except for the explicit `Render()` call.
- **Backing lesson** (`GameFlow` + `TruckController` + `TrailerRig` + `AirLines`): automatic when seated within
  45 m of A-14 after the gate opens. Overlay shows distance-to-kingpin, alignment angle, speed, contacts and
  pull-ups. On kingpin capture it scores contacts, lateral/angle alignment and stop offset, marks
  `backingPassed`, and offers RETRY (R key or button). Retry calls `TruckController.ResetForBacking` and
  `TrailerRig.ResetForRetry` (spring brakes, gear down, hoses stowed, trailer restored under `Destination`).
- **Debrief** (`GameFlow.BuildDebrief`): Safety, Procedure and Efficiency are separate lines; contact/hard-brake/
  engine-running-exit/backing-contact events force a `SAFETY HOLD` regardless of efficiency. Company-standard
  line is `// PLACEHOLDER: confirm policy`.
- **Pause/sensitivity**: `PlayerController.SetSensitivity` persists `D2D.MouseSensitivity` in `PlayerPrefs`;
  pause menu has +/- buttons. The pause dim no longer blocks clicks, and `CaptureCursor` will not lock while
  `uiLock`/`menuView` is set.
- Compile clean (`COMPILE OK`); no Play Mode available on this box.

## 2026-11-10: stage 2 — load handling + trailer turning sweep

- **`TruckController`**: added `LoadState` (`Bobtail`/`Loaded`) and named constants: `BobtailMass` 9 t /
  `LoadedMass` 33 t, accel caps 2.3/1.1 m/s², reverse caps 1.2/0.6, service brakes 6.8/5.0 m/s², rolling
  drag 0.07/0.09, aero terms 6.5/9.5 and loaded engine-brake factor 0.55. `State`, `Loaded` and `Mass` expose
  the model to the HUD/debrief; the physics update now uses these constants instead of inline literals.
- **`TrailerRig.Follow`**: the turning sweep now samples both the tandem translation (≤0.55 m steps) and the
  trailer heading rotation (≤3° steps), interpolating a trailer-space pose for each sample. Any overlap
  refuses the tractor's step and raises the existing `VehicleContact` event, so the shift log/debrief sees
  trailer sweeps, not just the final pose.
- Still no Play Mode on this box; only `COMPILE OK`. Load feel and sweep tolerance need a human drive.

## 2026-11-10: real deadhead distance (stage 1)

- **`WorldBuilder`**: `WorldRefs.destination` + `routeLength`. The Amazon yard, gate, warehouse, yard props,
  yard paving, reflection probe and yard/dock detail now build at their authored local coordinates under a
  `Destination` transform, which is then placed at `route = DeadheadAssignedMiles × 1609.344 m` from the truck.
  `BuildHighway` lays a tiled asphalt surface, shoulders, edge/centre lines, delineators and mile boards from
  z 217.5 to `destOffset + 430` (one box collider on the surface). Local yard ground, yard/gate decals and yard
  props were removed from the travel-center build so nothing floats on the new road.
- **`GameFlow`**: removed all "training slice" wording from objective, subtitles, HUD, debrief and engine-start
  line; they state the assigned miles. Shift-end check is relative to `World.destination` (`destZ + 216`,
  bounded below by `destZ + 150`). Autoshot and autoplay positions add the destination offset.
- **Unverified**: Play Mode is impossible here (no editor, no `playtest.sh`). Static compile only
  (`COMPILE OK`). Milepost text and the single 96 km collider have not been seen in a running build.

## Done
- **`TrailerRig`** (on `Trailer_A14`): measures the kingpin, plate, axle, nose and crank from the FBX at start-up.
  For A-14 it logs kingpin (0, 1.07, -8.00), axleZ 5.71, crank (-1.12, 1.55, -5.10). When coupled, the tandem
  follows the kingpin along a tractrix. `Follow` returns false on a jackknife (>84°, but moves that unfold are
  allowed) or when the body would overlap a collider, and the tractor then undoes its step. It also has the
  landing-gear crank coroutine, a standing `NoseWall` (the FBX front wall lies flat and is hidden), glad hands,
  and body colliders raised above the coupler so the tractor can slide under.
- **`AirLines`**: three Bezier tubes rebuilt every frame. They hang stowed off the back of the cab until connected.
- **`TruckController`**: fifth wheel at local z -4.75, with its height set from the trailer plate. The hull cast
  is split into a tall cab cast and a low frame cast so the tractor can back under. Kingpin capture works
  within ±0.24 m and 12° (outside that it misses, with feedback). While the trailer brakes are set, the tractor
  only moves on kingpin slack (-4..+9 cm), and pulling for >1 s passes the tug test. Loaded mass is 33 t, with
  a low gear, softer brakes and more drag. The `FifthWheelLamp` lights green in the cluster.
- **`GameFlow`**: `RefreshObjective` covers every hook-up state, and inspection can happen before or after
  coupling. Interactables: `lines` (behind the cab, driver side), `crank` (on the trailer), and the ignition
  doubles as the red trailer-air valve. HUD line: BOBTAIL / TRAILER BRAKES SET / TRAILER AIR. The shift ends at
  z < 216 with the rig hooked, air supplied and the inspection done. Autoplay `AutoHook` and autoshot
  `CouplingShots` (09_hook_approach, 10_coupled, 11_rig_turn) are new.
- **World**: A-14 is rotated to yaw 0 (nose toward the gate), with yellow lead-in lines in front of the nose and
  chocks behind the tandem. Seal shot and autoplay seal stand are now in trailer space.
- **Audio**: `fifth_wheel_lock`, `glad_hand`, `crank_ratchet` and `trailer_air` are synthesised by
  `make_coupling_audio.py` and registered in `AudioManager`.
- **Fix**: the interrupted session wrote 9 `ShowSubtitle` literals in `GameFlow.cs` with raw newlines, which is
  error CS1010. The build had never been run on it. Replaced them with `\n` escapes.

## Verified
- `Result: Success`, 0 `error CS`. Autoplay hook: coupled in 8.4 s at 0.92 m/s with 0.12 m lateral offset, tug,
  lines, crank and air all through the real interactables, departed, max articulation 15.8°. Third-person run is
  identical. 0 exceptions, no `blocked by` lines. Autoshot turn reaches 31.5° articulation.

## Open (rank order, replaces the list below)
1. A human play-test of the hook-up, then tune the capture tolerance, the loaded acceleration (1.1 m/s² cap) and
   the tug feel.
2. Driver realism (unchanged, see the old #2).
3. Cab polish (unchanged, see the old #3).
4. Canopy livery and the box store (old #4). 5. Tyres and feet sink ~10 cm (old #5).
6. Only A-14 is hookable, and there's no un-hook. The NoseWall is plain and very bright in sun (frame 10).

---

# Handoff: 2026-11-06 cab interior pass

Backups of every file changed here, plus the previous HANDOFF/STATE (ground and parked-rig pass):
`backups/2026-11-06-cab-interior/`. Older ones: `backups/2026-11-06-ground-rigs/`, `backups/2026-11-06-sketchfab-props/`.

## Goal
Work through the parent brief in order: parked tractors, cab interior, ground, driver, frame rate, then the
Open list. Done so far: tractors, ground, vest, cab interior, frame-rate check. Keep `GameFlow.ActivateSelf()`,
the cursor-lock fix, muted automation and the red CC-BY Cascadia.

## Done (cab pass)
- **No third-party asset.** A Sketchfab interior was not used: fitting one into the Cascadia shell would cost
  more than shaping the parts, and downloads need the user's OK. Nothing to add to `THIRD_PARTY_NOTICES.md`.
- **Measured the shell** with headless Blender on the FBX (Unity x = Blender x, Unity y = Blender z, Unity
  z = Blender y). Eye (seat) is (-0.62, 2.55, 0.22). The roof slopes from y 3.13 at z 1.0 to 3.88 at z 0 (at
  x = ±0.8; lower near x = ±1.0). The mirror housings' rear faces are DarkTrim at x ±1.28..1.53,
  y 2.08..2.72, z 1.19..1.32; the left one is part of `DriverDoor`.
- **`MeshFactory.Extrude`** (new): extrudes a closed (z, y) profile along x, with split normals at sharp
  corners and ear-clipped caps.
- **`UpgradeCabInterior`** (rewritten middle section):
  dash in three x sections (driver -1.20..-0.22, centre ..0.38, passenger ..1.20), each a dark upper piece over
  a mid-grey lower piece. The driver section has a binnacle hood over a cluster face tilted 12.3°. `ClusterFrame`
  holds the cluster quad, a rocker-switch panel and two needle pivots, `TachNeedle` and `SpeedoNeedle`. The
  `CenterStack` frame is yawed 18° toward the driver, with screen, HVAC panel, knobs and the park-brake/
  trailer-air valves. `WheelPivot` (same name and axis, so `TruckController` still turns it) has a leather torus,
  an oval lathe airbag pad and four dished spokes with buttons. `ColumnFrame` (doesn't turn) has the shroud,
  column, boot and two stalks. `OverheadConsole` is an extrusion under the header (x ±0.85, kept 6 cm under the
  roof), with a textured door/CB face, dome lights and stowed visors. There's a dash cam on the glass (no
  rear-view mirror: sleeper cabs have none), dash vents, a glovebox, and mirror glass quads (metallic 1,
  smoothness 0.97; the left ones are re-parented to the door).
  Removed: box dash, floating RELAY tablet and text, torus wheel with grey hub and chrome horn, rear-view mirror,
  old overhead box and LEDs.
- **`TruckController.UpdateVisuals`** drives the needles: tach 0–3000 rpm, speedo 0–90 mph, 240° sweep.
- **`make_cab_textures.py`** (new, PIL + numpy) writes `cab_cluster`, `cab_screen`, `cab_hvac`, `cab_switch`,
  `cab_overhead` and `cab_grain_nor` (tileable plastic grain, auto-imported as a normal map through `_nor`).
- `TruckInterior` shell material: smoothness 0.30 → 0.12 (it showed white edge highlights), slightly lighter.
- One correction pass: interior albedos raised (the first values were near-black in linear space), satin trim
  toned down, the pad badge made matte, and panel quads set to receive shadows.

## Verified
- Build clean (`Result: Success`, 0 `error CS`). `-autoplay` and `-autoplay -thirdperson` both end
  `D2D PLAY END ended=True sealPhoto=True inspected=5`, 0 exceptions.
- Sheets: before `logs/cab_sheet_before.png`, pass 1 `logs/cab_sheet_pass1.png`, final
  `logs/cab_sheet_final.png`, textures `logs/cab_tex_sheet.png`.
- **Frame rate:** `-perfprobe -screen-fullscreen 1` at 2560×1664, focused, Blender idle
  (`logs/cab_perfprobe.log`): every step reads 58.6–60 fps at render scale 0.75, until the window lost focus
  near the end. The automation window is 1500×448 because that size is saved in the game's prefs. The prefs
  were exported before the probe and restored after it.

## Open (rank order)
1. **Trailer coupling** (the old #5). Then a human play-test of how it feels.
2. **Driver realism** (brief item 4): a higher-detail CC0/CC-BY character, and real hands in the cab (the
   mitten hands come from the driver model, `DriverBody.cs`).
3. **Cab polish, still open:**
   - The mirrors reflect the probe, not the scene. A low-res mirror camera (about 192×384, every other frame)
     would show the trailer, which matters for backing.
   - The walls and sleeper are the decimated dark shell with white edge lines (`03c`, `03f`).
   - The bunk renders as a black box, and the seats are boxes.
   - The visor front edges catch the sun as two light strips.
   - Red paint shows at the windshield and door-opening edges from inside (no rubber seals).
4. The canopy fascia's pink/teal stripe looks like a chain's livery. Recolour it in
   `build_props_sketchfab_rigs.py`. The store is still a box.
5. Minor: the player's truck and the player sit on the `Ground` collider (y -0.02), so tyres and feet sink
   ~10 cm into the lot visually. The lot and yard boxes have no colliders.

## Commands
```bash
./prepare_unity.sh && ./build_mac.sh
"unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" -autoplay [-thirdperson] -logFile "$PWD/logs/autoplay.log" > /dev/null
"unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" -autoshot -logFile "$PWD/logs/autoshot.log" > /dev/null
```
Autoshot frames land in `~/Library/Application Support/Dock to Drop/Dock to Drop/`.

Cab textures: `python3 unity-assets/make_cab_textures.py`. Focused full-size frame-rate check (export the
prefs first, because Unity saves the fullscreen size, and restore them afterwards):
```bash
defaults export com.Dock-to-Drop.Dock-to-Drop /tmp/d2d_prefs.plist
"unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" -perfprobe -screen-fullscreen 1 -screen-width 2560 -screen-height 1664 -logFile "$PWD/logs/perfprobe.log" > /dev/null
defaults import com.Dock-to-Drop.Dock-to-Drop /tmp/d2d_prefs.plist
```
