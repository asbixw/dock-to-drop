# State: 2026-11-12, sleeper start and journey skip work; full autoplay shift passes

**Committed (`5daf6ca`, pushed)**: floating-origin audit fixes.

**Uncommitted (built, autoplay verified)**
- Day starts on the sleeper bunk at the truck stop. Truck at (0, 0.074, 8) facing north in the clear lot lane. `PlayerController.EnterBunk` / `InBunk`; X climbs to the driver's seat (`Truck.Enter(false)`). The house is gone; trailer A-14 is back in its yard slot.
- Journey skip (`GameFlow.JourneySkip`): 300 m up the road (point A) the screen fades, three road shots play with an A-to-B route strip (Enter skips), and control returns 600 m before Gate 1 (point B) at 30 mph. Odometer and shift clock are credited at posted speeds. `-fulldrive` keeps the whole 32-mile drive.
- Autoplay: bunk → answer call → seat → step out → board by the door ray → drive. Yard heights use `Yard().y`.
- Earlier: route curves and grades at exact length, `RouteAimPoint`, `Yard()`.

**Runs**
- `logs/skip-autoplay.log`: `seated=True`, journey 05:41 → 06:14, `guardOpen=True`, 5/5 inspections, hook, `ended=True`, 0 contacts, 0 exceptions.
- `logs/sleeper-autoplay-fulldrive.log` (before the skip): 25 origin shifts, `guardOpen=True`, 31.96 mi, but two `CONTACT tractor collider=RouteAsphalt` on grades at 20x.

**Known issues**
- Full drive only: the bumper cast hits the road surface on grades.
- Skip scene: bare terrain, bars pop in, engine sound does not follow the shots.
- No pre-trip walkaround objective yet; truck HUD shows in the bunk; "Amazon" copy remains.
- `-autoshot` and `-uishot` not rerun since the start moved.

**Exact next step.** Play the opening by hand once (bunk, call, X, drive to point A, scene, gate). Then add the pre-trip walkaround as real objectives (tires, lights, air lines) between leaving the bunk and starting the engine.
