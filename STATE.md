# State: 2026-11-11, project audit after stage N (compiled, Play Mode unverified)

**Where the code is**
- Stage N steps 0-6, playtest #3 fixes and the gate kiosk are committed on `animation-env-polish`.
- Audit pass (this session, uncommitted): fixed three floating-origin bugs that would have broken the drive after 2-3 km.
  - Road colliders: every chunk transform sits at the route origin, so the cull switched all of them off once the truck was 3 km out. It now measures to each chunk's mesh centre and runs twice a second.
  - Seated player: a child of the truck seat, so it was shifted twice on every recentre. Only an on-foot player is shifted now.
  - Cached world positions (`TrailerRig.lastAxle`, parked pose, chase/third-person camera points, `DriverBody.lastPos`, `backingStartPos`) now follow the shift through `FloatingOrigin.OnShift`.
- Also: the shift-end check is relative to the yard on x as well as z; route end no longer snaps sideways in its last 25 m (integer division); dead code and two deprecated calls removed; README corrected.
- `COMPILE OK` (`logs/audit-compile.log`), scripts mirrored byte-identical.

**Known risks**
- `WorldBuilder.BuildRoute` uses `Mathf.SmoothStep` as if it were GLSL `smoothstep`, so the curves and grades are about 5% of the intended size. Not changed: fixing it reshapes the whole drive.
- Autoplay steers straight at the gate and caches the yard offset before driving, so it cannot follow a curved road or survive an origin shift.
- Review list from the audit is in `HANDOFF.md` under "2026-11-11: audit".

**Exact next step.** `./prepare_unity.sh && ./build_mac.sh`, then drive at least 5 km of the deadhead by hand and check the log for `D2D ORIGIN shift=`: the truck must stay on the road, the driver in the seat, and the trailer (if hooked) behind the tractor.
