# State: 2026-11-10, stage 1 done + stage 2 (load handling / trailer sweep)

**Done this pass** (details in `HANDOFF.md`)
- Bug batch 0,a–h committed (`2aea44b`): menu body layer/culling, camera obstruction filtering, reverse gear HUD,
  gate waypoint, HUD contrast, guard position/collider, back fence + solid barrier arm, opaque speed sign,
  shadow/LOD tuning. Compile clean; Play Mode not run (no editor here).
- Stage 1 real distance committed (`0bc4db0`): Amazon yard/gate/props under a `Destination` root at the end of a
  highway exactly `DeadheadAssignedMiles × 1609.344 m` long. Odometer integrates actual metres; no compression.
  "Training slice" wording removed. Shift end, autoplay and autoshot positions are destination-relative.
- Stage 2: `TruckController` now has a documented `LoadState` enum and named bobtail/loaded constants
  (mass, accel, reverse accel, service brake, rolling/aero drag, engine-brake factor). `TrailerRig.Follow`
  samples both translation and rotation of the body between poses, so the trailer cannot clip a fence/post
  between frames; blocked trailer sweeps raise the existing `VehicleContact` bus and feed the shift log.
- Compile: `COMPILE OK` after stage 2. Play Mode unverified.

**Known risks**
- 60 mi (~96.5 km) is a genuine long drive. Highway uses one giant collider and tiled material.
- Sweep sampling is discrete; a very fast frame could still cut a corner between samples. No Play Mode test.
- Load feel (accel/brake tuning) has not been driven by a human.

**Exact next step.** Stage 3: usable mirrors/rear view and the backing lesson with contacts, alignment,
stop distance, pull-ups and retry.
