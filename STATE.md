# State: 2026-11-10, stages 1–5 implemented (static compile only)

**Done this pass** (details in `HANDOFF.md`)
- Bug batch 0,a–h committed (`2aea44b`): menu body layer/culling, camera obstruction filtering, reverse gear HUD,
  gate waypoint, HUD contrast, guard position/collider, back fence + solid barrier arm, opaque speed sign,
  shadow/LOD tuning. Compile clean; Play Mode not run (no editor here).
- Stage 1 real distance (`0bc4db0`): Amazon yard/gate/props under a `Destination` root at the end of a highway
  exactly `DeadheadAssignedMiles × 1609.344 m` long. Odometer integrates actual metres; no compression.
- Stage 2 (`1ee9f44`): documented `LoadState` constants and rotation-aware trailer turning-sweep collision
  feeding `VehicleContact`.
- Stage 3: `MirrorView.cs` renders small rear-facing camera textures onto both west-coast mirror glasses
  (new script + meta). Backing lesson overlay tracks distance-to-kingpin, alignment, speed, contacts and
  pull-ups; scores contacts/alignment/stop offset on capture and offers RETRY (R key or button) with a full
  tractor/trailer reset (`ResetForBacking` / `ResetForRetry`).
- Stage 4: end-of-shift debrief records Safety, Procedure and Efficiency separately. Safety holds override
  speed/efficiency; company-standard line is marked `PLACEHOLDER: confirm policy`.
- Stage 5: sensitivity persists in `PlayerPrefs` (`D2D.MouseSensitivity`) with pause-menu +/- controls.
  Pause dim no longer swallows clicks; `CaptureCursor` refuses to lock while a UI lock/menu is active.
- Compile: `COMPILE OK` after all stages. Play Mode unverified.

**Known risks**
- Mirror camera orientation/offset is unverified visually; two extra low-res cameras may cost frame rate.
- 60 mi (~96.5 km) is a genuine long drive; highway uses one giant collider and tiled material.
- Backing lesson starts automatically within 45 m of A-14 while seated; retry resets coupling and requires a
  fresh hook-up. Untested in Play Mode.
- Sensitivity slider is +/- buttons, not a continuous slider.

**Exact next step.** Re-review stage 3–5 changes for regressions, then run `compile_check.sh` and commit/push
the remaining stage 3–5 work.
