using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameFlow : MonoBehaviour
{
    public PlayerController Player;
    public Camera Camera;
    public AudioManager Audio;
    public PhoneUI Phone;
    public TruckController Truck;
    public WorldRefs World;
    public InteractionSystem Interaction;
    MobileControls mobileControls;

    Canvas hudCanvas;
    GameObject hudRoot;
    Text objectiveText, clockText, promptText, subtitleText, speedText, gearText, focusHint;
    Text warningText, warningIconText;
    Image warningIconBg;
    RectTransform promptRect;
    GameObject promptPanel, subtitlePanel, guardPanel, endPanel, speedPanel, warningPanel, pausePanel;
    Text sensitivityText;
    Coroutine warningRoutine;
    int warningPriority;
    float warningUntil;
    bool paused;
    GameObject endDim;
    GameObject[] hudBase;
    Text guardText, endKicker, endSub, endInspect, endSeal, endTime, endNote, endDebrief;
    Coroutine subtitleRoutine;
    bool gateOpen, gatePassed, ended;
    bool guardOpen;
    bool sealPhotoSent;
    bool loadVerified;
    int inspected;
    float playTime;
    string currentGoal;
    GameObject startPanel;
    bool started;
    bool autoShotMode;
    bool menuShotMode;
    // trailer coupling
    BoxCollider doorCol, seatCol, linesCol, crankCol;
    Interactable ignitionIt;
    bool callStarted, departureWarned;
    Text trailerText;
    int trailerKey = -1;
    float lastBlockedMsg = -10f, lastJackMsg = -10f;
    // shift log: contacts, hard braking and cab-securing misses feed the debrief
    int contactCount;
    string lastContactName = "";
    Vector3 lastContactPoint;
    float lastContactAt = -10f;
    int hardBrakeCount;
    float lastHardBrakeAt = -10f;
    int engineRunningExits;
    int kingpinMisses;
    int guardWrongAttempts;
    int deadheadDepartMinutes;
    bool deadheadOnTime;
    // backing lesson (stage 3)
    bool backingActive, backingPassed, backingResultShown, backingWasReversing;
    int backingContacts;
    int backingAttempts;
    float backingAlignment, backingStopDistance;
    Vector3 backingStartPos;
    Quaternion backingStartRot = Quaternion.identity;
    float backingRefresh;
    GameObject backingPanel;
    Text backingText, backingResult;
    TrailerRig Rig => World != null ? World.trailerRig : null;
    bool InspectionDone => inspected >= 5 && sealPhotoSent;
    string DeadheadObjective => "Deadhead " + DeadheadAssignedMiles + " mi to Gate 1 · Amazon DFW9.";
    bool autoPlayMode;
    bool uiShotMode;
    string shotDir;

    // ---- deadhead -------------------------------------------------------
    // The assignment is a genuine 32/60-mile drive. The highway is built to
    // exactly that length (1 world unit = 1 metre) and the odometer integrates
    // the truck's actual path along it; there is no multiplier or compressed
    // mileage. The destination yard and gate sit at the far end of the road.
    public int DeadheadAssignedMiles { get; private set; } = 32;
    const float DeadheadMph = 55f;
    const float MetresPerMile = 1609.344f;
    float deadheadRouteLength = 180f;
    float deadheadWorld;
    float deadheadMiles;
    Vector3 deadheadLastPos;
    bool deadheadActive, deadheadArrived;
    string deadheadDepartClock = "05:40", deadheadArrivalClock = "--:--";
    float deadheadHoldUntil;
    GameObject tripPanel;
    Text tripKicker, tripMiles, tripDetail, tripEta;
    float tripRefresh;

    public bool BlocksInteraction => (Phone != null && Phone.IsOpen) || guardOpen || ended;
    public bool IsEnded => ended;
    public bool LoadVerified => loadVerified;

    public bool IsAutomation => autoShotMode || menuShotMode || autoPlayMode || uiShotMode || HasArg("-perfprobe");

    // When the player switches to another app, the game goes quiet and drops to a
    // low frame rate so it neither drones on in the background nor slows the Mac.
    // Automation keeps full speed (it is already muted) so its timing holds.
    // Launched from Terminal (the raw executable), macOS can show the window
    // without making the game the active app: clicks land, but the keyboard
    // stays with Terminal. Ask macOS to activate us so keys always arrive.
    public static void ActivateSelf()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("/usr/bin/osascript",
                "-e \"tell application id \\\"" + Application.identifier + "\\\" to activate\"")
            { UseShellExecute = false, CreateNoWindow = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch (System.Exception e) { Debug.LogWarning("D2D activate failed: " + e.Message); }
#endif
    }

    void OnApplicationFocus(bool focused)
    {
        Debug.Log("D2D FOCUS " + focused);
        if (IsAutomation) return;
        AudioListener.pause = !focused;
        QualitySettings.vSyncCount = focused ? 1 : 0;
        Application.targetFrameRate = focused ? 60 : 10;
        // Losing the window is another way to lose the mouse. Pause and show
        // the click-to-resume overlay instead of grabbing it back on refocus.
        if (!focused && started && !ended && !(Phone != null && Phone.IsOpen) && !guardOpen) PauseGame();
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        VehicleContactBus.Contact += OnVehicleContact;
        // The project asset has mixed quality levels; force the good one at runtime
        // so a fresh build never starts with shadows and MSAA switched off.
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        // 130 m cut the shadow cascades off just as roadside trees entered the
        // view, so their shadows popped on and off while the truck drove. Keep a
        // longer shadow range and a stable split; the first cascade still gets
        // most of the resolution where the cab and gate are.
        QualitySettings.shadowDistance = 280f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowCascade4Split = new Vector3(0.067f, 0.18f, 0.42f);
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.enableLODCrossFade = false;
        QualitySettings.antiAliasing = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.pixelLightCount = 6;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.vSyncCount = 1;
        var args = System.Environment.GetCommandLineArgs();
        autoShotMode = HasArg("-autoshot");
        menuShotMode = HasArg("-menushot");
        autoPlayMode = HasArg("-autoplay");
        uiShotMode = HasArg("-uishot");
        // Dispatch picks one of two deadheads. Automation stays deterministic;
        // -deadhead60 / -deadhead32 force a value for a test run.
        DeadheadAssignedMiles = HasArg("-deadhead60") ? 60 : HasArg("-deadhead32") ? 32
            : (IsAutomation ? 32 : (UnityEngine.Random.value < 0.5f ? 32 : 60));
        shotDir = Application.persistentDataPath;
        for (int i = 0; i < args.Length; i++) if (args[i] == "-shotdir" && i + 1 < args.Length) shotDir = args[i + 1];
        if (autoShotMode || menuShotMode || uiShotMode) System.IO.Directory.CreateDirectory(shotDir);
        // Automation runs are for pictures and logs, often behind other windows:
        // they never make sound.
        if (IsAutomation) AudioListener.volume = 0f;
        else ActivateSelf();
        Debug.Log("D2D START version=" + Application.version + " product=" + Application.productName
            + " deadhead=" + DeadheadAssignedMiles + " auto=" + autoShotMode + " dir=" + shotDir);
        Audio = gameObject.AddComponent<AudioManager>();
        BuildHud();
        Debug.Log("D2D HUD built");
        World = WorldBuilder.Build(this, transform);
        Debug.Log("D2D WORLD built");
        BuildPlayer();
        RefreshSensitivityLabel();
        Truck = World.truck;
        Truck.player = Player;
        Truck.audio = Audio;
        if (World.gateTrigger != null)
        {
            // The trigger fires when the nose of the cab enters it, not when
            // the truck's origin reaches the trigger centre. Measure to that
            // contact point so a straight run lands exactly on the assignment
            // instead of reading a mile and a half short.
            Vector3 gate = World.gateTrigger.transform.position;
            var gateCol = World.gateTrigger.GetComponent<BoxCollider>();
            float gateHalf = gateCol != null ? gateCol.size.z * 0.5f : 0f;
            var hull = Truck.GetComponent<BoxCollider>();
            float nose = hull != null ? hull.center.z + hull.size.z * 0.5f : 0f;
            Vector3 contact = gate - World.gateTrigger.transform.forward * (gateHalf + nose);
            deadheadRouteLength = Mathf.Max(1f, FlatXZ(contact - Truck.transform.position).magnitude);
        }
        deadheadLastPos = Truck.transform.position;
        float assignedMetres = DeadheadAssignedMiles * MetresPerMile;
        if (Mathf.Abs(deadheadRouteLength - assignedMetres) > 1f)
            Debug.LogError("D2D ROUTE MISMATCH assigned=" + assignedMetres.ToString("F1")
                + " measured=" + deadheadRouteLength.ToString("F1"));
        Debug.Log("D2D DEADHEAD assigned=" + DeadheadAssignedMiles + " routeMetres=" + deadheadRouteLength.ToString("F1")
            + " routeMiles=" + DeadheadRouteMiles.ToString("F3") + " (odometer: 1 unit = 1 m, no compression)");
        BuildTruckInteractions();
        Phone = new GameObject("PhoneUI").AddComponent<PhoneUI>();
        Phone.sealTarget = World.sealTarget;
        Phone.Build(this);
        Debug.Log("D2D PHONE built");
        BuildMobileControls();
        Phone.Close();
        SetObjective("Walk to the Freightliner. Open the driver door.");
        BuildStartScreen();
        // The title card owns a composed yard view. Keeping the camera on the
        // player's eyes while the third-person driver model is visible fills the
        // frame with the inside of his head and arms, so hand the camera to a
        // fixed pose and hide the body until START SHIFT.
        Player.ShowMenuView(new Vector3(-10f, 3.0f, 21f), new Vector3(0f, 1.9f, 8f), 52f);
        hudCanvas.gameObject.SetActive(false);
        Player.SetUILock(true);
        if (autoShotMode) StartCoroutine(AutoShotRoutine());
        else if (menuShotMode) StartCoroutine(MenuShotRoutine());
        else if (autoPlayMode) StartCoroutine(AutoPlayRoutine());
        else if (uiShotMode) StartCoroutine(UIShotRoutine());
        else if (HasArg("-perfprobe")) StartCoroutine(PerfProbe());
    }

    // -perfprobe: measures the frame rate at the start view with one feature
    // switched off at a time, then quits. Used to find what costs the most.
    IEnumerator PerfProbe()
    {
        yield return new WaitForSeconds(1f);
        BeginShift();
        Player.LookAt(new Vector3(0f, 1.5f, 8f));
        yield return new WaitForSeconds(3f);
        var trees = new List<GameObject>();
        foreach (var g in FindObjectsByType<LODGroup>(FindObjectsSortMode.None)) trees.Add(g.gameObject);
        var truckVis = GameObject.Find("TruckVisual");
        var driver = Player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        string[] steps = { "base", "noshadow", "notrees", "notruck", "nomsaa", "nodriver", "onelight", "base2" };
        foreach (var st in steps)
        {
            QualitySettings.shadows = st == "noshadow" ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.antiAliasing = st == "nomsaa" ? 0 : 4;
            QualitySettings.pixelLightCount = st == "onelight" ? 1 : 6;
            foreach (var t in trees) t.SetActive(st != "notrees");
            if (truckVis != null) truckVis.SetActive(st != "notruck");
            foreach (var r in driver) r.enabled = st != "nodriver";
            yield return new WaitForSeconds(2.5f);
            float t0 = Time.realtimeSinceStartup; int n = 0;
            while (Time.realtimeSinceStartup - t0 < 4f) { n++; yield return null; }
            Debug.Log($"D2D PROBE {st} fps={n / (Time.realtimeSinceStartup - t0):F1} trees={trees.Count}");
        }
        Application.Quit();
    }

    IEnumerator Intro()
    {
        ShowSubtitle("DALLAS, TEXAS  ·  05:40\nAmazon Relay load  #D2D-0612", 4.5f);
        yield return new WaitForSeconds(2.0f);
        ShowSubtitle("Walk to the truck. Open the driver door.  [E]", 4f);
    }

    void BuildPlayer()
    {
        var p = new GameObject("Player");
        var cc = p.AddComponent<CharacterController>();
        cc.height = 1.82f; cc.radius = 0.34f; cc.center = new Vector3(0, 0.91f, 0);
        cc.slopeLimit = 50f; cc.stepOffset = 0.35f;
        var pc = p.AddComponent<PlayerController>();
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.04f; cam.farClipPlane = 1200f; cam.fieldOfView = 68f;
        camGo.AddComponent<CinematicGrade>();
        camGo.AddComponent<AudioListener>();
        camGo.transform.SetParent(p.transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.66f, 0);
        pc.cam = cam; pc.audioManager = Audio; pc.SetCameraHome(new Vector3(0, 1.66f, 0));
        pc.body = DriverBody.Create(pc);
        p.transform.position = World.playerSpawn.position;
        p.transform.rotation = World.playerSpawn.rotation;
        Player = pc; Camera = cam;
        Interaction = p.AddComponent<InteractionSystem>();
        Interaction.cam = cam; Interaction.flow = this;
    }

    void BuildTruckInteractions()
    {
        // Driver door
        var door = new GameObject("InteractDoor");
        door.transform.SetParent(Truck.transform, false);
        door.transform.localPosition = new Vector3(-1.95f, 1.55f, 1.55f);
        var dc = door.AddComponent<BoxCollider>(); dc.size = new Vector3(0.85f, 2.30f, 1.95f); dc.isTrigger = true; doorCol = dc;
        var di = door.AddComponent<Interactable>(); di.prompt = "Open driver door  [E]"; di.id = "door";
        // The collider is an empty marker; the visible door is the FBX transform.
        di.SetHighlightRoot(Truck.driverDoor);
        // Seat
        var seat = new GameObject("InteractSeat");
        seat.transform.SetParent(Truck.transform, false);
        seat.transform.localPosition = new Vector3(-1.72f, 1.45f, 1.55f);
        var sc = seat.AddComponent<BoxCollider>(); sc.size = new Vector3(0.8f, 1.9f, 1.5f); sc.isTrigger = true; seatCol = sc;
        sc.enabled = false;
        var si = seat.AddComponent<Interactable>(); si.prompt = "Climb into the cab  [E]"; si.id = "seat";
        si.onInteract.AddListener(() =>
        {
            if (!Truck.doorOpen) { ShowSubtitle("Open the driver door first.", 2.2f); return; }
            Truck.Enter();
        });
        di.onInteract.AddListener(() =>
        {
            // Opening the door is one action; climbing in is a separate press so
            // the player can look inside, back out, or walk around first.
            Truck.SetDoor(true);
            dc.enabled = false;
            sc.enabled = true;
            ShowSubtitle("Driver door open. Press E at the seat to climb in.", 2.4f);
        });

        // Ignition / dashboard
        var ign = new GameObject("InteractIgnition");
        ign.transform.SetParent(Truck.transform, false);
        ign.transform.localPosition = new Vector3(-0.62f, 2.18f, 1.20f);
        var ic = ign.AddComponent<BoxCollider>(); ic.size = new Vector3(0.62f, 0.50f, 0.50f); ic.isTrigger = true;
        var ii = ign.AddComponent<Interactable>(); ii.prompt = "Start engine  [E]"; ii.id = "ignition";
        ignitionIt = ii;
        ii.onInteract.AddListener(() =>
        {
            if (!Player.IsSeated) return;
            if (!loadVerified) { ShowSubtitle("Review the load on your phone before rolling.", 2.6f); return; }
            if (!Truck.engineOn) { Truck.StartEngine(); return; }
            // second control on the dash: the red trailer-air supply valve
            var rig = Rig;
            if (rig != null && rig.coupled && !rig.airSupplied)
            {
                if (rig.linesConnected && rig.gearUp) rig.SupplyAir();
                else ShowWarning("No trailer air yet: connect the glad hands and raise the landing gear first.", 3f, 1);
            }
        });

        // Glad hands and light cord, reached from the ground behind the cab on the
        // driver's side. Only live while the trailer is coupled and unconnected.
        var gl = new GameObject("InteractLines");
        gl.transform.SetParent(Truck.transform, false);
        gl.transform.localPosition = new Vector3(-1.78f, 1.55f, -3.0f);
        linesCol = gl.AddComponent<BoxCollider>(); linesCol.size = new Vector3(0.7f, 1.4f, 1.5f); linesCol.isTrigger = true;
        linesCol.enabled = false;
        var gi = gl.AddComponent<Interactable>(); gi.id = "lines"; gi.prompt = "Connect air lines and light cord  [E]";
        gi.onInteract.AddListener(() => { if (Rig != null) Rig.ConnectLines(); });
        if (Rig != null)
        {
            var ct = Rig.transform.Find("InteractCrank");
            if (ct != null) { crankCol = ct.GetComponent<BoxCollider>(); crankCol.enabled = false; }
        }
    }

    public void OnEnteredTruck()
    {
        Truck.SetDoor(false);
        if (doorCol != null) doorCol.enabled = false;
        if (seatCol != null) seatCol.enabled = false;
        if (callStarted) { if (gatePassed) RefreshObjective(); else SetObjective(Truck.engineOn ? DeadheadObjective : "Start the engine.  [E]"); return; }
        callStarted = true;
        SetObjective("Answer the dispatch call.");
        if (!autoShotMode) StartCoroutine(CallAfter(3.2f));
    }

    IEnumerator CallAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (ended || Truck == null) yield break;
        Ring();
    }

    void Ring()
    {
        if (Audio != null) Audio.PlaySfx("ring", 0.8f);
        Phone.ShowIncomingCall(AnswerCall, DeclineCall);
        SetObjective("Incoming call from dispatch. Answer it.");
    }

    void AnswerCall()
    {
        Audio.StopVoice();
        Phone.BeginCallAnswered();
        Phone.SetCallNext(null);
        StartCoroutine(CallRoutine());
    }

    IEnumerator CallRoutine()
    {
        string[][] lines = {
            new[]{"dispatch_01","Morning, driver. It's Mike. You awake? I've got a loaded fifty-three footer out of the Amazon fulfillment center in Dallas. Good paying run."},
            new[]{"dispatch_02","Pickup is the Amazon fulfillment center, 4800 Industrial Boulevard, Dallas. Appointment is 7:30. They charge for no-shows, so don't be late."},
            new[]{"dispatch_03","Delivery is the Amazon delivery station in Houston. Appointment is 14:00. About 240 miles."},
            new[]{"dispatch_04","Fifty-three foot dry container, 38,500 pounds, palletized retail. Rate is $2,450 all in. Seal number is 482913."},
            new[]{"dispatch_05","I'm sending the load screenshot to your phone. Open Relay, verify the appointment and the seal, then start rolling."}
        };
        for (int i = 0; i < lines.Length; i++)
        {
            Phone.SetCallLine(lines[i][1]);
            Audio.PlayVoice(lines[i][0]);
            float len = Audio.Get(lines[i][0]) != null ? Audio.Get(lines[i][0]).length : 3f;
            // Space / Enter / click skips to the next line; the details are on the phone anyway.
            float until = Time.time + Mathf.Max(0.6f, len - 0.1f);
            yield return null;
            while (Time.time < until)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                { Audio.StopVoice(); break; }
                yield return null;
            }
        }
        Phone.EndCall();
        Phone.Open("load");
        SetObjective("Review the load on your phone.  [TAB]");
        ShowSubtitle("Load details are on your phone. Verify the appointment, seal, and rate.", 4f);
        Phone.SetCallLine("");
    }

    void DeclineCall()
    {
        Phone.EndCall();
        ShowSubtitle("Dispatch will call back. He is not going away.", 2.2f);
        StartCoroutine(CallAfter(3f));
    }

    public void VerifyLoad()
    {
        if (loadVerified) return;
        loadVerified = true;
        Phone.Close();
        SetObjective("Start the engine.  [E]");
        ShowSubtitle("Details verified. Start the engine when you're ready.", 3f);
        Audio.PlayVoice("coach_01");
    }

    public void OnEngineStarted()
    {
        if (gatePassed) { RefreshObjective(); return; }
        SetObjective(DeadheadObjective);
        ShowSubtitle("Engine running. Follow the two-lane north to Gate 1. "
            + DeadheadAssignedMiles + " miles; the odometer counts the metres you actually drive.", 6f);
    }

    public void OnExitedTruck()
    {
        // Exit() shuts the engine down as a safety action, but stepping out with
        // it still running is a real procedure miss, so log it for the debrief.
        if (Truck != null && Truck.ExitLeftEngineRunning)
        {
            engineRunningExits++;
            ShowWarning("Engine was still running. Shut it down and set the brake before leaving the cab.", 3.5f, 1);
        }
        // the door is shut behind the driver; one press opens it and climbs back in
        if (doorCol != null) doorCol.enabled = true;
        if (seatCol != null) seatCol.enabled = false;
        if (!gatePassed) { SetObjective(Truck.engineOn ? DeadheadObjective : "Climb back in and drive to Gate 1."); return; }
        if (InspectionDone || (Rig != null && Rig.coupled)) RefreshObjective();
        else SetObjective("Walk to trailer A-14 and run the five-point inspection.");
    }

    // ---- trailer coupling ------------------------------------------------

    // The objective for wherever the driver is in the hook-up. Inspection can
    // happen before or after coupling; the shift ends at the gate with both.
    void RefreshObjective()
    {
        var rig = Rig;
        if (ended || rig == null || !gatePassed) return;
        bool seated = Player != null && Player.IsSeated;
        if (!rig.coupled)
        {
            if (!InspectionDone)
            {
                if (seated) SetObjective("Stop beside A-14 and press X to exit. Inspect it first.");
                else if (inspected < 5) SetObjective("Continue inspecting trailer A-14.  " + inspected + "/5");
                else SetObjective("Photograph seal 482913 with your phone.  [TAB]");
                return;
            }
            if (!seated) SetObjective("Climb back in. Next: hook trailer A-14.");
            else if (!Truck.engineOn) SetObjective("Start the engine.  [E]");
            else SetObjective("Hook A-14: line up with its nose between the yellow lines and back slowly under it.");
            return;
        }
        if (!rig.tugPassed)
        {
            SetObjective(seated && Truck.engineOn ? "Tug test: ease forward in low gear against the trailer brakes."
                                                  : "Climb in, start the engine and do a tug test.");
            return;
        }
        if (!rig.linesConnected || !rig.gearUp)
        {
            if (seated) SetObjective("Set the brake and step out [X]. Connect the air lines and raise the landing gear.");
            else if (!rig.linesConnected) SetObjective("Connect the air lines and light cord behind the cab.");
            else SetObjective("Crank up the landing gear on the trailer's " + rig.CrankSideName + " side.");
            return;
        }
        if (!seated) { SetObjective("Climb back in the cab."); return; }
        if (!Truck.engineOn) { SetObjective("Start the engine.  [E]"); return; }
        if (!rig.airSupplied) { SetObjective("Push in the red trailer-air valve.  [E]"); return; }
        SetObjective(InspectionDone ? "Pull A-14 out to Gate 1. Watch the trailer swing on the turn."
                                    : "Finish the inspection and the seal photo before you leave.");
    }

    public void OnCoupled(float hitSpeed)
    {
        if (Audio != null) Audio.PlaySfx("fifth_wheel_lock", hitSpeed > 1.2f ? 1f : 0.85f);
        ShowSubtitle(hitSpeed > 1.6f ? "FIFTH WHEEL LOCKED\nHard hit. Back under slower next time; that jolts the load."
                                     : "FIFTH WHEEL LOCKED\nThe jaws closed on the kingpin. Tug test next: ease forward in low gear.", 5f);
        Debug.Log("D2D FLOW coupled speed=" + hitSpeed.ToString("F2"));
        RefreshObjective();
    }

    public void OnKingpinMissed(float lateral, float angle)
    {
        kingpinMisses++;
        string why = Mathf.Abs(lateral) > 0.24f
            ? "The kingpin is " + Mathf.RoundToInt(Mathf.Abs(lateral) * 100f) + " cm to the " + (lateral > 0f ? "right" : "left") + " of the jaws."
            : "You're coming in " + Mathf.RoundToInt(angle) + " degrees crooked.";
        ShowWarning("Missed the kingpin. " + why + " Pull forward, straighten up and back in again.", 4f, 2);
    }

    public void OnTugPassed()
    {
        if (Audio != null) Audio.AirBrake(0.35f);
        ShowSubtitle("TUG TEST GOOD\nThe trailer held against the pull. The kingpin is locked.", 4f);
        Debug.Log("D2D FLOW tug passed");
        RefreshObjective();
    }

    public void OnLinesConnected()
    {
        if (Audio != null) Audio.PlaySfx("glad_hand", 0.9f);
        ShowSubtitle("GLAD HANDS CONNECTED\nRed is emergency, blue is service, green is the light cord.", 4f);
        Debug.Log("D2D FLOW lines connected");
        RefreshObjective();
    }

    public void OnGearRaised()
    {
        ShowSubtitle("LANDING GEAR UP\nThe trailer rides on the fifth wheel now.", 3.5f);
        Debug.Log("D2D FLOW gear up");
        RefreshObjective();
    }

    public void OnTrailerAir()
    {
        if (Audio != null) Audio.PlaySfx("trailer_air", 0.9f);
        ShowSubtitle("TRAILER AIR SUPPLIED\nBrakes released. Pull out slowly; the trailer cuts inside on turns.", 5f);
        Debug.Log("D2D FLOW trailer air");
        RefreshObjective();
    }

    public void OnJackknife()
    {
        if (Time.time - lastJackMsg < 3f) return;
        lastJackMsg = Time.time;
        ShowWarning("JACKKNIFE — the trailer is folding against the cab. Pull forward to straighten out.", 3.5f, 3);
    }

    public void OnTrailerBlocked()
    {
        if (Time.time - lastBlockedMsg < 3f) return;
        lastBlockedMsg = Time.time;
        ShowWarning("The trailer would hit something. Back up and swing wider.", 3f, 2);
    }

    // Raised by TruckController and TrailerRig. One shared handler means the HUD,
    // the shift log and the backing lesson all see the same contacts.
    void OnVehicleContact(VehicleContact c)
    {
        if (!started || ended) return;
        // Debounce: a held throttle against a wall raises many physics hits.
        if (Time.time - lastContactAt < 0.9f) return;
        lastContactAt = Time.time;
        contactCount++;
        lastContactName = c.name;
        lastContactPoint = c.point;
        if (backingActive) backingContacts++;
        string what = c.trailer ? "the trailer" : "the tractor";
        string fast = c.speed > 2.5f ? " At that speed, cargo and equipment can shift." : "";
        ShowWarning("CONTACT: " + what + " hit " + c.name + "." + fast + " Stop and check before continuing.", 4f, 2);
        Debug.Log("D2D CONTACT " + (c.trailer ? "trailer" : "tractor") + " collider=" + c.name
            + " speed=" + c.speed.ToString("F2") + " point=" + c.point.ToString("F1")
            + " count=" + contactCount);
    }

    void OnDestroy()
    {
        VehicleContactBus.Contact -= OnVehicleContact;
    }

    void UpdateCoupling()
    {
        var rig = Rig;
        if (rig == null || Truck == null) return;
        if (linesCol != null) linesCol.enabled = rig.coupled && !rig.linesConnected && !Player.IsSeated;
        if (crankCol != null) crankCol.enabled = rig.coupled && !rig.gearUp && !rig.Cranking && !Player.IsSeated;
        if (ignitionIt != null)
            ignitionIt.prompt = !Truck.engineOn ? "Start engine  [E]"
                : rig.coupled && !rig.airSupplied ? "Push in the red trailer-air valve  [E]" : "Engine running";
        if (trailerText != null)
        {
            int key = !rig.coupled ? 0 : !rig.airSupplied ? 1 : 2;
            if (key != trailerKey)
            {
                trailerKey = key;
                trailerText.text = key == 0 ? "BOBTAIL" : key == 1 ? "5TH WHEEL LOCKED  ·  TRAILER BRAKES SET" : "5TH WHEEL LOCKED  ·  TRAILER AIR";
                trailerText.color = key == 0 ? new Color(0.62f, 0.66f, 0.70f) : key == 1 ? new Color(1f, 0.74f, 0.25f) : new Color(0.45f, 0.95f, 0.55f);
            }
        }
        UpdateBackingLesson();
        // the shift ends when the rig rolls up to the gate with everything done
        if (!ended && rig.coupled && rig.airSupplied && Player.IsSeated)
        {
            Vector3 p = Truck.transform.position;
            float destZ = World.destination != null ? World.destination.position.z : 0f;
            if (p.z < destZ + 216f && p.z > destZ + 150f && Mathf.Abs(p.x) < 14f)
            {
                if (InspectionDone) { Debug.Log("D2D FLOW departed"); EndShift(); }
                else if (!departureWarned) { departureWarned = true; ShowWarning("Not yet: the inspection and the seal photo come first.", 3f, 1); }
            }
        }
    }

    void StartBackingLesson()
    {
        backingActive = true;
        backingContacts = 0;
        backingAttempts = 0;
        backingResultShown = false;
        backingWasReversing = false;
        backingRefresh = 0f;
        backingStartPos = Truck.transform.position;
        backingStartRot = Truck.transform.rotation;
        backingResult.text = "REVERSING TO THE KINGPIN\nUse both mirrors. Creep at walking pace.\nStraighten the tractor before the fifth wheel reaches the nose.";
        if (backingPanel != null) backingPanel.SetActive(true);
        ShowSubtitle("Backing lesson: reverse slowly, use both mirrors, and watch the trailer sweep.", 5f);
        Debug.Log("D2D BACKING lesson start");
    }

    void FinishBackingLesson()
    {
        backingActive = false;
        backingResultShown = true;
        float lateral = Mathf.Abs(Truck.LastCouplingLateral);
        float angle = Mathf.Abs(Truck.LastCouplingAngle);
        float stop = Mathf.Abs(Truck.LastCouplingOver);
        backingAlignment = lateral;
        backingStopDistance = stop;
        bool pass = backingContacts == 0 && lateral < 0.25f && angle < 10f && stop < 0.8f;
        backingPassed = pass;
        backingResult.text = (pass ? "BACKING PASS" : "BACKING NEEDS WORK")
            + "\nCONTACTS " + backingContacts + " · ALIGN " + lateral.ToString("F2") + " M / " + angle.ToString("F0") + "°"
            + "\nSTOP OFFSET " + stop.ToString("F2") + " M · PULL-UPS " + backingAttempts
            + (pass ? "" : "\nPRESS R OR RETRY TO IMPROVE THE SCORE");
        ShowSubtitle(pass ? "Clean backing. The kingpin is locked." : "The backing needs work. Retry to improve the score.", 4f);
        if (Player != null) Player.ReleaseCursor();
        Debug.Log("D2D BACKING pass=" + pass + " contacts=" + backingContacts
            + " lateral=" + lateral.ToString("F2") + " angle=" + angle.ToString("F1")
            + " stop=" + stop.ToString("F2") + " pullUps=" + backingAttempts);
    }

    void HideBackingLesson()
    {
        backingActive = false;
        backingResultShown = false;
        backingWasReversing = false;
        if (backingPanel != null) backingPanel.SetActive(false);
    }

    public void RetryBacking()
    {
        if (Rig == null || Truck == null || paused || ended) return;
        Truck.ResetForBacking(backingStartPos, backingStartRot);
        Rig.ResetForRetry();
        backingContacts = 0;
        backingAttempts = 0;
        backingResultShown = false;
        backingWasReversing = false;
        backingRefresh = 0f;
        backingActive = true;
        backingResult.text = "LESSON RESET\nPull up, then reverse slowly under the kingpin.";
        if (backingPanel != null) backingPanel.SetActive(true);
        ShowSubtitle("Backing lesson reset. Pull up, then reverse slowly under the kingpin.", 4f);
        Debug.Log("D2D BACKING retry");
    }

    void UpdateBackingLesson()
    {
        var rig = Rig;
        if (rig == null || Truck == null || !started || ended || !gatePassed || Player == null || !Player.IsSeated)
        {
            if (backingActive) HideBackingLesson();
            return;
        }
        if (rig.coupled)
        {
            if (backingActive && !backingResultShown) FinishBackingLesson();
            return;
        }
        bool near = Vector3.Distance(Truck.transform.position, rig.transform.position) < 45f;
        if (!near)
        {
            if (backingActive) HideBackingLesson();
            return;
        }
        if (!backingActive) StartBackingLesson();
        bool reversing = Truck.speed < -0.15f;
        if (backingWasReversing && Truck.speed > 0.4f) backingAttempts++;
        backingWasReversing = reversing;
        backingRefresh -= Time.unscaledDeltaTime;
        if (backingRefresh > 0f) return;
        backingRefresh = 0.15f;
        Vector3 fifth = Truck.transform.TransformPoint(Truck.FifthWheelLocal);
        float dist = Vector3.Distance(fifth, rig.KingpinWorld);
        float align = Vector3.Angle(Truck.transform.forward, rig.Forward);
        int mph = Mathf.RoundToInt(Mathf.Abs(Truck.speed) * 2.23694f);
        backingText.text = "BACKING LESSON\nTO KINGPIN " + dist.ToString("F1") + " M · ALIGN " + align.ToString("F0")
            + "° · SPEED " + mph + " MPH\nCONTACTS " + backingContacts + " · PULL-UPS " + backingAttempts
            + " · RETRY RESETS THE LESSON";
    }

    public void OnGateApproach(TruckController truck)
    {
        if (gatePassed || guardOpen || ended) return;
        CompleteDeadhead();
        guardOpen = true;
        truck.controlLock = true;
        truck.speed = 0f;
        Audio.PlayVoice("guard_01");
        ShowGuard();
    }

    void ShowGuard()
    {
        guardPanel.SetActive(true);
        if (Player != null) Player.SetUILock(true);
        if (mobileControls != null && mobileControls.canvas != null) mobileControls.canvas.gameObject.SetActive(false);
        guardText.text = "Morning, driver. I need your appointment time, the shipper, and the seal number before I raise this arm.";
        SetObjective("Check in at Gate 1.");
    }

    public void GuardAnswer(bool correct)
    {
        Debug.Log("D2D GUARD answer=" + correct + " open=" + guardOpen + " passed=" + gatePassed);
        if (!correct)
        {
            guardWrongAttempts++;
            guardText.text = "That's not what's on my sheet. Check the load app and give me the correct appointment and shipper.";
            Audio.PlayVoice("guard_01");
            return;
        }
        guardOpen = false;
        gatePassed = true;
        World.gate.SetOpen(true);
        Audio.PlaySfx("gate_motor", 0.8f);
        Truck.controlLock = false;
        Audio.PlayVoice("guard_02");
        guardPanel.SetActive(false);
        if (Player != null) Player.SetUILock(false);
        if (mobileControls != null && mobileControls.canvas != null) mobileControls.canvas.gameObject.SetActive(true);
        ReturnToPause();
        ShowSubtitle("Gate open. Trailer A-14 is staged on the right. Pull in, stop, and press X to exit.", 6f);
        SetObjective("Drive into the yard. Stop by A-14, then press X to exit.");
        Debug.Log("D2D GUARD passed gate");
    }

    public void OnInspect(InspectionPoint point)
    {
        if (point == null || point.inspected) return;
        point.Mark();
        inspected++;
        ShowSubtitle(point.title + "\n" + point.details, 6f);
        if (point.id == "seal")
        {
            SetObjective("Photograph seal 482913 with your phone.  [TAB]");
        }
        else if (inspected < 5)
        {
            SetObjective("Continue inspecting trailer A-14.  " + inspected + "/5");
        }
        if (inspected >= 5)
        {
            if (sealPhotoSent) PreTripDone();
            else SetObjective("Seal inspection remains. Take a photo with your phone.");
        }
    }

    public void OnSealPhotoSent(Texture2D tex)
    {
        sealPhotoSent = true;
        Phone.AddProofChat();
        Phone.Close();
        ShowSubtitle("SEAL VERIFIED  ·  482913\nDispatch has the proof.", 4f);
        SetObjective("Seal verified. Finish the remaining inspections.");
        if (inspected >= 5) PreTripDone();
    }

    void PreTripDone()
    {
        Debug.Log("D2D FLOW inspection done");
        if (Rig != null && !Rig.coupled)
            ShowSubtitle("INSPECTION COMPLETE\nNow hook it: line up in front of A-14's nose and back under it slowly.", 5f);
        RefreshObjective();
    }

    IEnumerator EndAfter(float t)
    {
        yield return new WaitForSeconds(t);
        EndShift();
    }

    void EndShift()
    {
        if (ended) return;
        ended = true;
        guardOpen = false;
        Truck.controlLock = true;
        Player.SetUILock(true);
        ClearSubtitle();
        SetPrompt(null);
        SetInspectionMarkers(false);
        if (hudBase != null) foreach (var go in hudBase) if (go != null) go.SetActive(false);
        endDim.SetActive(true);
        endPanel.SetActive(true);
        if (mobileControls != null && mobileControls.canvas != null) mobileControls.canvas.gameObject.SetActive(false);
        int total = ShiftClockMinutes;
        int minutes = Mathf.FloorToInt(playTime / 60f);
        int secs = Mathf.FloorToInt(playTime % 60f);
        endKicker.text = "SHIFT 01  ·  DALLAS, TX  ·  " + ClockString(total);
        endSub.text = "Trailer A-14 is hooked, sealed, and outbound for Houston.";
        endInspect.text = inspected + "/5";
        endSeal.text = "482913";
        endTime.text = minutes + ":" + secs.ToString("00");
        if (endNote != null)
            endNote.text = "DEADHEAD " + deadheadMiles.ToString("F2") + " / " + DeadheadAssignedMiles
                + " MI DRIVEN · ARR " + (deadheadArrived ? deadheadArrivalClock : "--:--")
                + "   ·   LOADED 240 MI · $2,450";
        if (endDebrief != null)
        {
            endDebrief.text = BuildDebrief();
            Debug.Log("D2D DEBRIEF " + endDebrief.text);
        }
    }

    // Safety, Procedure and Efficiency are reported separately. A fast or
    // efficient run never buys back a contact: the overall line is a safety
    // hold whenever any of those events were recorded.
    // PLACEHOLDER: confirm policy — the zero-contact standard and the score
    // weights below are placeholders until the safety department signs off.
    string BuildDebrief()
    {
        var rig = Rig;
        int safety = Mathf.Clamp(100 - 25 * contactCount - 10 * hardBrakeCount
            - 20 * engineRunningExits - 15 * backingContacts, 0, 100);
        int steps = 8;
        int done = (loadVerified ? 1 : 0)
            + (inspected >= 5 ? 1 : 0)
            + (sealPhotoSent ? 1 : 0)
            + (gatePassed ? 1 : 0)
            + (rig != null && rig.tugPassed ? 1 : 0)
            + (rig != null && rig.linesConnected ? 1 : 0)
            + (rig != null && rig.gearUp ? 1 : 0)
            + (rig != null && rig.airSupplied ? 1 : 0);
        int procedure = Mathf.RoundToInt(100f * done / steps);
        float efficiency = DeadheadAssignedMiles > 0
            ? Mathf.Clamp(deadheadMiles / DeadheadAssignedMiles * 100f, 0f, 100f) : 0f;
        string overall = "READY TO ROLL";
        if (contactCount > 0 || hardBrakeCount > 0 || engineRunningExits > 0 || backingContacts > 0)
            overall = "SAFETY HOLD — SPEED DOES NOT OFFSET CONTACTS";
        else if (backingResultShown && !backingPassed)
            overall = "BACKING NEEDS WORK";
        else if (done < steps || !deadheadArrived)
            overall = "PROCEDURE INCOMPLETE";
        return "SAFETY " + safety + "%   ·   PROCEDURE " + procedure + "%   ·   EFFICIENCY " + Mathf.RoundToInt(efficiency) + "%\n"
            + "CONTACTS " + contactCount + " · HARD BRAKES " + hardBrakeCount + " · ENGINE-RUNNING EXITS " + engineRunningExits
            + " · BACKING CONTACTS " + backingContacts + " · KINGPIN MISSES " + kingpinMisses + "\n"
            + "INSPECTIONS " + inspected + "/5 · SEAL " + (sealPhotoSent ? "SENT" : "NOT SENT")
            + " · GATE " + (gatePassed ? "CHECKED IN" : "MISSED") + " (" + guardWrongAttempts + " WRONG ANSWERS)"
            + " · DEADHEAD " + (deadheadArrived ? (deadheadOnTime ? "ON TIME" : "LATE") : "NOT ARRIVED")
            + " · BACKING " + (backingPassed ? "PASS" : backingResultShown ? "NEEDS WORK" : "NOT ATTEMPTED")
            + "   ·   COMPANY STANDARD: 0 CONTACTS [PLACEHOLDER: confirm policy]   ·   " + overall;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void TogglePhone()
    {
        if (Phone == null || guardOpen || ended) return;
        if (Phone.IsOpen) Phone.Close();
        else
        {
            if (Player.IsSeated && Mathf.Abs(Truck.speed) > 0.4f) return;
            Phone.Open("load");
        }
    }

    public void OpenCamera()
    {
        if (Phone == null || guardOpen || ended) return;
        Phone.ShowCamera(World.sealTarget);
    }

    public void Interact()
    {
        if (Interaction != null) Interaction.TryInteract();
    }

    public void ExitTruck()
    {
        if (Player != null && Player.IsSeated && Truck.speed < 0.4f) Truck.Exit();
    }

    public void SetGoal(string text) { SetObjective(text); }

    public void SetObjective(string text)
    {
        currentGoal = text;
        if (objectiveText != null) objectiveText.text = text;
    }

    public void SetPrompt(string text)
    {
        if (promptText == null) return;
        promptPanel.SetActive(!string.IsNullOrEmpty(text));
        promptText.text = text ?? "";
        if (promptPanel.activeSelf) UpdatePromptPosition();
    }

    // Anchor the use prompt just above the object the centre ray is aimed at.
    // When there is no target it sits above the driver's aim point, never in
    // the subtitle strip at the bottom.
    void UpdatePromptPosition()
    {
        if (promptPanel == null || !promptPanel.activeSelf || promptRect == null || Camera == null) return;
        Vector3 world;
        if (Interaction != null && Interaction.HasPrompt) world = Interaction.PromptPoint;
        else if (Player != null) world = Player.InteractOrigin + Camera.transform.forward * 2.4f;
        else world = Camera.transform.position + Camera.transform.forward * 2.4f;
        Vector3 vp = Camera.WorldToViewportPoint(world);
        var canvasRt = (RectTransform)hudRoot.transform;
        Vector2 pos;
        if (vp.z < 0f) pos = new Vector2(canvasRt.rect.width * 0.5f, 140f);
        else pos = new Vector2(vp.x * canvasRt.rect.width, vp.y * canvasRt.rect.height + 52f);
        pos.x = Mathf.Clamp(pos.x, 280f, canvasRt.rect.width - 280f);
        pos.y = Mathf.Clamp(pos.y, 120f, canvasRt.rect.height - 100f);
        promptRect.anchoredPosition = pos;
    }

    // ---- warnings -------------------------------------------------------
    // Warnings have their own panel, their own lifetime, and a priority.
    // A routine line can never replace a live higher-priority warning.
    public void ShowWarning(string text, float seconds = 3f, int priority = 1)
    {
        if (warningPanel == null || warningText == null) return;
        float now = Time.unscaledTime;
        if (warningPanel.activeSelf && now < warningUntil && warningPriority > priority) return;
        warningPriority = priority;
        warningUntil = now + Mathf.Max(0.5f, seconds);
        warningText.text = text ?? "";
        warningPanel.SetActive(true);
        var img = warningPanel.GetComponent<Image>();
        if (img != null)
            img.color = priority >= 3 ? new Color(0.46f, 0.07f, 0.035f, 0.96f)
                      : priority == 2 ? new Color(0.34f, 0.17f, 0.025f, 0.96f)
                      : new Color(0.05f, 0.13f, 0.19f, 0.94f);
        if (warningIconBg != null)
            warningIconBg.color = priority >= 3 ? new Color(1f, 0.32f, 0.20f)
                                : priority == 2 ? new Color(1f, 0.64f, 0.16f)
                                : new Color(0.38f, 0.78f, 1f);
        if (warningIconText != null) warningIconText.text = priority >= 3 ? "!" : priority == 2 ? "!" : "i";
        if (warningRoutine != null) StopCoroutine(warningRoutine);
        warningRoutine = StartCoroutine(HideWarning(warningUntil));
    }

    IEnumerator HideWarning(float until)
    {
        while (Time.unscaledTime < until) yield return null;
        if (Time.unscaledTime >= warningUntil - 0.01f)
        {
            if (warningPanel != null) warningPanel.SetActive(false);
            warningPriority = 0;
            warningRoutine = null;
        }
    }

    // ---- pause / cursor -------------------------------------------------
    public bool IsPaused => paused;

    public void PauseGame()
    {
        if (!started || ended) { if (Player != null) Player.ReleaseCursor(); return; }
        if (Player != null) Player.ReleaseCursor();
        if (IsAutomation) return;
        // A menu owns the mouse and keeps time running while it is open (the
        // dispatch call is timed). It returns to pause when it closes.
        if ((Phone != null && Phone.IsOpen) || guardOpen) return;
        if (paused) return;
        paused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);
        if (focusHint != null) focusHint.gameObject.SetActive(false);
        SetPrompt(null);
    }

    // Called when a menu closes. It never captures the mouse on its own: the
    // game returns to the paused/released state and waits for an explicit click.
    public void ReturnToPause()
    {
        if (Player != null) Player.ReleaseCursor();
        if (!started || ended) return;
        if (IsAutomation) return;
        PauseGame();
    }

    void ChangeSensitivity(float delta)
    {
        if (Player == null) return;
        Player.AdjustSensitivity(delta);
        RefreshSensitivityLabel();
    }

    void RefreshSensitivityLabel()
    {
        if (sensitivityText != null && Player != null)
            sensitivityText.text = "MOUSE SENSITIVITY  " + Player.Sensitivity.ToString("F1");
    }

    public void ResumeGame()
    {
        if (ended) return;
        if (Phone != null && Phone.IsOpen) return;
        if (guardOpen) return;
        paused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (focusHint != null) focusHint.gameObject.SetActive(false);
        if (Player != null) Player.CaptureCursor();
    }

    public void SetHudVisible(bool visible)
    {
        if (hudCanvas != null) hudCanvas.gameObject.SetActive(visible);
        if (mobileControls != null && mobileControls.canvas != null) mobileControls.canvas.gameObject.SetActive(visible);
    }

    public void ShowSubtitle(string text, float seconds = 4f)
    {
        if (subtitleText == null) return;
        if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
        subtitlePanel.SetActive(true);
        subtitleText.text = text;
        subtitleRoutine = StartCoroutine(HideSubtitle(seconds));
    }

    public void ClearSubtitle()
    {
        if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
        subtitleRoutine = null;
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
    }

    public void SetInspectionMarkers(bool visible)
    {
        foreach (var ip in FindObjectsOfType<InspectionPoint>(true))
        {
            if (ip.markerRoot != null) ip.markerRoot.gameObject.SetActive(visible && !ip.inspected);
        }
    }

    IEnumerator HideSubtitle(float t)
    {
        yield return new WaitForSeconds(t);
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        subtitleRoutine = null;
    }

    // Frame-time log every 5 s: average and worst frame, so lag reports can be
    // checked against Player.log.
    float perfT, perfWorst; int perfN;
    void LogPerf()
    {
        float dt = Time.unscaledDeltaTime;
        perfT += dt; perfN++; if (dt > perfWorst) perfWorst = dt;
        if (perfT < 5f) return;
        Debug.Log($"D2D PERF fps={perfN / perfT:F1} avgMs={1000f * perfT / perfN:F1} worstMs={1000f * perfWorst:F1} res={Screen.width}x{Screen.height}");
        perfT = 0f; perfN = 0; perfWorst = 0f;
    }

    // Waypoint: a small diamond and distance over the current goal, clamped to
    // the screen edge when it's behind or off screen, as open-world games do.
    // The play test spent 48 of 72 s wandering the lot looking for the truck.
    Text controlsText;
    RectTransform waypoint;
    Text waypointDist;
    bool waypointSeated = true;
    void BuildWaypoint()
    {
        var wp = UIFactory.Rect("Waypoint", hudRoot.transform);
        wp.anchorMin = wp.anchorMax = Vector2.zero; wp.pivot = new Vector2(0.5f, 0.5f);
        wp.sizeDelta = new Vector2(40, 40);
        var d = UIFactory.Panel(wp, "Diamond", new Vector2(22, 22), Vector2.zero, new Color(1f, 0.62f, 0.16f, 0.92f), false, 3);
        d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        d.raycastTarget = false;
        waypointDist = UIFactory.Label(wp, "Dist", "", 20, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperCenter, FontStyle.Bold);
        waypointDist.rectTransform.anchoredPosition = new Vector2(0, -34);
        waypointDist.rectTransform.sizeDelta = new Vector2(140, 30);
        waypointDist.horizontalOverflow = HorizontalWrapMode.Overflow;
        waypoint = wp;
        wp.gameObject.SetActive(false);
    }

    Vector3? GateWaypoint()
    {
        if (World == null) return null;
        if (World.gateWaypoint != null) return World.gateWaypoint.position;
        return World.gate != null ? World.gate.transform.position + Vector3.up * 3f : (Vector3?)null;
    }

    Vector3? WaypointTarget()
    {
        if (!started || ended || World == null || Truck == null) return null;
        if (Phone != null && Phone.IsOpen) return null;
        bool seated = Player != null && Player.IsSeated;
        if (!gatePassed)
        {
            if (!seated) return Truck.transform.TransformPoint(new Vector3(-1.95f, 1.9f, 1.55f));
            if (!Truck.engineOn) return null;
            return GateWaypoint();
        }
        var rig = Rig;
        if (rig != null && rig.coupled)
        {
            if (!rig.tugPassed) return null;
            if (!rig.linesConnected || !rig.gearUp)
            {
                if (seated) return null;
                return !rig.linesConnected ? Truck.transform.TransformPoint(new Vector3(-1.78f, 1.8f, -3.0f)) : rig.CrankWorld + Vector3.up * 0.4f;
            }
            if (!seated) return Truck.transform.TransformPoint(new Vector3(-1.95f, 1.9f, 1.55f));
            if (!rig.airSupplied) return null;
            return GateWaypoint();
        }
        if (InspectionDone && rig != null)
        {
            // line up in front of the nose, but keep the marker off the trailer
            // body so it never reads as a decal on the rig
            if (!seated) return Truck.transform.TransformPoint(new Vector3(-1.95f, 1.9f, 1.55f));
            return rig.KingpinWorld + rig.Forward * 9f + rig.transform.right * 2.8f + Vector3.up * 1.2f;
        }
        if (seated) return World.targetTrailer != null
            ? World.targetTrailer.position + World.targetTrailer.right * 3.2f + Vector3.up * 1.2f
            : (Vector3?)null;
        foreach (var ip in World.inspections)
            if (ip != null && !ip.inspected) return ip.transform.position + Vector3.up * 0.6f;
        return null;
    }

    void UpdateWaypoint()
    {
        if (waypoint == null || Camera == null) return;
        if (paused) { if (waypoint.gameObject.activeSelf) waypoint.gameObject.SetActive(false); return; }
        bool seated = Player != null && Player.IsSeated;
        if (controlsText != null && seated != waypointSeated)
        {
            waypointSeated = seated;
            controlsText.text = seated ? "W/S DRIVE  ·  A/D STEER  ·  V CAMERA  ·  X EXIT"
                                       : "WASD MOVE  ·  SHIFT RUN  ·  V CAMERA  ·  E USE";
        }
        var t = WaypointTarget();
        if (t == null) { if (waypoint.gameObject.activeSelf) waypoint.gameObject.SetActive(false); return; }
        Vector3 vp = Camera.WorldToViewportPoint(t.Value);
        // Distance is measured from the driver, not the chase camera, so the
        // number agrees with interaction reach.
        Vector3 driver = Player != null ? Player.InteractOrigin : Camera.transform.position;
        float dist = Vector3.Distance(driver, t.Value);
        float reach = Interaction != null ? Interaction.distance : 3.4f;
        // hide once the goal is within reach of the driver
        bool show = dist > (seated ? 14f : reach);
        if (waypoint.gameObject.activeSelf != show) waypoint.gameObject.SetActive(show);
        if (!show) return;
        if (vp.z < 0f) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; vp.y = 0.08f; }
        vp.x = Mathf.Clamp(vp.x, 0.04f, 0.96f);
        vp.y = Mathf.Clamp(vp.y, 0.08f, 0.9f);
        // Keep the marker and its distance label clear of the TAB PHONE hint
        // in the lower-right corner.
        if (vp.x > 0.72f && vp.y < 0.18f) { vp.x = 0.72f; vp.y = 0.18f; }
        var canvasRt = (RectTransform)hudRoot.transform;
        waypoint.anchoredPosition = new Vector2(vp.x * canvasRt.rect.width, vp.y * canvasRt.rect.height);
        int m = Mathf.RoundToInt(dist);
        if (waypointDist.text.Length == 0 || lastWaypointM != m) { waypointDist.text = m + " m"; lastWaypointM = m; }
    }
    int lastWaypointM = -1;
    int speedoKey = -1;

    // ---- deadhead -------------------------------------------------------
    // 1 world unit is 1 metre. The odometer is therefore the distance actually
    // driven, never a scale factor onto a shorter road.
    float DeadheadRouteMiles => deadheadRouteLength / MetresPerMile;
    float MilesPerUnit => 1f / MetresPerMile;
    float DeadheadRemainingMiles => Mathf.Max(0f, DeadheadAssignedMiles - deadheadMiles);
    float RouteRemainingMiles => Mathf.Max(0f, DeadheadRouteMiles - deadheadMiles);
    int ShiftClockMinutes => 5 * 60 + 40 + Mathf.FloorToInt(playTime / 60f);
    string ClockNow => ClockString(ShiftClockMinutes);
    string DeadheadEta => ClockString(ShiftClockMinutes + Mathf.CeilToInt(RouteRemainingMiles * 60f / DeadheadMph));
    static string ClockString(int total) => (total / 60 % 24).ToString("00") + ":" + (total % 60).ToString("00");

    void UpdateDeadhead()
    {
        if (Truck == null) return;
        Vector3 p = Truck.transform.position;
        if (!started || ended || gatePassed || deadheadArrived)
        {
            deadheadLastPos = p;
            return;
        }
        float step = FlatXZ(p - deadheadLastPos).magnitude;
        deadheadLastPos = p;
        // A teleport or a scene cut is not driving. Allow for low frame rates:
        // at 10 fps and 25 m/s a real frame covers 2.5 m, and that distance
        // still belongs on the odometer. Only a jump far beyond the truck's
        // own speed is treated as a teleport.
        float maxStep = Mathf.Max(1.5f, Mathf.Abs(Truck.speed) * Time.deltaTime * 2f);
        if (step > maxStep) return;
        if (!deadheadActive)
        {
            if (!Truck.engineOn || step < 0.0005f) return;
            // Departure is when the truck first rolls, not when the key turns.
            deadheadActive = true;
            deadheadDepartClock = ClockNow;
            deadheadDepartMinutes = ShiftClockMinutes;
        }
        if (step < 0.0005f) return;
        deadheadWorld += step;
        deadheadMiles = deadheadWorld * MilesPerUnit;
        // No clock compression: playTime already advances in real time, and the
        // odometer now reports the metres the truck actually covered.
    }

    void UpdateTripHud()
    {
        if (tripPanel == null) return;
        bool hold = deadheadArrived && Time.unscaledTime < deadheadHoldUntil;
        bool show = !ended && (hold || (started && deadheadActive && !deadheadArrived && !gatePassed
            && Truck != null && Truck.engineOn));
        if (tripPanel.activeSelf != show) tripPanel.SetActive(show);
        if (!show) return;
        if (hold)
        {
            tripKicker.text = "DEADHEAD COMPLETE · DFW9";
            tripMiles.text = deadheadMiles.ToString("F2") + " MI";
            tripDetail.text = "DRIVEN · ROUTE " + DeadheadRouteMiles.ToString("F2") + " MI · ASSIGNED " + DeadheadAssignedMiles + " MI";
            tripEta.text = "ODOMETER = DISTANCE ACTUALLY DRIVEN";
            return;
        }
        tripRefresh -= Time.unscaledDeltaTime;
        if (tripRefresh > 0f) return;
        tripRefresh = 0.12f;
        tripKicker.text = "DEADHEAD · DFW9";
        tripMiles.text = deadheadMiles.ToString("F2") + " MI";
        tripDetail.text = "DRIVEN · ROUTE " + DeadheadRouteMiles.ToString("F2") + " MI OF " + DeadheadAssignedMiles + " ASSIGNED";
        tripEta.text = "DEP " + deadheadDepartClock + " · ETA " + DeadheadEta + " · 1 UNIT = 1 M";
    }

    void CompleteDeadhead()
    {
        if (deadheadArrived) return;
        deadheadArrived = true;
        deadheadActive = false;
        deadheadArrivalClock = ClockNow;
        int scheduledEta = deadheadDepartMinutes + Mathf.CeilToInt(DeadheadRouteMiles * 60f / DeadheadMph);
        // A short or forced deadhead is not "on time": the assigned miles must
        // actually be on the odometer.
        deadheadOnTime = deadheadMiles >= DeadheadAssignedMiles - 0.05f
            && ShiftClockMinutes <= scheduledEta;
        bool realRun = deadheadWorld > 5f;
        if (realRun) deadheadHoldUntil = Time.unscaledTime + 7f;
        UpdateTripHud();
        Debug.Log("D2D DEADHEAD assigned=" + DeadheadAssignedMiles + " routeMiles=" + DeadheadRouteMiles.ToString("F3")
            + " drivenUnits=" + deadheadWorld.ToString("F1") + " miles=" + deadheadMiles.ToString("F3")
            + " remainingAssigned=" + DeadheadRemainingMiles.ToString("F2") + " arrival=" + deadheadArrivalClock);
        if (!realRun) return;
        ShowSubtitle("DEADHEAD COMPLETE · " + deadheadMiles.ToString("F2") + " MI DRIVEN\n"
            + "Gate 1 · Amazon DFW9. Check in with the guard.", 6f);
    }

    // Render scale. On a Retina Mac the window is ~2940 px wide and the GPU
    // can't hold 60 fps there (play test: 21-43 fps, 200-600 ms spikes). The 3D
    // view renders at a lower internal size (Metal dynamic resolution) while
    // the UI stays sharp. Starts at ~1800 px wide and steps down if the frame
    // rate stays low, back up when there is headroom.
    float renderScale = 1f, rsT; int rsN; float rsCap = 1f;
    bool rsInit;
    void UpdateRenderScale()
    {
        if (Camera == null) return;
        if (!rsInit)
        {
            rsInit = true;
            Camera.allowDynamicResolution = true;
            rsCap = Mathf.Clamp(1800f / Mathf.Max(1, Screen.width), 0.5f, 1f);
            string forced = ArgValue("-renderscale");
            if (forced != null && float.TryParse(forced, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                rsCap = Mathf.Clamp(f, 0.4f, 1f);
            renderScale = rsCap;
            ScalableBufferManager.ResizeBuffers(renderScale, renderScale);
            Debug.Log("D2D RENDERSCALE start=" + renderScale.ToString("F2") + " applied=" + ScalableBufferManager.widthScaleFactor.ToString("F2") + " screen=" + Screen.width);
            return;
        }
        if (IsAutomation || !Application.isFocused) return;
        rsT += Time.unscaledDeltaTime; rsN++;
        if (rsT < 2.5f) return;
        float fps = rsN / rsT; rsT = 0f; rsN = 0;
        float cap = Mathf.Clamp(1800f / Mathf.Max(1, Screen.width), 0.5f, 1f);
        float next = renderScale;
        if (fps < 50f) next = Mathf.Max(0.5f, renderScale - 0.08f);
        else if (fps > 58.5f) next = Mathf.Min(cap, renderScale + 0.04f);
        if (Mathf.Abs(next - renderScale) > 0.001f)
        {
            renderScale = next;
            ScalableBufferManager.ResizeBuffers(renderScale, renderScale);
            Debug.Log("D2D RENDERSCALE " + renderScale.ToString("F2") + " fps=" + fps.ToString("F1"));
        }
    }

    void Update()
    {
        LogPerf();
        UpdateRenderScale();
        UpdateWaypoint();
        UpdatePromptPosition();
        if (!started)
        {
            // The start screen must work without a mouse. A lot of players launch a
            // windowed build and type before clicking it; accept Enter or Space.
            if (!ended && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
                BeginShift();
            return;
        }
        if (ended) return;
        if (paused)
        {
            // The pause overlay is the only thing that can resume play. Menu
            // panels keep the cursor for themselves; a click there must not
            // silently capture the mouse again.
            bool menuOpen = (Phone != null && Phone.IsOpen) || guardOpen;
            if (!menuOpen && Input.GetMouseButtonDown(0)
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                if (!Application.isFocused) ActivateSelf();
                ResumeGame();
            }
            return;
        }
        playTime += Time.deltaTime;
        UpdateDeadhead();
        UpdateTripHud();
        UpdateCoupling();
        if (clockText != null) clockText.text = ClockNow;
        if (Truck != null)
        {
            Truck.controlLock = (Phone != null && Phone.IsOpen) || guardOpen || ended;
            // Hard braking is a safety line in the debrief, not per-frame noise.
            if (Player != null && Player.IsSeated && !ended && Mathf.Abs(Truck.speed) > 4.5f
                && Truck.LongAccel < -4.5f && Time.time - lastHardBrakeAt > 3f)
            {
                lastHardBrakeAt = Time.time;
                hardBrakeCount++;
                ShowWarning("HARD BRAKING. Smooth, progressive pressure keeps the load and the tyres happy.", 3f, 1);
                Debug.Log("D2D HARDBRAKE decel=" + Truck.LongAccel.ToString("F1") + " count=" + hardBrakeCount);
            }
            if (speedPanel != null)
            {
                bool show = Player != null && Player.IsSeated && !ended;
                if (speedPanel.activeSelf != show) speedPanel.SetActive(show);
                if (show)
                {
                    // rebuild the strings only when the shown values change (no per-frame garbage)
                    int mph = Mathf.RoundToInt(Mathf.Abs(Truck.speed) * 2.23694f);
                    int rpm50 = Truck.engineOn ? Mathf.RoundToInt(Truck.Rpm / 50f) * 50 : 0;
                    int key = mph * 1000000 + (Truck.GearKey + 2) * 10000 + rpm50;
                    if (key != speedoKey)
                    {
                        speedoKey = key;
                        speedText.text = mph.ToString();
                        gearText.text = !Truck.engineOn ? "P  ·  OFF" : Truck.GearLabel + "  ·  " + rpm50 + " RPM";
                    }
                }
            }
        }
        bool uiWantsCursor = (Phone != null && Phone.IsOpen) || guardOpen;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Phone != null && Phone.IsOpen)
            {
                if (Phone.CameraActive) Phone.CloseCamera();
                else Phone.Close();
                return;
            }
            if (!guardOpen) { PauseGame(); return; }
        }
        if (backingResultShown && (Phone == null || !Phone.IsOpen) && Input.GetKeyDown(KeyCode.R))
            RetryBacking();
        if (uiWantsCursor) return;

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.P)) TogglePhone();
        if (Input.GetKeyDown(KeyCode.E)) Interact();
        if (Input.GetKeyDown(KeyCode.X)) ExitTruck();

        // Escape and right-click both release the mouse and pause. Only a
        // click on the pause overlay resumes; no menu close re-captures it.
        if (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(1))
        {
            PauseGame();
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked && !paused && Input.GetMouseButtonDown(0))
        {
            if (!Application.isFocused) ActivateSelf();
            Player.CaptureCursor();
        }
        if (focusHint != null)
        {
            bool showHint = !uiWantsCursor && !paused && Cursor.lockState != CursorLockMode.Locked;
            if (focusHint.gameObject.activeSelf != showHint) focusHint.gameObject.SetActive(showHint);
        }
    }

    void BuildStartScreen()
    {
        var c = UIFactory.Canvas("StartCanvas", 60, true, true);
        c.transform.SetParent(transform, false);
        var dim = UIFactory.Full(c.transform, "Dim", new Color(0.01f, 0.02f, 0.03f, 0.30f));
        dim.raycastTarget = true;
        var kicker = UIFactory.Label(c.transform, "Kicker", "DALLAS, TEXAS  ·  05:40", 26, new Color(1f, 0.52f, 0.12f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(kicker.rectTransform, new Vector2(80, -90), new Vector2(700, 40));
        var title = UIFactory.Label(c.transform, "Title", "DOCK\nTO DROP", 120, new Color(0.93f, 0.90f, 0.82f), TextAnchor.UpperLeft, FontStyle.Bold);
        title.lineSpacing = 0.82f;
        UIFactory.TopLeft(title.rectTransform, new Vector2(74, -140), new Vector2(900, 300));
        var sub = UIFactory.Label(c.transform, "Sub", "A FREIGHT DAY SIMULATOR", 30, new Color(0.72f, 0.80f, 0.86f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(sub.rectTransform, new Vector2(80, -450), new Vector2(800, 60));
        var copy = UIFactory.Label(c.transform, "Copy", "FREIGHTLINER CASCADIA  ·  DFW9  ·  DEADHEAD " + DeadheadAssignedMiles + " MI", 24, new Color(0.58f, 0.66f, 0.73f), TextAnchor.LowerLeft);
        var crt = copy.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0, 0);
        crt.pivot = new Vector2(0, 0);
        crt.anchoredPosition = new Vector2(80, 268);
        crt.sizeDelta = new Vector2(900, 50);

        // A visible build stamp: if this line is missing, the window is not the
        // build that contains the HUD, ground-contact and deadhead fixes.
        var build = UIFactory.Label(c.transform, "Build", "v" + Application.version + "  ·  BUILD 2026-11-10  ·  REAL DEADHEAD + STAGES 1-5", 18, new Color(0.46f, 0.56f, 0.63f), TextAnchor.LowerRight);
        var brt = build.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(1, 0);
        brt.anchoredPosition = new Vector2(-34, 30);
        brt.sizeDelta = new Vector2(720, 28);
        var start = UIFactory.Button(c.transform, "Start", "START SHIFT", new Vector2(430, 92), new Vector2(80, 120), new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 32, BeginShift);
        start.GetComponent<RectTransform>().anchorMin = start.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0);
        start.GetComponent<RectTransform>().anchoredPosition = new Vector2(295, 150);
        var quit = UIFactory.Button(c.transform, "Quit", "QUIT", new Vector2(200, 70), new Vector2(80, 70), new Color(0.10f, 0.16f, 0.20f, 0.9f), Color.white, 22, Quit);
        quit.GetComponent<RectTransform>().anchorMin = quit.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0);
        quit.GetComponent<RectTransform>().anchoredPosition = new Vector2(180, 70);
        startPanel = c.gameObject;
        startPanel.SetActive(true);
    }

    void BeginShift()
    {
        if (started) return;
        started = true;
        Debug.Log("D2D SHIFT start focused=" + Application.isFocused);
        if (!Application.isFocused) ActivateSelf();
        startPanel.SetActive(false);
        hudCanvas.gameObject.SetActive(true);
        Player.EndMenuView();
        Player.SetUILock(false);
        Player.CaptureCursor();
        Audio.StartMusic();
        Audio.PlaySfx("seat", 0.5f);
        StartCoroutine(Intro());
    }

    bool HasArg(string arg)
    {
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == arg) return true;
        return false;
    }

    static string ArgValue(string arg)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == arg) return a[i + 1];
        return null;
    }

    IEnumerator UIShotRoutine()
    {
        Debug.Log("D2D UISHOT start");
        yield return new WaitForSeconds(1.0f);
        BeginShift();
        ClearSubtitle();
        yield return new WaitForSeconds(0.5f);

        // Relay load screen exactly as dispatch leaves it.
        SetObjective("Review the load on your phone.  [TAB]");
        Phone.Open("load");
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(Shot("08_load"));

        Phone.ShowTab(1);
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(Shot("09_chat"));

        Phone.ShowTab(2);
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(Shot("10_photos"));

        // Incoming dispatch call and connected state.
        Phone.Close();
        Ring();
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(Shot("11_call"));

        Phone.BeginCallAnswered();
        Phone.SetCallLine("Morning, driver. It's Mike. You awake? I've got a loaded fifty-three footer out of the Amazon fulfillment center in Dallas. Good paying run.");
        Phone.SetCallNext(null);
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(Shot("12_call_connected"));
        Phone.EndCall();

        // Phone camera and photo preview at the seal. Mark the five points first so
        // the staged proof gallery matches a completed walk-around.
        foreach (var ip in World.inspections) ip.Mark();
        var seal = World.sealTarget;
        if (seal != null)
        {
            Vector3 sealPlate = seal.transform.position + World.container.TransformDirection(new Vector3(0f, -0.05f, -0.26f));
            Vector3 sealStand = sealPlate + World.container.TransformDirection(new Vector3(0f, 0f, 0.8f));
            sealStand.y = 0.2f;
            Player.Teleport(sealStand, 0f);
            yield return new WaitForSeconds(0.25f);
            Player.LookAt(sealPlate);
            SetObjective("Photograph seal 482913 with your phone.  [TAB]");
            yield return new WaitForSeconds(0.25f);
            OpenCamera();
            yield return new WaitForSeconds(0.7f);
            yield return StartCoroutine(Shot("13_camera"));

            Phone.Capture();
            yield return new WaitForSeconds(1.2f);
            Debug.Log("D2D UISHOT previewReady=" + Phone.PreviewReady);
            yield return StartCoroutine(Shot("14_preview"));

            Phone.SendPhoto();
            yield return new WaitForSeconds(0.5f);
            Phone.Open("chat");
            yield return new WaitForSeconds(0.4f);
            yield return StartCoroutine(Shot("15_proof_chat"));
            Phone.ShowTab(2);
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(Shot("16_proof_photo"));
            Phone.Close();
        }

        // End-of-shift panel with completed results. Stage a completed
        // deadhead so the summary reads like a real shift instead of 0.0 mi.
        yield return new WaitForSeconds(0.3f);
        inspected = 5;
        sealPhotoSent = true;
        int deadheadMinutes = Mathf.CeilToInt(DeadheadRouteMiles * 60f / DeadheadMph);
        deadheadMiles = DeadheadRouteMiles;
        deadheadWorld = deadheadRouteLength;
        deadheadArrived = true;
        deadheadArrivalClock = ClockString(5 * 60 + 40 + deadheadMinutes);
        playTime = (deadheadMinutes + 5) * 60f;
        EndShift();
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(Shot("17_end"));

        Debug.Log("D2D UISHOT done");
        Application.Quit();
    }

    // -menushot: capture the title card as it really appears on launch (the
    // composed menu camera, not a staged CineShot) then quit. Used to verify
    // the driver-mesh clipping fix without touching gameplay.
    IEnumerator MenuShotRoutine()
    {
        Debug.Log("D2D MENUSHOT start");
        yield return new WaitForSeconds(1.4f);
        yield return StartCoroutine(Shot("menu"));
        Debug.Log("D2D MENUSHOT done");
        Application.Quit();
    }

    IEnumerator AutoShotRoutine()
    {
        Debug.Log("D2D AUTOSHOT start");
        float dz = World.destination != null ? World.destination.position.z : 0f;
        yield return new WaitForSeconds(1.6f);
        yield return StartCoroutine(CineShot("01_start",
            new Vector3(-9.8f, 1.18f, 16.4f), new Vector3(0f, 2.95f, 8.0f), 50f));
        startPanel.SetActive(false);
        started = true;
        hudCanvas.gameObject.SetActive(true);
        Player.EndMenuView();
        Player.SetUILock(false);
        Player.CaptureCursor();
        yield return new WaitForSeconds(0.7f);
        yield return StartCoroutine(CineShot("02_truckstop",
            new Vector3(-8.0f, 1.55f, 18.0f), new Vector3(0f, 2.05f, 8.0f), 52f));

        // close look at the store front: lit interior, signs, puddles
        yield return StartCoroutine(CineShot("02c_store",
            new Vector3(-8.5f, 1.6f, 16.0f), new Vector3(-20f, 1.4f, 0.5f), 50f));
        // parked rigs and a lot-light pole up close (material and placement check)
        yield return StartCoroutine(CineShot("02f_rig_east",
            new Vector3(25f, 1.9f, -11f), new Vector3(34f, 1.7f, -23f), 46f));
        yield return StartCoroutine(CineShot("02g_rig_west",
            new Vector3(-33f, 1.9f, -55f), new Vector3(-42f, 1.7f, -42f), 46f));
        yield return StartCoroutine(CineShot("02h_pole",
            new Vector3(13f, 1.7f, -10f), new Vector3(20f, 7.0f, -18f), 55f));

        // the driver: a composed full-body frame, then the over-the-shoulder view
        Player.Teleport(new Vector3(-3.6f, 0.2f, 12.6f), 205f);
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(CineShot("02d_driver",
            new Vector3(-6.6f, 1.45f, 16.4f), new Vector3(-3.4f, 1.05f, 12.4f), 42f));
        Player.SetThirdPerson(true); Player.SetLook(160f, 8f);
        yield return new WaitForSeconds(0.8f);
        yield return StartCoroutine(Shot("02e_driver_tp"));
        Player.SetThirdPerson(false); Player.SetLook(205f, 0f);
        Truck.SetDoor(true);
        yield return new WaitForSeconds(0.8f);
        yield return StartCoroutine(CineShot("02b_door",
            new Vector3(-8.2f, 1.55f, 15.4f), new Vector3(-1.65f, 1.65f, 9.15f), 46f));
        Truck.Enter();
        yield return new WaitForSeconds(0.6f);
        Truck.SetDoor(false);
        yield return new WaitForSeconds(1.1f);
        yield return StartCoroutine(Shot("03_cab"));
        // Look-around test: left, right, up, down, then left with the door open.
        Player.SetLook(-80f, 6f); yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(Shot("03b_cab_left"));
        Player.SetLook(75f, 6f); yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(Shot("03c_cab_right"));
        Player.SetLook(0f, -40f); yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(Shot("03d_cab_up"));
        Player.SetLook(-20f, 50f); yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(Shot("03e_cab_down"));
        Player.SetLook(110f, 10f); yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(Shot("03f_cab_back"));
        Truck.SetDoor(true); Player.SetLook(-70f, 12f);
        yield return new WaitForSeconds(1.0f);
        yield return StartCoroutine(Shot("03g_cab_door_open"));
        yield return StartCoroutine(CineShot("03h_driver_seated",
            new Vector3(-2.3f, 2.85f, 15.2f), new Vector3(-0.62f, 2.35f, 8.4f), 34f));
        Truck.SetDoor(false); Player.SetLook(0f, 0f);
        yield return new WaitForSeconds(0.8f);

        // Stage the first leg so the deadhead HUD can be checked in a still.
        // The autoplay run exercises the real odometer separately.
        Truck.engineOn = true;
        deadheadActive = true;
        deadheadDepartClock = "05:40";
        deadheadWorld = deadheadRouteLength * 0.55f;
        deadheadMiles = deadheadWorld * MilesPerUnit;
        // Keep the staged clock consistent with the staged odometer so the
        // still reads like a real mid-trip frame.
        playTime = deadheadMiles * (60f / DeadheadMph) * 60f;
        SetObjective(DeadheadObjective);
        UpdateTripHud();
        yield return new WaitForSeconds(0.35f);
        yield return StartCoroutine(Shot("03i_deadhead_hud"));
        deadheadWorld = 0f; deadheadMiles = 0f; deadheadActive = false; Truck.engineOn = false;
        playTime = 0f;
        UpdateTripHud();

        // Step out for the exterior establishing shots; the autoplay run exercises
        // the real enter/exit path separately.
        Truck.speed = 0f;
        Truck.Exit(true);
        Truck.transform.position = new Vector3(0, 0, 182f + dz);
        Truck.transform.rotation = Quaternion.identity;
        Player.Teleport(new Vector3(-5.0f, 0.22f, 173.0f + dz), 0f);
        yield return new WaitForSeconds(0.5f);
        if (!guardOpen) OnGateApproach(Truck);
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(CineShot("04_gate",
            new Vector3(7.0f, 1.65f, 176.5f + dz), new Vector3(1.0f, 2.2f, 199.5f + dz), 58f));

        // Clear the guard conversation so the yard shots show the world, not the quiz.
        if (guardOpen)
        {
            GuardAnswer(true);
            yield return new WaitForSeconds(0.8f);
        }
        ClearSubtitle();
        SetObjective("Drive to trailer A-14 in the yard.");

        Truck.transform.position = new Vector3(14, 0, 254f + dz);
        Truck.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
        Truck.speed = 0f;
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(CineShot("05_yard",
            new Vector3(5.5f, 1.68f, 244.0f + dz), new Vector3(20.0f, 2.40f, 266.0f + dz), 55f));

        yield return StartCoroutine(CineShot("06_container",
            new Vector3(13.5f, 1.50f, 265.0f + dz), new Vector3(25.5f, 2.55f, 276.0f + dz), 50f));

        ClearSubtitle();
        SetObjective("Photograph seal 482913 with your phone.  [TAB]");
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(CineShot("07_seal",
            World.container.TransformPoint(new Vector3(0.28f, 1.76f, 12.10f)), World.container.TransformPoint(new Vector3(-0.22f, 1.34f, 9.28f)), 33f, true));
        // close look at the docks: pallets, signal lights, chocks, puddle
        yield return StartCoroutine(CineShot("08_docks",
            new Vector3(-40f, 1.7f, 312f + dz), new Vector3(-52f, 1.2f, 327f + dz), 50f, true));
        yield return StartCoroutine(CouplingShots());
        yield return new WaitForSeconds(0.7f);
        Application.Quit();
    }

    IEnumerator AutoPlayRoutine()
    {
        Debug.Log("D2D PLAY start");
        float dz = World.destination != null ? World.destination.position.z : 0f;
        yield return new WaitForSeconds(1.0f);
        BeginShift();
        yield return new WaitForSeconds(0.8f);

        // open the driver door using the real interaction ray
        for (int attempt = 0; attempt < 3 && !Truck.doorOpen; attempt++)
        {
            Player.Teleport(new Vector3(-3.15f, 0.2f, 10.35f), 0f);
            Player.LookAt(new Vector3(-1.95f, 1.55f, 9.55f));
            yield return new WaitForSeconds(0.4f);
            Debug.Log("D2D PLAY door attempt=" + attempt + " target=" + (Interaction.Current != null ? Interaction.Current.id : "none")
                + " cam=" + Camera.transform.position);
            if (Interaction.Current != null && Interaction.Current.id == "door") Interaction.TryInteract();
            yield return new WaitForSeconds(0.7f);
        }
        Debug.Log("D2D PLAY doorOpen=" + Truck.doorOpen);

        // climb into the cab through the open door
        for (int attempt = 0; attempt < 3 && !Player.IsSeated; attempt++)
        {
            Player.Teleport(new Vector3(-3.25f, 0.2f, 9.65f), 0f);
            Player.LookAt(new Vector3(-1.72f, 1.5f, 9.55f));
            yield return new WaitForSeconds(0.4f);
            Debug.Log("D2D PLAY seat attempt=" + attempt + " target=" + (Interaction.Current != null ? Interaction.Current.id : "none"));
            if (Interaction.Current != null) Interaction.TryInteract();
            yield return new WaitForSeconds(0.8f);
        }
        Debug.Log("D2D PLAY seated=" + Player.IsSeated);

        // let the dispatch call run at 5x, then verify the load
        Time.timeScale = 8f;
        yield return new WaitForSeconds(8.5f);
        Time.timeScale = 1f;
        Debug.Log("D2D PLAY call open=" + Phone.IsOpen + " verified=" + loadVerified);
        if (!loadVerified) VerifyLoad();
        yield return new WaitForSeconds(0.5f);

        // ignition, looking down at the dash
        if (Player.IsSeated)
        {
            Player.SetLook(0f, 30f);
            yield return new WaitForSeconds(0.3f);
            Debug.Log("D2D PLAY ignition state phone=" + Phone.IsOpen + " blocked=" + BlocksInteraction
                + " cam=" + Camera.transform.position + " fwd=" + Camera.transform.forward
                + " target=" + (Interaction.Current != null ? Interaction.Current.id : "none"));
            Interaction.TryInteract();
            yield return new WaitForSeconds(0.5f);
        }
        Debug.Log("D2D PLAY engineOn=" + Truck.engineOn);
        if (!Truck.engineOn) { Truck.StartEngine(); Debug.Log("D2D PLAY engine forced"); }

        // Deadhead for real: drive the two-lane from the lot to Gate 1 under
        // throttle. The odometer integrates this path, so the trip panel and
        // the arrival time are earned, not teleported.
        Truck.controlLock = false;
        Truck.touchSteer = 0f;
        Truck.touchBrake = 0f;
        Time.timeScale = 2f;
        float gateT0 = Time.time;
        float nextTripLog = 0f;
        while (!guardOpen && !ended && Time.time - gateT0 < 75f)
        {
            Vector3 p = Truck.transform.position;
            Vector3 local = Truck.transform.InverseTransformPoint(new Vector3(0f, 0f, 188f + dz));
            float ang = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            Truck.touchSteer = Mathf.Clamp(ang / 20f, -1f, 1f);
            Truck.touchThrottle = Truck.speed < 6.5f ? 0.55f : 0f;
            if (Time.time >= nextTripLog)
            {
                nextTripLog = Time.time + 3f;
                Debug.Log("D2D PLAY deadhead z=" + p.z.ToString("F1") + " speed=" + Truck.speed.ToString("F1")
                    + " miles=" + deadheadMiles.ToString("F3") + " assignedRemaining=" + DeadheadRemainingMiles.ToString("F2")
                    + " clock=" + ClockNow);
            }
            yield return null;
        }
        Truck.touchThrottle = 0f;
        Truck.touchSteer = 0f;
        Time.timeScale = 1f;
        Debug.Log("D2D PLAY guardOpen=" + guardOpen + " truckZ=" + Truck.transform.position.z
            + " miles=" + deadheadMiles.ToString("F3") + " assignedRemaining=" + DeadheadRemainingMiles.ToString("F2")
            + " arrival=" + deadheadArrivalClock);
        if (!guardOpen)
        {
            OnGateApproach(Truck);
            yield return new WaitForSeconds(0.4f);
            Debug.Log("D2D PLAY guard forced=" + guardOpen);
        }
        if (guardOpen)
        {
            GuardAnswer(true);
            yield return new WaitForSeconds(0.7f);
        }
        Debug.Log("D2D PLAY gatePassed=" + gatePassed);

        // park, exit, and verify feet land on the ground
        Truck.speed = 0f;
        Truck.transform.position = new Vector3(6f, 0f, 256f + dz);
        Truck.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        yield return new WaitForSeconds(0.4f);
        Truck.Exit();
        yield return new WaitForSeconds(0.8f);
        if (Player.IsSeated) Truck.Exit();
        yield return new WaitForSeconds(0.7f);
        Debug.Log("D2D PLAY exited seated=" + Player.IsSeated + " pos=" + Player.transform.position);

        // walk up to every inspection tag with the real interaction ray
        foreach (var ip in World.inspections)
        {
            Vector3 dir = ip.transform.position - World.container.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            dir.Normalize();
            Vector3 stand = ip.transform.position + dir * 1.95f;
            stand.y = 0.2f;
            for (int attempt = 0; attempt < 3 && !ip.inspected; attempt++)
            {
                Player.Teleport(stand, 0f);
                Player.LookAt(ip.transform.position);
                yield return new WaitForSeconds(0.35f);
                Debug.Log("D2D PLAY inspect " + ip.id + " attempt=" + attempt + " target=" + (Interaction.Current != null ? Interaction.Current.id : "none")
                    + " stand=" + stand + " cam=" + Camera.transform.position);
                if (Interaction.Current != null && Interaction.Current.id == ip.id) Interaction.TryInteract();
                yield return new WaitForSeconds(0.45f);
            }
            Debug.Log("D2D PLAY inspected " + ip.id + "=" + ip.inspected + " count=" + inspected);
        }

        // photograph the seal using the phone camera
        var seal = World.sealTarget;
        if (seal != null)
        {
            // trailer space: the doors face local +z
            Vector3 sealPlate = seal.transform.position + World.container.TransformDirection(new Vector3(0f, -0.05f, -0.26f));
            Vector3 stand = sealPlate + World.container.TransformDirection(new Vector3(0f, 0f, 0.8f));
            stand.y = 0.2f;
            Player.Teleport(stand, 0f);
            yield return new WaitForSeconds(0.25f);
            Player.LookAt(sealPlate);
            yield return new WaitForSeconds(0.2f);
            OpenCamera();
            yield return new WaitForSeconds(0.6f);
            Phone.Capture();
            yield return new WaitForSeconds(1.0f);
            Debug.Log("D2D PLAY previewReady=" + Phone.PreviewReady);
            Phone.SendPhoto();
            yield return new WaitForSeconds(3.5f);
        }

        // hook A-14 for real and pull it out to the gate
        yield return StartCoroutine(AutoHook());
        Debug.Log("D2D PLAY END ended=" + ended + " sealPhoto=" + sealPhotoSent + " inspected=" + inspected
            + " coupled=" + (Rig != null && Rig.coupled) + " lines=" + (Rig != null && Rig.linesConnected)
            + " gearUp=" + (Rig != null && Rig.gearUp) + " air=" + (Rig != null && Rig.airSupplied)
            + " deadhead=" + DeadheadAssignedMiles + " driven=" + deadheadMiles.ToString("F2")
            + " assignedRemaining=" + DeadheadRemainingMiles.ToString("F2") + " arrival=" + deadheadArrivalClock);
        Application.Quit();
    }

    static Vector3 FlatXZ(Vector3 v) { v.y = 0f; return v; }

    // Line up 7 m in front of the kingpin, a hand's width off centre, back under
    // with the real controls, tug test, connect the lines, crank the gear, supply
    // trailer air and drive out to the gate by pure pursuit.
    IEnumerator AutoHook()
    {
        var rig = Rig;
        if (rig == null) yield break;
        float dz = World.destination != null ? World.destination.position.z : 0f;
        Time.timeScale = 1f;
        Vector3 f = rig.Forward;
        Quaternion rot = Quaternion.LookRotation(f);
        Vector3 fw = rig.KingpinWorld + f * 7f + rot * new Vector3(0.12f, 0f, 0f);
        Vector3 pos = fw - rot * Truck.FifthWheelLocal; pos.y = 0f;
        Truck.speed = 0f;
        Truck.transform.SetPositionAndRotation(pos, rot);
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(AutoBoard());
        yield return StartCoroutine(AutoDash("engine"));
        if (!Truck.engineOn) { Truck.StartEngine(); Debug.Log("D2D PLAY hook engine forced"); }
        yield return new WaitForSeconds(0.4f);

        // hold S at a standstill to select R, then creep back at walking pace
        float t0 = Time.time;
        while (!rig.coupled && Time.time - t0 < 25f)
        {
            Truck.touchBrake = Truck.Gear >= 0 ? 0.7f : (Truck.speed > -0.9f ? 0.32f : 0f);
            yield return null;
        }
        Truck.touchBrake = 0f;
        Debug.Log("D2D PLAY hook coupled=" + rig.coupled + " t=" + (Time.time - t0).ToString("F1"));
        yield return new WaitForSeconds(1.2f);

        // tug test: W selects drive at a standstill, then pulls against the trailer brakes
        t0 = Time.time;
        while (!rig.tugPassed && Time.time - t0 < 8f) { Truck.touchThrottle = 0.75f; yield return null; }
        Truck.touchThrottle = 0f;
        Debug.Log("D2D PLAY hook tug=" + rig.tugPassed);
        yield return new WaitForSeconds(0.8f);

        Truck.Exit();
        yield return new WaitForSeconds(0.8f);
        if (Player.IsSeated) Truck.Exit();
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(AutoUse("lines", Truck.transform.TransformPoint(new Vector3(-3.0f, 0.2f, -3.0f)),
            Truck.transform.TransformPoint(new Vector3(-1.78f, 1.5f, -3.0f)), () => rig.linesConnected, rig.ConnectLines));
        yield return new WaitForSeconds(0.8f);
        Vector3 crankAt = crankCol != null ? crankCol.transform.position : rig.CrankWorld;
        yield return StartCoroutine(AutoUse("crank", rig.CrankStandWorld, crankAt, () => rig.Cranking || rig.gearUp, rig.TryCrank));
        t0 = Time.time;
        while (!rig.gearUp && Time.time - t0 < 6f) yield return null;
        Debug.Log("D2D PLAY hook gearUp=" + rig.gearUp);

        yield return StartCoroutine(AutoBoard());
        yield return StartCoroutine(AutoDash("engine"));
        if (!Truck.engineOn) Truck.StartEngine();
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(AutoDash("trailer air"));
        if (!rig.airSupplied) { rig.SupplyAir(); Debug.Log("D2D PLAY hook air forced"); }
        yield return new WaitForSeconds(0.8f);

        // pull out and swing right toward the gate lane
        Time.timeScale = 2f;
        Vector3[] path = { new Vector3(24f, 0f, 252f + dz), new Vector3(12f, 0f, 236f + dz), new Vector3(2f, 0f, 218f + dz), new Vector3(0f, 0f, 190f + dz) };
        int wp = 0;
        float maxArt = 0f;
        t0 = Time.time;
        while (!ended && Time.time - t0 < 90f)
        {
            Vector3 p = Truck.transform.position;
            if (wp < path.Length - 1 && Vector3.Distance(FlatXZ(p), path[wp]) < 7f) wp++;
            Vector3 local = Truck.transform.InverseTransformPoint(path[wp]);
            float ang = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            Truck.touchSteer = Mathf.Clamp(ang / 20f, -1f, 1f);
            Truck.touchThrottle = Truck.speed < 4.5f ? 0.55f : 0f;
            maxArt = Mathf.Max(maxArt, Mathf.Abs(rig.Articulation));
            yield return null;
        }
        Truck.touchThrottle = 0f;
        Truck.touchSteer = 0f;
        Time.timeScale = 1f;
        Debug.Log("D2D PLAY hook departed=" + ended + " maxArticulation=" + maxArt.ToString("F1") + " truck=" + Truck.transform.position.ToString("F1"));
    }

    IEnumerator AutoBoard()
    {
        for (int attempt = 0; attempt < 3 && !Player.IsSeated; attempt++)
        {
            Player.Teleport(Truck.transform.TransformPoint(new Vector3(-3.15f, 0.2f, 2.35f)), 0f);
            Player.LookAt(Truck.transform.TransformPoint(new Vector3(-1.95f, 1.55f, 1.55f)));
            yield return new WaitForSeconds(0.4f);
            string id = Interaction.Current != null ? Interaction.Current.id : "none";
            Debug.Log("D2D PLAY board attempt=" + attempt + " target=" + id
                + " tp=" + Player.ThirdPersonActive + " blocked=" + BlocksInteraction
                + " player=" + Player.transform.position + " cam=" + Camera.transform.position
                + " fwd=" + Camera.transform.forward);
            if (id == "door" || id == "seat") Interaction.TryInteract();
            yield return new WaitForSeconds(1.0f);
        }
        if (!Player.IsSeated) { Truck.SetDoor(true); Truck.Enter(); Debug.Log("D2D PLAY board forced"); }
        yield return new WaitForSeconds(0.4f);
    }

    IEnumerator AutoDash(string what)
    {
        if (!Player.IsSeated) yield break;
        Player.SetLook(0f, 30f);
        yield return new WaitForSeconds(0.3f);
        string id = Interaction.Current != null ? Interaction.Current.id : "none";
        Debug.Log("D2D PLAY dash " + what + " target=" + id);
        if (id == "ignition") Interaction.TryInteract();
        yield return new WaitForSeconds(0.5f);
        Player.SetLook(0f, 0f);
    }

    IEnumerator AutoUse(string id, Vector3 stand, Vector3 look, Func<bool> done, Action force)
    {
        stand.y = 0.2f;
        for (int attempt = 0; attempt < 3 && !done(); attempt++)
        {
            Player.Teleport(stand, 0f);
            Player.LookAt(look);
            yield return new WaitForSeconds(0.4f);
            string cur = Interaction.Current != null ? Interaction.Current.id : "none";
            Debug.Log("D2D PLAY use " + id + " attempt=" + attempt + " target=" + cur);
            if (cur == id) Interaction.TryInteract();
            yield return new WaitForSeconds(0.5f);
        }
        if (!done()) { force(); Debug.Log("D2D PLAY use " + id + " forced"); }
    }


    // Autoshot frames of the hook-up: backing in, locked with the lines on, and
    // the rig articulated in a right turn (the tractor driven along an arc with
    // the real trailer follow).
    IEnumerator CouplingShots()
    {
        var rig = Rig;
        if (rig == null) yield break;
        Vector3 f = rig.Forward;
        Quaternion rot = Quaternion.LookRotation(f);
        Vector3 right = rot * Vector3.right;
        Vector3 pos = rig.KingpinWorld + f * 2.2f - rot * Truck.FifthWheelLocal; pos.y = 0f;
        Truck.speed = 0f;
        Truck.transform.SetPositionAndRotation(pos, rot);
        yield return new WaitForSeconds(0.3f);
        Vector3 kp = rig.KingpinWorld;
        yield return StartCoroutine(CineShot("09_hook_approach", kp + f * 1.4f - right * 4.2f + Vector3.up * 0.55f, kp + f * 1.0f, 46f, true));

        pos = rig.KingpinWorld - rot * Truck.FifthWheelLocal; pos.y = 0f;
        Truck.transform.SetPositionAndRotation(pos, rot);
        Truck.hooked = rig;
        rig.Couple(Truck, 0.4f);
        rig.PassTug();
        rig.ConnectLines();
        rig.TryCrank();
        yield return new WaitForSeconds(3.5f);
        rig.SupplyAir();
        ClearSubtitle();
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(CineShot("10_coupled",
            Truck.transform.TransformPoint(new Vector3(-3.3f, 2.1f, -2.0f)), Truck.transform.TransformPoint(new Vector3(-0.2f, 1.35f, -4.1f)), 50f, true));

        // drive a 14 m radius right turn with the real follow, a step at a time
        for (int i = 0; i < 46; i++)
        {
            Vector3 pivot = Truck.transform.TransformPoint(new Vector3(0f, 0f, Truck.RearAxleZ));
            Truck.transform.RotateAround(pivot, Vector3.up, 0.25f / 14f * Mathf.Rad2Deg);
            Truck.transform.position += Truck.transform.forward * 0.25f;
            rig.Follow(Truck.transform, Truck.FifthWheelLocal, 0.033f);
            if (i % 8 == 0) yield return null;
        }
        Debug.Log("D2D SHOT articulation=" + rig.Articulation.ToString("F1"));
        ClearSubtitle();
        Vector3 mid = (Truck.transform.position + rig.transform.position) * 0.5f;
        Vector3 side = Vector3.Cross(Vector3.up, rig.Forward).normalized;   // the trailer's left
        yield return StartCoroutine(CineShot("11_rig_turn", mid - side * 17f + rig.Forward * 6f + Vector3.up * 7.5f, mid + Vector3.up * 1.2f, 50f, true));
    }

    IEnumerator CineShot(string name, Vector3 pos, Vector3 look, float fov, bool hideMarkers = false)
    {
        // Temporarily detach the graded first-person camera for a composed frame.
        // The HUD stays live, and the player is restored afterwards.
        var cam = Camera;
        var parent = cam.transform.parent;
        Vector3 localPos = cam.transform.localPosition;
        Quaternion localRot = cam.transform.localRotation;
        float oldFov = cam.fieldOfView;
        Player.SetUILock(true);
        if (Player.body != null)
        {
            Player.body.ForceHead = true;
            // only the driver shots frame the player; elsewhere keep him out of the way
            Player.body.model.gameObject.SetActive(name.Contains("driver") || name.Contains("door"));
        }
        if (hideMarkers) SetInspectionMarkers(false);
        cam.transform.SetParent(null, true);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
        cam.fieldOfView = fov;
        yield return new WaitForSeconds(0.18f);
        yield return StartCoroutine(Shot(name));
        cam.fieldOfView = oldFov;
        cam.transform.SetParent(parent, false);
        cam.transform.localPosition = localPos;
        cam.transform.localRotation = localRot;
        if (hideMarkers) SetInspectionMarkers(true);
        if (Player.body != null)
        {
            Player.body.ForceHead = false;
            // Never resurrect the driver body while the title camera owns the
            // view; the menu restores it in EndMenuView.
            if (!Player.MenuView)
            {
                Player.body.model.gameObject.SetActive(true);
                Player.body.Visible = true;
            }
        }
        Player.SetUILock(false);
    }

    IEnumerator Shot(string name)
    {
        string path = System.IO.Path.Combine(shotDir, name + ".png");
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        if (tex != null)
        {
            var bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            Destroy(tex);
        }
        yield return new WaitForSeconds(0.35f);
        Debug.Log("AUTOSHOT " + path + " exists=" + System.IO.File.Exists(path));
    }

    static void AddTextShadow(Text t)
    {
        if (t == null || t.GetComponent<Shadow>() != null) return;
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.9f);
        s.effectDistance = new Vector2(1.2f, -1.2f);
        s.useGraphicAlpha = true;
    }

    // Backing lesson overlay: live distance/alignment, contact and pull-up
    // counters, the score after the kingpin is captured, and a retry button.
    void BuildBackingLesson()
    {
        var bp = UIFactory.Panel(hudRoot.transform, "BackingLesson", new Vector2(780, 196), Vector2.zero, new Color(0.025f, 0.04f, 0.06f, 0.92f), true, 18);
        UIFactory.BottomCenter(bp.rectTransform, new Vector2(0, 224), new Vector2(780, 196));
        backingPanel = bp.gameObject;

        backingText = UIFactory.Label(bp.transform, "Text", "BACKING LESSON", 20, new Color(1f, 0.55f, 0.16f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(backingText.rectTransform, new Vector2(20, -14), new Vector2(740, 28));
        backingResult = UIFactory.Label(bp.transform, "Result", "", 19, new Color(0.92f, 0.95f, 0.97f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(backingResult.rectTransform, new Vector2(20, -50), new Vector2(740, 108));

        var retry = UIFactory.Button(bp.transform, "Retry", "RETRY", new Vector2(150, 46), Vector2.zero, new Color(0.10f, 0.16f, 0.20f), Color.white, 18, RetryBacking);
        var rt = retry.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18, 14);

        backingPanel.SetActive(false);
    }

    void BuildHud()
    {
        hudCanvas = UIFactory.Canvas("HUD", 20, true, true);
        hudRoot = hudCanvas.gameObject;

        // Objective: deliberately small in the top-left. Safety messages have
        // their own panel near the centre so a routine line never covers them.
        var top = UIFactory.Panel(hudRoot.transform, "Objective", new Vector2(500, 104), Vector2.zero, new Color(0.02f, 0.035f, 0.05f, 0.78f), true, 18);
        UIFactory.TopLeft(top.rectTransform, new Vector2(24, -20), new Vector2(500, 104));
        var kicker = UIFactory.Label(top.rectTransform, "Kicker", "SHIFT 01  ·  DALLAS, TX", 14, new Color(1f, 0.52f, 0.12f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(kicker.rectTransform, new Vector2(18, -12), new Vector2(464, 22));
        objectiveText = UIFactory.Label(top.rectTransform, "Text", "", 22, new Color(0.92f, 0.95f, 0.97f), TextAnchor.UpperLeft);
        objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
        objectiveText.verticalOverflow = VerticalWrapMode.Truncate;
        UIFactory.TopLeft(objectiveText.rectTransform, new Vector2(18, -36), new Vector2(464, 58));

        // The sky behind this panel is near-white at dawn; a translucent slate
        // panel plus a text shadow keeps the controls readable without hiding
        // the road.
        var infoPanel = UIFactory.Panel(hudRoot.transform, "Info", new Vector2(380, 100), Vector2.zero, new Color(0.02f, 0.035f, 0.05f, 0.88f), true, 18);
        UIFactory.TopRight(infoPanel.rectTransform, new Vector2(24, -22), new Vector2(380, 100));
        clockText = UIFactory.Label(infoPanel.transform, "Clock", "05:40", 36, new Color(0.96f, 0.98f, 1f), TextAnchor.UpperRight, FontStyle.Bold);
        UIFactory.TopRight(clockText.rectTransform, new Vector2(20, -12), new Vector2(180, 40));
        AddTextShadow(clockText);

        var controls = UIFactory.Label(infoPanel.transform, "Controls", "WASD MOVE  ·  E INTERACT  ·  TAB PHONE  ·  X EXIT", 17, new Color(0.95f, 0.97f, 0.99f), TextAnchor.UpperRight, FontStyle.Normal);
        controlsText = controls;
        AddTextShadow(controls);
        BuildWaypoint();
        controls.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.TopRight(controls.rectTransform, new Vector2(20, -60), new Vector2(330, 26));

        // Deadhead trip: assigned miles, actual odometer, miles left and ETA.
        // It only appears while the truck is rolling toward DFW9; the yard
        // moves after the gate are explicitly not part of the trip.
        var tp = UIFactory.Panel(hudRoot.transform, "Trip", new Vector2(380, 170), Vector2.zero, new Color(0.02f, 0.035f, 0.05f, 0.64f), true, 18);
        UIFactory.TopRight(tp.rectTransform, new Vector2(24, -132), new Vector2(380, 152));
        tripKicker = UIFactory.Label(tp.transform, "Kicker", "DEADHEAD · DFW9", 15, new Color(1f, 0.52f, 0.12f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(tripKicker.rectTransform, new Vector2(18, -12), new Vector2(344, 22));
        tripMiles = UIFactory.Label(tp.transform, "Miles", "0.00 MI", 38, new Color(0.94f, 0.96f, 0.98f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(tripMiles.rectTransform, new Vector2(18, -32), new Vector2(200, 46));
        tripDetail = UIFactory.Label(tp.transform, "Detail", "DRIVEN · ROUTE 0.12 MI OF 32 ASSIGNED", 17, new Color(0.74f, 0.82f, 0.87f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(tripDetail.rectTransform, new Vector2(18, -82), new Vector2(344, 24));
        tripEta = UIFactory.Label(tp.transform, "Eta", "DEP 05:40 · ETA 05:41 · 1 UNIT = 1 M", 16, new Color(0.58f, 0.72f, 0.80f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(tripEta.rectTransform, new Vector2(18, -118), new Vector2(344, 24));
        tripPanel = tp.gameObject;
        tripPanel.SetActive(false);
        BuildBackingLesson();

        var phoneHint = UIFactory.Label(hudRoot.transform, "PhoneHint", "TAB  PHONE", 22, new Color(0.8f, 0.85f, 0.88f), TextAnchor.LowerRight, FontStyle.Bold);
        phoneHint.rectTransform.anchorMin = phoneHint.rectTransform.anchorMax = new Vector2(1, 0);
        phoneHint.rectTransform.pivot = new Vector2(1, 0);
        phoneHint.rectTransform.anchoredPosition = new Vector2(-30, 34);
        phoneHint.rectTransform.sizeDelta = new Vector2(260, 40);

        focusHint = UIFactory.Label(hudRoot.transform, "FocusHint", "CLICK TO CAPTURE MOUSE", 19, new Color(1f, 0.62f, 0.24f), TextAnchor.LowerCenter, FontStyle.Bold);
        focusHint.rectTransform.anchorMin = focusHint.rectTransform.anchorMax = new Vector2(0.5f, 0);
        focusHint.rectTransform.pivot = new Vector2(0.5f, 0);
        focusHint.rectTransform.anchoredPosition = new Vector2(0, 92);
        focusHint.rectTransform.sizeDelta = new Vector2(520, 34);
        focusHint.gameObject.SetActive(false);

        var cross = UIFactory.Img(hudRoot.transform, "Cross", new Vector2(6, 6), Vector2.zero, new Color(1f, 1f, 1f, 0.55f));
        cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        cross.rectTransform.anchoredPosition = Vector2.zero;

        var speedo = UIFactory.Panel(hudRoot.transform, "Speedo", new Vector2(300, 150), Vector2.zero, new Color(0.02f, 0.035f, 0.05f, 0.80f), true, 18);
        var srt = speedo.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0, 0);
        srt.pivot = new Vector2(0, 0);
        srt.anchoredPosition = new Vector2(30, 30);
        speedText = UIFactory.Label(speedo.transform, "Speed", "0", 58, new Color(0.95f, 0.97f, 0.99f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(speedText.rectTransform, new Vector2(24, -12), new Vector2(140, 70));
        var mph = UIFactory.Label(speedo.transform, "MPH", "MPH", 19, new Color(0.45f, 0.78f, 0.92f), TextAnchor.LowerLeft, FontStyle.Bold);
        UIFactory.TopLeft(mph.rectTransform, new Vector2(26, -80), new Vector2(100, 30));
        gearText = UIFactory.Label(speedo.transform, "Gear", "P  ·  118 PSI", 20, new Color(0.82f, 0.85f, 0.88f), TextAnchor.UpperRight, FontStyle.Bold);
        UIFactory.TopRight(gearText.rectTransform, new Vector2(22, -22), new Vector2(160, 40));
        trailerText = UIFactory.Label(speedo.transform, "Trailer", "BOBTAIL", 15, new Color(0.62f, 0.66f, 0.70f), TextAnchor.LowerLeft, FontStyle.Bold);
        UIFactory.TopLeft(trailerText.rectTransform, new Vector2(26, -116), new Vector2(270, 26));
        trailerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        speedPanel = speedo.gameObject;
        speedPanel.SetActive(false);

        // Use prompt: anchored to the object the centre ray is aimed at, not
        // parked at the bottom of the screen. UpdatePromptPosition moves it.
        var pp = UIFactory.Panel(hudRoot.transform, "Prompt", new Vector2(520, 56), Vector2.zero, new Color(0.02f, 0.035f, 0.05f, 0.90f), true, 16);
        promptRect = pp.rectTransform;
        promptRect.anchorMin = promptRect.anchorMax = Vector2.zero;
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(960, 220);
        promptPanel = pp.gameObject;
        promptText = UIFactory.Label(pp.rectTransform, "Text", "", 24, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(promptText.rectTransform, 12, 12, 6, 6);
        promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptPanel.SetActive(false);

        // Safety warning: centre screen, icon, priority colour. This is a
        // separate panel from subtitles so routine dialogue cannot erase it.
        var wp = UIFactory.Panel(hudRoot.transform, "Warning", new Vector2(780, 96), Vector2.zero, new Color(0.34f, 0.08f, 0.04f, 0.95f), true, 18);
        var wrt = wp.rectTransform;
        wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
        wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.anchoredPosition = new Vector2(0, 150);
        warningPanel = wp.gameObject;
        warningIconBg = UIFactory.Panel(wrt, "IconBg", new Vector2(56, 56), Vector2.zero, new Color(1f, 0.55f, 0.15f), true, 14);
        var ibr = warningIconBg.rectTransform;
        ibr.anchorMin = ibr.anchorMax = new Vector2(0f, 0.5f);
        ibr.pivot = new Vector2(0f, 0.5f);
        ibr.anchoredPosition = new Vector2(20, 0);
        warningIconText = UIFactory.Label(ibr, "Icon", "!", 34, new Color(0.10f, 0.06f, 0.02f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(warningIconText.rectTransform, 0, 0, 0, 0);
        warningText = UIFactory.Label(wrt, "Text", "", 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        warningText.rectTransform.anchorMin = new Vector2(0, 0);
        warningText.rectTransform.anchorMax = new Vector2(1, 1);
        warningText.rectTransform.offsetMin = new Vector2(92, 8);
        warningText.rectTransform.offsetMax = new Vector2(-18, -8);
        warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        warningPanel.SetActive(false);

        // Dialogue subtitles stay near the bottom, out of the warning's way.
        var sp = UIFactory.Panel(hudRoot.transform, "Subtitle", new Vector2(920, 110), Vector2.zero, new Color(0.02f, 0.035f, 0.05f, 0.86f), true, 16);
        UIFactory.BottomCenter(sp.rectTransform, new Vector2(0, 96), new Vector2(920, 110));
        subtitleText = UIFactory.Label(sp.rectTransform, "Text", "", 28, new Color(0.94f, 0.96f, 0.98f), TextAnchor.MiddleCenter);
        UIFactory.Stretch(subtitleText.rectTransform, 20, 20, 10, 10);
        subtitlePanel = sp.gameObject;
        subtitlePanel.SetActive(false);

        BuildGuard();
        BuildPause();
        BuildEnd();
    }

    void BuildPause()
    {
        var dim = UIFactory.Full(hudRoot.transform, "Pause", new Color(0.006f, 0.010f, 0.016f, 0.62f));
        // The dim is not a button. Clicks on it resume through Update; only
        // real UI (the sensitivity buttons) blocks that click.
        dim.raycastTarget = false;
        pausePanel = dim.gameObject;
        var box = UIFactory.Panel(dim.transform, "Box", new Vector2(620, 340), Vector2.zero, new Color(0.035f, 0.05f, 0.065f, 0.98f), true, 22);
        box.raycastTarget = false;
        box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        box.rectTransform.anchoredPosition = Vector2.zero;
        var title = UIFactory.Label(box.rectTransform, "Title", "PAUSED", 48, new Color(0.96f, 0.95f, 0.90f), TextAnchor.MiddleCenter, FontStyle.Bold);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1);
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.rectTransform.anchoredPosition = new Vector2(0, -14);
        title.rectTransform.sizeDelta = new Vector2(560, 56);
        var sub = UIFactory.Label(box.rectTransform, "Sub", "CLICK TO RESUME", 26, new Color(1f, 0.62f, 0.24f), TextAnchor.MiddleCenter, FontStyle.Bold);
        sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 0);
        sub.rectTransform.pivot = new Vector2(0.5f, 0);
        sub.rectTransform.anchoredPosition = new Vector2(0, 34);
        sub.rectTransform.sizeDelta = new Vector2(540, 40);
        var note = UIFactory.Label(box.rectTransform, "Note", "Escape and right-click release the mouse. Click anywhere to take the controls again.", 17, new Color(0.62f, 0.72f, 0.78f), TextAnchor.MiddleCenter);
        note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        note.rectTransform.anchoredPosition = new Vector2(0, -2);
        note.rectTransform.sizeDelta = new Vector2(560, 44);

        sensitivityText = UIFactory.Label(box.rectTransform, "Sensitivity", "MOUSE SENSITIVITY  2.0", 22, new Color(0.92f, 0.95f, 0.97f), TextAnchor.MiddleCenter, FontStyle.Bold);
        sensitivityText.rectTransform.anchorMin = sensitivityText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        sensitivityText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        sensitivityText.rectTransform.anchoredPosition = new Vector2(0, -92);
        sensitivityText.rectTransform.sizeDelta = new Vector2(560, 32);
        var minus = UIFactory.Button(box.rectTransform, "SensMinus", "-", new Vector2(76, 50), Vector2.zero, new Color(0.10f, 0.16f, 0.20f), Color.white, 26, () => ChangeSensitivity(-0.2f));
        minus.GetComponent<RectTransform>().anchorMin = minus.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        minus.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        minus.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, -140);
        var plus = UIFactory.Button(box.rectTransform, "SensPlus", "+", new Vector2(76, 50), Vector2.zero, new Color(0.10f, 0.16f, 0.20f), Color.white, 26, () => ChangeSensitivity(0.2f));
        plus.GetComponent<RectTransform>().anchorMin = plus.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        plus.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        plus.GetComponent<RectTransform>().anchoredPosition = new Vector2(90, -140);

        pausePanel.SetActive(false);
    }

    void BuildGuard()
    {
        var panel = UIFactory.Panel(hudRoot.transform, "Guard", new Vector2(860, 390), Vector2.zero, new Color(0.025f, 0.04f, 0.055f, 0.95f), true, 24);
        UIFactory.BottomCenter(panel.rectTransform, new Vector2(0, 170), new Vector2(860, 390));
        guardPanel = panel.gameObject;
        var title = UIFactory.Label(panel.rectTransform, "Title", "GATE 1  ·  SECURITY", 22, new Color(1f, 0.52f, 0.12f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, new Vector2(28, -20), new Vector2(700, 32));
        guardText = UIFactory.Label(panel.rectTransform, "Text", "", 24, Color.white, TextAnchor.UpperLeft);
        UIFactory.TopLeft(guardText.rectTransform, new Vector2(28, -58), new Vector2(800, 110));
        var a = UIFactory.Button(panel.rectTransform, "Correct", "07:30  ·  AMAZON DFW9  ·  SEAL 482913", new Vector2(780, 58), new Vector2(0, -190), new Color(0.10f, 0.18f, 0.23f), Color.white, 22, () => GuardAnswer(true));
        a.GetComponent<RectTransform>().anchorMin = a.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        a.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
        a.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -190);
        var b = UIFactory.Button(panel.rectTransform, "Wrong1", "08:00  ·  AMAZON DFW9  ·  SEAL 482913", new Vector2(380, 54), new Vector2(-190, -260), new Color(0.08f, 0.11f, 0.14f), Color.white, 18, () => GuardAnswer(false));
        b.GetComponent<RectTransform>().anchorMin = b.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        b.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
        b.GetComponent<RectTransform>().anchoredPosition = new Vector2(-190, -260);
        var c = UIFactory.Button(panel.rectTransform, "Wrong2", "07:30  ·  NORTHGATE  ·  SEAL 482913", new Vector2(380, 54), new Vector2(190, -260), new Color(0.08f, 0.11f, 0.14f), Color.white, 18, () => GuardAnswer(false));
        c.GetComponent<RectTransform>().anchorMin = c.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        c.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
        c.GetComponent<RectTransform>().anchoredPosition = new Vector2(190, -260);
        guardPanel.SetActive(false);
    }

    void BuildEnd()
    {
        var baseList = new List<GameObject>();
        foreach (Transform child in hudRoot.transform) baseList.Add(child.gameObject);
        hudBase = baseList.ToArray();
        var dim = UIFactory.Full(hudRoot.transform, "EndDim", new Color(0.006f, 0.010f, 0.016f, 0.86f));
        dim.raycastTarget = true;
        endDim = dim.gameObject;
        endDim.SetActive(false);

        var panel = UIFactory.Panel(hudRoot.transform, "End", new Vector2(1200, 760), Vector2.zero, new Color(0.045f, 0.060f, 0.076f, 0.985f), true, 30);
        UIFactory.BottomCenter(panel.rectTransform, new Vector2(0, 90), new Vector2(1200, 760));
        endPanel = panel.gameObject;

        var accent = UIFactory.Img(panel.rectTransform, "Accent", new Vector2(1200, 6), Vector2.zero, new Color(1f, 0.48f, 0.10f));
        accent.rectTransform.anchorMin = new Vector2(0.5f, 1);
        accent.rectTransform.anchorMax = new Vector2(0.5f, 1);
        accent.rectTransform.pivot = new Vector2(0.5f, 1);
        accent.rectTransform.anchoredPosition = Vector2.zero;

        var kicker = UIFactory.Label(panel.rectTransform, "Kicker", "", 22, new Color(1f, 0.55f, 0.16f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(kicker.rectTransform, new Vector2(54, -40), new Vector2(900, 32));
        endKicker = kicker;
        var title = UIFactory.Label(panel.rectTransform, "Title", "SHIFT READY", 64, new Color(0.97f, 0.95f, 0.89f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, new Vector2(50, -66), new Vector2(900, 82));
        var sub = UIFactory.Label(panel.rectTransform, "Sub", "", 26, new Color(0.70f, 0.78f, 0.83f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(sub.rectTransform, new Vector2(54, -154), new Vector2(1090, 42));
        endSub = sub;
        var rule = UIFactory.Img(panel.rectTransform, "Rule", new Vector2(1092, 2), Vector2.zero, new Color(1f, 1f, 1f, 0.12f));
        UIFactory.TopLeft(rule.rectTransform, new Vector2(54, -208), new Vector2(1092, 2));

        endInspect = Stat(panel.rectTransform, -380, "INSPECTIONS", "5/5", "ALL FIVE POINTS", new Color(0.83f, 0.90f, 0.94f));
        endSeal = Stat(panel.rectTransform, 0, "SEAL", "482913", "VERIFIED", new Color(0.40f, 0.95f, 0.65f));
        endTime = Stat(panel.rectTransform, 380, "SHIFT TIME", "7:42", "READY TO ROLL", new Color(0.83f, 0.90f, 0.94f));

        var note = UIFactory.Label(panel.rectTransform, "Note", "LOAD #D2D-0612   ·   DFW9 → DWH1   ·   240 MI   ·   $2,450", 26, new Color(0.68f, 0.77f, 0.83f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(note.rectTransform, new Vector2(54, -492), new Vector2(1080, 38));
        endNote = note;

        var debrief = UIFactory.Label(panel.rectTransform, "Debrief", "", 16, new Color(0.80f, 0.88f, 0.93f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(debrief.rectTransform, new Vector2(54, -420), new Vector2(1080, 64));
        endDebrief = debrief;

        var b = UIFactory.Button(panel.rectTransform, "Restart", "DRIVE AGAIN", new Vector2(470, 88), Vector2.zero, new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 30, Restart);
        b.GetComponent<RectTransform>().anchorMin = b.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        b.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
        b.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, 66);
        var q = UIFactory.Button(panel.rectTransform, "Quit", "QUIT", new Vector2(470, 88), Vector2.zero, new Color(0.10f, 0.16f, 0.20f), Color.white, 30, Quit);
        q.GetComponent<RectTransform>().anchorMin = q.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        q.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
        q.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, 66);
        endPanel.SetActive(false);
    }

    Text Stat(Transform parent, float x, string label, string value, string foot, Color valueColor)
    {
        var card = UIFactory.Panel(parent, "Stat" + label, new Vector2(340, 170), Vector2.zero, new Color(0.065f, 0.088f, 0.108f), true, 18);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(x, -244);
        var l = UIFactory.Label(rt, "Label", label, 20, new Color(0.58f, 0.68f, 0.75f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(l.rectTransform, new Vector2(26, -20), new Vector2(288, 28));
        var v = UIFactory.Label(rt, "Value", value, 52, valueColor, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(v.rectTransform, new Vector2(24, -54), new Vector2(292, 64));
        var f = UIFactory.Label(rt, "Foot", foot, 18, new Color(0.55f, 0.66f, 0.72f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(f.rectTransform, new Vector2(26, -126), new Vector2(288, 26));
        return v;
    }

    void BuildMobileControls()
    {
        if (!Application.isMobilePlatform && Screen.width >= 900) return;
        var go = new GameObject("MobileControls");
        go.transform.SetParent(transform, false);
        var mc = go.AddComponent<MobileControls>();
        mc.flow = this;
        mc.Build();
        mobileControls = mc;
    }
}
