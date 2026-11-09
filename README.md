# Dock to Drop

**A freight shift, from the driver's seat.**

![Freightliner Cascadia, authored in Blender](unity-assets/Previews/truck_v4_hero.png)

> 05:40. Dallas, Texas. The lot is still wet from the night.  
> The trailer is at A-14. The seal is 482913.  
> Amazon DFW9 opens in twenty minutes.

This is not a game about driving fast. It is a game about doing a job properly.

You walk out to a Freightliner Cascadia in the dark, open the door, climb into a cab
that smells like diesel and cold coffee, answer dispatch, verify the load, and drive
north. You clear a guard gate, stage a 53-foot trailer in a yard built to real
dimensions, walk the five inspection points, photograph the seal, and send the proof.
Then the shift ends.

No cutscenes. No dramatic score. No floating waypoints. The tension comes from
procedure, weather, weight, and the small chance that something is wrong.

---

## The promise

A player should finish the shift and feel like they just worked — not like they just
watched a truck game.

That means the truck has to look like a truck up close. The cab has to feel enclosed,
used, and specific. The yard has to feel like a place where people actually work. The
voices have to sound like people on a radio, not assistants reading a script. The
controls have to disappear into the act of driving. And the whole thing has to run
smoothly enough that the player forgets they are playing a build.

This document is the bar. The current build is a playable vertical slice; the job is to
close the gap between the slice and the promise.

---

## Design pillars

**The truck is the set.**
The Cascadia is not a vehicle select screen. It is the room the player lives in for the
shift. The hood, the steps, the door handle, the seat, the dash, the bunk, the mirror
arms and the wheel are all part of the story. If any of them reads as a box, the game
loses.

**Procedure is the story.**
Every step has a reason: verify the load because a wrong seal is a real problem; check
the landing gear because a dropped trailer is a real problem; photograph the seal because
the proof is the point. Nothing is a minigame for its own sake.

**One continuous world.**
The travel center, the road, the gate, the yard and the trailer exist in one space. No
loading screens, no teleports, no disconnected scenes. The player should always know
where they came from and where they are going.

**Sound is half the picture.**
A diesel idle is not a hum. An air brake is not a hiss. A gate motor has weight. A
trailer door has a rattle. The title screen is silent. The world only makes the sounds
it would actually make.

**Human voices, human pace.**
Dispatch is a dispatcher. The guard is a guard. They interrupt, repeat themselves, and
talk over engine noise. They do not sound like a text-to-speech demo.

**Nothing cartoonish.**
No exaggerated proportions, no glossy plastic where rubber should be, no saturated
sunset where dawn should be, no bouncy camera, no UI that shouts. Restraint is the
style.

---

## The shift

### 1. The lot — 05:40

The player starts outside a Texas travel center. The asphalt is dark, the pumps are lit,
and the truck is parked under the canopy with the trailer already coupled. The first
objective is simple: walk to the driver door and open it.

The walk is the tutorial. The player learns the pace of the game, the scale of the
truck, and the sound of the lot before anything asks them to drive.

### 2. The cab — 05:42

The door swings open with a real latch. The player climbs the steps, settles into the
air seat, and the camera drops into the cab. The interior is dark, trimmed, and worn:
charcoal door cards, a padded dash, a cluster that glows, a wheel with a thick rim and a
column rake that feels like a truck, not a car.

The phone rings. Dispatch has the load.

### 3. Dispatch — 05:44

The call is a conversation, not a menu. The dispatcher gives the appointment, the
shipper, the seal number and the rate. The player can ask for a repeat. The details
matter later.

Then the player opens Relay, checks the load board, and confirms the seal and the
trailer number before turning the key.

### 4. The road — 05:50

The engine catches with a diesel rattle, settles into an idle, and the air pressure
builds. The player releases the brake, pulls out of the lot, and drives north. The road
is not a race track: it is a two-lane approach with signs, fences, poles and the first
light of the morning.

Dispatch assigns a 32- or 60-mile empty deadhead to DFW9. The odometer counts the path
actually driven, the trip panel shows miles remaining and an ETA, and the shift clock
advances at 55 mph. A 60-mile assignment simply takes half an hour longer. The 240-mile
loaded run to Houston is the next leg, not this approach, and yard moves after the gate
are not counted on the trip.

The truck has weight. The steering has play. The brakes are air brakes. The player feels
the trailer behind them.

### 5. Gate 1 — 06:25

Amazon DFW9 appears as a long low building behind a fence. The guard booth is lit. The
gate arm is down. The guard asks a check-in question: trailer number, seal, appointment,
or destination. The player answers. If they are wrong, the guard pushes back. If they
are right, the arm lifts and the yard opens.

### 6. The yard — 06:30

The yard is a working space: dock doors, parked trailers, cones, puddles, a staging
pylon marked A-14. The player pulls alongside the trailer, sets the brake, and shuts
down.

Then they climb out and walk.

### 7. The walk — 06:35

Five inspection points around the trailer:

- the seal on the rear doors,
- the door locks and rods,
- the landing gear,
- the tires and mud flaps,
- the under-ride guard.

Each point is a physical object with a prompt, not a floating icon. The player has to
stand in the right place and look at the right thing.

### 8. The proof — 06:40

The phone becomes a camera with a 3:4 viewfinder. The player frames the seal, captures
the photo, checks the preview, and sends it to dispatch. The shift ends on the proof
screen, not on a scoreboard.

---

## The truck

The hero asset is a **Freightliner Cascadia-style conventional tractor with a 72-inch
sleeper**. It is authored in Blender, exported as FBX, and assembled in Unity.

The exterior carries the details that make a truck read at gameplay distance and up
close:

- a crowned cab roof and a sleeper roof fairing,
- flush driver and passenger doors with real shut lines, handles and badges,
- wheel arches with fender lips, not cut boxes,
- a recessed grille, chrome bumper, air dam and Texas plate,
- swept headlamp pods with projector lenses, DRLs and turn signals,
- curved mirror arms, hood mirrors, air horns and marker lamps,
- open aluminum steps on visible brackets,
- fuel tank, battery box, DEF tank, tank straps and mud flaps,
- dual rear wheels with grooved tires, detailed rims and hubs,
- a fifth wheel, air lines and a rear frame that looks like it can carry a trailer.

The interior is a separate Unity build because the FBX deliberately carries no cab
geometry. It includes:

- a carpeted floor and transmission tunnel,
- driver and passenger air seats with pedestals, bolsters and headrests,
- a sleeper bunk with a mattress, pillow and curtain rail,
- a padded dash with a cluster, gauges, a center Relay screen and physical switches,
- a raked steering column and a three-spoke wheel,
- door cards, armrests, pull cups and window sills,
- a headliner, sun visors, A-pillar trim and a rear-view mirror.

The cab must pass the **look-around test**: from the driver's seat, in every direction,
with the door open and closed, the player sees a finished interior. No painted shell, no
cream slabs, no floating panels, no gaps where the trim should be.

---

## The world

**The travel center** is a real roadside place: canopy, pumps, diesel signs, a café
window, a shower block, parked cars, a wet lot. It is the last familiar place before the
highway.

**The approach** is a two-lane road with fences, poles, trees, signs and the sky
opening up. The drive is short enough to respect the player's time and long enough to
establish distance.

**Amazon DFW9** is a fulfillment center, not a warehouse prop: long concrete walls, dock
doors, trailer parking, lighting poles, a guard booth, a gate arm, fencing, a staging
pylon and the small human details that make a yard feel used.

**The trailer** is a 53-foot dry van with corrugated sides, rear doors, lock rods,
hinges, tires, landing gear, mud flaps, an under-ride guard, a Texas plate and a cable
seal. It is the object the player spends the most time inspecting, so it has to hold up
to a close look.

The world is lit as a single continuous dawn: low sun, long shadows, cool shade, warm
highlights, haze in the distance, and reflections that move as the player walks around
the truck.

---

## The phone

Relay is a modal phone, not a HUD panel. It opens in the player's hand, takes over the
screen, and pauses the world prompts while it is open.

It contains:

- the load board with the assignment,
- the dispatch chat and the incoming call,
- the proof-photo gallery,
- a 3:4 camera viewfinder for the seal photo,
- the seal preview and the send-to-dispatch flow.

The camera is the only time the game crops the world to a frame. That crop should feel
like a phone camera: slightly narrow, slightly imperfect, with the seal filling the
frame.

---

## Sound and voice

The sound design is deliberately sparse.

- **Title screen:** silence. No wind bed, no hum, no loop. The music starts with the shift.
- **Music:** a quiet, calm piano bed under the work. It fades in, loops seamlessly, and
  ducks under anyone speaking. It is never the point.
- **Engine:** a close-mic diesel idle with a real firing rhythm, a low-pass filter, and a
  fade-in when the key turns. Pitch rises with road speed and throttle. No buzz.
- **Air brake:** a short, dry release, not a cartoon hiss.
- **Door:** a latch and a hinge, with weight.
- **Footsteps:** real boot impacts on concrete and asphalt, with variation.
- **Gate:** a motor with a load, then the arm.
- **Camera:** a single shutter, no fanfare.
- **Voices:** dispatch, guard, receiver and coaching lines, pre-rendered with natural
  pacing and regional texture. They should sound like people on a radio, not a virtual
  assistant.

If a sound is not motivated by something on screen, it does not play. If a loop has a
click, a DC offset, or a constant low-frequency buzz, it is a bug.

---

## Controls

| Input | On foot | In the truck |
|---|---|---|
| **W A S D** | Walk | Throttle / brake / reverse / steer |
| **Mouse** | Look (orbits the camera in third person) | Look around the cab (orbits the chase camera) |
| **V** | Toggle third person (default) / first person | Toggle the seated cab view (default) / chase camera |
| **Shift** | Run | — |
| **E** | Interact with the highlighted object | Start the engine |
| **TAB** or **P** | Open / close the phone | Open / close the phone |
| **X** | — | Exit the cab (when stopped) |
| **Space** | — | Air brake |
| **H** | — | Horn |
| **F** | — | Headlights |
| **Esc** | Close the phone / release the mouse | Close the phone / release the mouse |
| **Right-click** | Release the mouse | Release the mouse |
| **Enter / Space** | Start the shift from the title screen | — |

Click the game window to capture the mouse. The HUD shows **CLICK TO CAPTURE MOUSE**
when the cursor is free. The game re-locks the cursor when the window regains focus.

---

## The quality bar

This is what “done” means. Do not call the game finished until every item is true.

- [ ] **The cab passes the look-around test.** From the seat, looking forward, left,
      right, up and down, with the driver door open and closed, there is no cream or
      white shell visible. The interior reads as dark, trimmed and finished.
- [ ] **The paint reads white.** In the dawn scene, the truck is white or off-white, not
      cream or beige. Shadows are cool, highlights are warm, and the truck does not look
      like it was lit by a single orange bulb.
- [ ] **The title screen is silent.** No wind, no hum, no buzz, no loop.
- [ ] **The engine sounds like a diesel.** It has firing rhythm, weight and a clean loop
      point. It does not buzz on laptop speakers or headphones.
- [ ] **The music stays under the work.** Calm piano, quiet on laptop speakers, no loop
      seam, and it ducks whenever dispatch or the guard speaks.
- [ ] **The voices sound human.** Dispatch and the guard have natural pacing, breath and
      interruption. No robotic TTS.
- [ ] **The controls work on a normal launch.** Click to capture, WASD, mouse look,
      interact, phone, drive, exit, Esc release, click recapture, Cmd-Tab refocus.
- [ ] **A full shift is playable without automation.** The player can complete every
      step from the title screen to the proof screen.
- [ ] **The full automated shift passes.** `-autoplay` ends with
      `D2D PLAY END ended=True sealPhoto=True inspected=5`.
- [ ] **The build is stable.** No exceptions, no missing references, no pink materials,
      no z-fighting, no floating props, no duplicate geometry.
- [ ] **The frame rate holds.** 60 fps at 1080p on Apple Silicon with shadows, MSAA and
      reflection probes enabled.
- [ ] **The screenshots are current.** Every image in this README comes from the final
      build.
- [ ] **The game feels restrained.** No cartoon proportions, no bouncy camera, no
      over-animated UI, no arcade sound design.

---

## Craft

**Engine:** Unity 6000.6.8f1, macOS arm64.  
**Assets:** Blender 5.2.2 LTS, exported as FBX and assembled in Unity.  
**Input:** the old Input Manager (`activeInputHandler: 0`), so every control works
without an Input System package rewrite.

The truck is generated by `unity-assets/model_truck_v4.py`. The trailer is generated by
`unity-assets/model_container_v2.py`. Both export to `unity-assets/Models/` and are
copied into the Unity project by `prepare_unity.sh`.

The visual craft lives in three places:

- **Blender** for proportion, panel breaks, wheel arches, glass, trim and the details
  that make the truck recognizable.
- **Unity materials and lighting** for paint, chrome, rubber, glass, reflections and the
  dawn grade. Chrome and white paint must be judged in the game, not in a flat Blender
  render.
- **Custom shaders** for depth-tested 3D text, cab glass and the final color grade.

The build also enforces quality at runtime so a fresh player never starts with shadows
or MSAA disabled: soft shadows, very high shadow resolution, 180 m shadow distance, four
cascades, 4x MSAA, anisotropic filtering, six pixel lights, realtime reflection probes
and vSync.

### Verification modes

The shipping app has three automation modes, all of which run the real build:

```bash
# world screenshots
"unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" \
  -autoshot -shotdir /tmp/d2d_shots

# phone and end-screen screenshots
"unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" \
  -uishot -shotdir /tmp/d2d_ui

# full automated shift
"unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" \
  -autoplay -shotdir /tmp/d2d_play
```

The executable path contains spaces. Use the full quoted path; `.../MacOS/DockToDrop`
will fail with exit code 127.

A successful full shift logs:

```text
D2D PLAY END ended=True sealPhoto=True inspected=5
```

A successful UI pass logs:

```text
D2D UISHOT previewReady=True
D2D UISHOT done
```

Automation proves the loop and the rendering. It does not prove real keyboard/mouse
focus or audio. Those require a normal launch and a human at the machine.

---

## Build and run

```bash
cd /Users/baxtiyorov/Documents/deepseek-harness/default-workspace/dock-to-drop
./prepare_unity.sh
./build_mac.sh
open "unity-project/Build/Mac/DockToDrop.app"
```

The build lands at:

```text
unity-project/Build/Mac/DockToDrop.app
```

Requirements: macOS, Unity 6000.6.8f1 at `tools/Unity/Unity.app`, and Blender 5.2.2 LTS
only if you want to regenerate the FBX assets.

To regenerate the truck:

```bash
/Applications/Blender.app/Contents/MacOS/Blender -b \
  --python unity-assets/model_truck_v4.py
```

That writes the FBX and the three `truck_v4_*.png` preview renders. The valid EEVEE
render-engine string in this Blender version is `BLENDER_EEVEE`, not
`BLENDER_EEVEE_NEXT`.

To regenerate the trailer:

```bash
/Applications/Blender.app/Contents/MacOS/Blender -b \
  --python unity-assets/model_container_v2.py
```

---

## Project map

```text
dock-to-drop/
  README.md                     this document
  prepare_unity.sh              copy assets into unity-project and prepare the scene
  build_mac.sh                  batch-build the macOS player
  voice/                        Edge neural TTS dialogue MP3s (see manifest.json)
  unity-assets/
    Scripts/
      GameFlow.cs               shift flow, HUD, end screen, automation modes, quality
      WorldBuilder.cs           scene layout, materials, truck, trailer, cab interior
      TruckController.cs        driving, engine, lights, door, enter/exit
      PlayerController.cs       first-person walk, seated camera, look/focus
      InteractionSystem.cs      raycast interaction and prompts
      PhoneUI.cs                modal Relay phone, load, chat, camera, proof, calls
      AudioManager.cs           voice/SFX/engine/music sources, ducking, footsteps
      MeshFactory.cs            procedural meshes, materials, 3D text
      UIFactory.cs              HUD widgets
      GateController.cs         gate arm and guard flow
      InspectionPoint.cs        five trailer inspection points
      PhotoTarget.cs            seal photo target
      CinematicGrade.cs         grade/bloom/vignette
      SkyBillboard.cs           sky and clouds
      MobileControls.cs         touch fallback
    Shaders/
      Text3D.shader             depth-tested world text
      Glass.shader              cab and mirror glass
      Grade.shader              color grade
    Models/
      Freightliner_Cascadia.fbx current v4 truck
      Amazon_53ft_Container.fbx trailer
    Textures/                   ground, decals, signage
    Audio/                      real diesel, doors, horn, gate, camera, footsteps, piano
    Previews/                   Blender render previews
    model_truck_v4.py           current truck generator
    model_container_v2.py       current trailer generator
    make_audio.py               compatibility entry point for the SFX build
    make_sfx_natural.py         cuts CC0 field recordings into the game's SFX
    AudioSource/                CC0 source recordings (see Audio/CREDITS.md)
  unity-project/
    Assets/                     generated/copied Unity assets
    ProjectSettings/            Unity 6000.6.8f1, old Input Manager
    Build/Mac/DockToDrop.app    shipping build
    Shots/                      README screenshots
  logs/                         build and verification logs
```

The project is not a git repository. Back up `unity-assets/` and `unity-project/` before
large changes, or initialise git and commit the current state first.

---

## The next pass

The vertical slice establishes the loop. The next pass is about raising the floor until
the game feels real.

### Priorities, in order

1. **The cab.** Make the interior pass the look-around test. The player spends the
   emotional center of the game in this seat; it has to be the most finished space in the
   build.
2. **Paint and light.** Make the truck white at dawn. Tune the paint, the directional
   light, the reflection probes and the grade together, in Unity.
3. **Sound.** Remove every buzz, clean every loop point, and make the diesel, air brake,
   door and gate sounds feel physical. Verify on laptop speakers and headphones.
4. **Input.** Test a normal launch, not an automation flag. Click, look, walk, drive,
   open the phone, exit, release the mouse, refocus, recapture.
5. **The full shift.** Run `-autoplay` on the current build and fix anything that breaks
   between the title screen and the proof screen.
6. **The world.** Add the details that make the travel center and the yard feel occupied:
   wear, clutter, puddles, tire marks, lit windows, small signage.
7. **The screenshots.** Regenerate every image from the final build.

### Non-negotiables

- Do not add new minigames or arcade mechanics. The game is about doing the job.
- Do not replace the authored truck with primitives or a marketplace asset that does not
  match the Cascadia silhouette.
- Do not use OS text-to-speech for dispatch or the guard.
- Do not reintroduce the old wind/electrical ambient loop. Silence is part of the sound
  design; the piano bed is the only intentional loop.
- Do not hide the world behind a full-screen HUD. The phone is the only modal surface.
- Do not declare the game finished from `-autoshot` alone. A real input test and a full
  shift are required.

### The first 90 minutes

1. Build the current source and run `-autoshot`. Open `03_cab.png` and look around the
   cab from the seat.
2. Isolate the material that is producing the cream surfaces. Temporarily set the truck's
   white paint to bright magenta, rebuild, and screenshot. If the slabs turn magenta, the
   problem is the truck's own painted material, not the Unity trim.
3. Fix it at the source. The robust approach is a two-sided body shader: front faces keep
   the white paint, back faces render charcoal. Then cover the remaining front-facing
   inner faces with full-height door cards sized from the door's renderer bounds, plus a
   charcoal kick panel under the dash.
4. Rebuild and run the look-around test with the door open and closed.
5. Tune the paint and lighting until the truck reads white in `02_truckstop.png`.
6. Launch normally and test real input, focus and audio.
7. Run the full `-autoplay` shift and check the final log line.

### Definition of done

The game is done when a player can start the shift, walk to the truck, climb in, take the
call, verify the load, start the engine, clear the gate, stage the trailer, inspect all
five points, photograph the seal, send the proof and end the shift — without a single
moment that feels like a tech demo.

---

## Credits and limits

- **Independent simulation.** Not affiliated with Amazon, Relay, Freightliner, Daimler
  Truck, or any carrier or broker. The facility and load are a fictional training
  scenario built around a real-world workflow.
- **No live services.** The load board, dispatch call, phone and photo proof are
  simulated locally. The game does not connect to Amazon, a real load board, or a phone
  system.
- **Single shift.** One complete shift is included. The flow is data-driven enough to add
  exceptions such as a wrong seal, a damaged trailer, or a late appointment.
- **Not a substitute for CDL/ELDT training.** Company policy, hands-on instruction and
  current regulations always take precedence.
- **Do not play while driving.** Use the simulator only while parked.

The older browser prototype (`index.html`, `src/`, `public/`) is still in the repository,
but the Unity build is the current game.

---

## Brief for the next agent

```text
You are continuing Dock to Drop, a first-person freight-day simulator in Unity.

Read this README first. The vertical slice is playable end to end. Your job is not to
add features; it is to raise the floor until the game feels real.

The player's acceptance criteria:
- genuinely playable, not a tech demo;
- a recognizable Freightliner Cascadia and a cab that holds up to a close look;
- Amazon DFW9 check-in, gate, yard, 53' trailer and five-point inspection;
- natural human voices, not robotic TTS;
- no constant buzz or hum;
- keyboard and mouse work with real OS focus;
- nothing cartoonish, boxy or over-animated.

Start with the cab. From the driver's seat, the cab currently shows painted shell where
the interior trim should be. Diagnose the source with the magenta material test, then
fix it with a two-sided body shader (front = paint, back = charcoal), full-height door
cards sized from the door renderer bounds, and a kick panel under the dash. Test the
look-around with the driver door open and closed.

Then: make the truck white at dawn; verify the title screen is silent and the diesel loop
is clean on real speakers; test a normal launch with real keyboard/mouse and focus;
rerun the full -autoplay shift until it logs
"D2D PLAY END ended=True sealPhoto=True inspected=5"; refresh the screenshots.

Build: ./prepare_unity.sh && ./build_mac.sh
App: "unity-project/Build/Mac/DockToDrop.app/Contents/MacOS/Dock to Drop" (spaces matter)
Autoshot: ... -autoshot -shotdir /tmp/d2d_shots
Autoplay: ... -autoplay -shotdir /tmp/d2d_play
```

---

*If a player finishes the shift and feels like they just worked, the game is done.*
