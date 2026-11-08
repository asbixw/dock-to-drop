using UnityEngine;
using System.Collections;

// Kinematic tractor model tuned for feel rather than a full tyre simulation:
// - a bicycle model that turns about the rear tandem, with the road-wheel angle
//   narrowing as speed rises;
// - an automated 10-speed gearbox with RPM, a torque curve, a short torque cut
//   on every shift, engine braking, air-brake lag, rolling and air drag;
// - box-cast collision so the truck stops at buildings, rigs and people;
// - visuals: spinning wheels, steering front wheels, a steering wheel turned by
//   the real steering ratio, and body roll and pitch on a damped spring.
// Techniques follow common vehicle-sim write-ups (Edy's Vehicle Physics notes,
// Marco Monster's "Car Physics for Games"); no code was copied.
public class TruckController : MonoBehaviour
{
    public GameFlow flow;
    public Transform seat;
    public Transform exitPoint;
    public Transform driverDoor;
    public Transform headlightLeft;
    public Transform headlightRight;
    public AudioManager audio;
    public PlayerController player;
    public bool engineOn;
    public bool doorOpen;
    public bool driving;
    public bool controlLock;
    public float speed;
    public float touchSteer;
    public float touchThrottle;
    public float touchBrake;

    Quaternion doorClosed;
    Quaternion doorOpenLocal;
    Quaternion doorClosedWorld;
    bool doorInitialized;
    float headlightTimer;
    Light[] headlights;
    float steerSmooth, throttleSmooth, brakeSmooth, lastSpeed, longAccel, yawRate;
    Transform wheelPivot;
    Quaternion wheelRest;
    float wheelBase = 5.9f;
    float rearAxleZ = -3.5f;

    // drivetrain
    static readonly float[] Ratios = { 12.8f, 9.25f, 6.76f, 4.90f, 3.58f, 2.61f, 1.89f, 1.38f, 1.00f, 0.73f };
    const float FinalDrive = 3.55f, WheelRadius = 0.54f, IdleRpm = 650f, GovernedSpeed = 26.8f;
    int gear = 1;            // 0-based; bobtail starts in 2nd like an AMT
    float shiftTimer;
    float rpm = IdleRpm;
    bool reverse;
    float stoppedBrakeTime;
    bool airHissDone;

    // rig
    bool rigBuilt;
    Transform bodyPivot;
    Transform[] spinPivots;
    Transform[] steerPivots;
    float wheelAngle;
    float rollAng, rollVel, pitchAng, pitchVel;
    Transform tachNeedle, speedoNeedle;
    bool gaugesFound;
    BoxCollider hull;

    // trailer coupling
    public TrailerRig hookTarget;          // the trailer this shift hooks (A-14)
    public TrailerRig hooked;              // the coupled trailer, null while bobtail
    public AirLines lines;
    public Transform fifthWheel;           // visual, centred on the coupling point
    public const float FifthWheelZ = -4.75f;   // a few inches ahead of the tandem centre
    public float fifthWheelTop = 1.12f;
    public Vector3 FifthWheelLocal => new Vector3(0f, fifthWheelTop, FifthWheelZ);
    float tugOffset, tugHold, lastMissTime = -10f, lastHeldMsg = -10f;
    Renderer fifthWheelLamp;
    bool lampFound, lampOn;

    public float RearAxleZ => rearAxleZ;
    public float YawRate => yawRate;      // deg/s
    public float LongAccel => longAccel;  // m/s^2
    public float Steer => steerSmooth;
    public int Gear => reverse ? -1 : gear + 1;
    public float Rpm => rpm;

    void Awake()
    {
        InitDoor();
    }

    void Start()
    {
        BuildRig();
    }

    void InitDoor()
    {
        if (doorInitialized || driverDoor == null) return;
        doorClosed = driverDoor.localRotation;
        doorClosedWorld = driverDoor.rotation;
        Quaternion openWorld = Quaternion.AngleAxis(78f, Vector3.up) * doorClosedWorld;
        doorOpenLocal = driverDoor.parent != null
            ? Quaternion.Inverse(driverDoor.parent.rotation) * openWorld
            : openWorld;
        doorInitialized = true;
    }

    // Splits the visual into a sprung body and unsprung wheels. The truck
    // model's wheels are separate meshes named Wheel_* (build_truck_sketchfab.py).
    void BuildRig()
    {
        if (rigBuilt) return;
        rigBuilt = true;
        hull = GetComponent<BoxCollider>();
        var wheels = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Wheel_")) wheels.Add(r);

        // body pivot near axle height; every existing child moves under it
        bodyPivot = new GameObject("BodyPivot").transform;
        bodyPivot.SetParent(transform, false);
        bodyPivot.localPosition = new Vector3(0f, 0.75f, 0f);
        var kids = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in transform) if (c != bodyPivot) kids.Add(c);
        foreach (var c in kids) c.SetParent(bodyPivot, true);

        if (wheels.Count == 0) { Debug.LogWarning("D2D TRUCK no Wheel_ meshes; wheels stay static"); return; }
        float maxZ = float.MinValue;
        foreach (var r in wheels) maxZ = Mathf.Max(maxZ, transform.InverseTransformPoint(r.bounds.center).z);
        spinPivots = new Transform[wheels.Count];
        steerPivots = new Transform[wheels.Count];
        float rearSum = 0f; int rearN = 0;
        for (int i = 0; i < wheels.Count; i++)
        {
            Vector3 c = transform.InverseTransformPoint(wheels[i].bounds.center);
            bool front = c.z > maxZ - 0.5f;
            Transform parent = transform;
            if (front)
            {
                steerPivots[i] = new GameObject("Steer_" + wheels[i].name).transform;
                steerPivots[i].SetParent(transform, false);
                steerPivots[i].localPosition = c;
                parent = steerPivots[i];
            }
            else { rearSum += c.z; rearN++; }
            var spin = new GameObject("Spin_" + wheels[i].name).transform;
            spin.SetParent(parent, false);
            if (!front) spin.localPosition = c;
            spinPivots[i] = spin;
            wheels[i].transform.SetParent(spin, true);
        }
        if (rearN > 0)
        {
            rearAxleZ = rearSum / rearN;
            wheelBase = Mathf.Clamp(maxZ - rearAxleZ, 3.5f, 8f);
        }
        Debug.Log("D2D TRUCK wheels=" + wheels.Count + " wheelBase=" + wheelBase.ToString("F2") + " rearAxleZ=" + rearAxleZ.ToString("F2"));
    }

    // The fifth wheel's top must meet the trailer's upper coupler plate; the
    // trailer measures its plate at start-up and sets the height here.
    public void SetFifthWheelTop(float y)
    {
        fifthWheelTop = y;
        if (fifthWheel != null) fifthWheel.position = transform.TransformPoint(FifthWheelLocal);
    }

    public void SetHeadlights(Light[] lights)
    {
        headlights = lights;
    }

    public void SetDoor(bool open)
    {
        InitDoor();
        if (doorOpen == open || driverDoor == null) return;
        doorOpen = open;
        if (audio != null) audio.Door(open);
        StopAllCoroutines();
        StartCoroutine(DoorRoutine(open));
    }

    IEnumerator DoorRoutine(bool open)
    {
        Quaternion from = driverDoor.localRotation;
        Quaternion to = open ? doorOpenLocal : doorClosed;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2.6f;
            driverDoor.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        driverDoor.localRotation = to;
    }

    public void Enter()
    {
        if (driving) return;
        driving = true;
        doorOpen = true;
        if (driverDoor != null) driverDoor.localRotation = doorOpenLocal;
        if (player != null) player.EnterTruck(seat);
        if (flow != null) flow.OnEnteredTruck();
    }

    public void Exit()
    {
        if (!driving || Mathf.Abs(speed) > 0.35f) return;
        driving = false;
        engineOn = false;
        if (audio != null) audio.StopEngine();
        if (player != null)
        {
            Vector3 p = exitPoint != null ? exitPoint.position : transform.position + transform.right * -2.5f;
            Quaternion r = Quaternion.Euler(0f, transform.eulerAngles.y - 90f, 0f);
            player.ExitTruck(p, r);
        }
        if (flow != null) flow.OnExitedTruck();
    }

    public void ToggleDoor()
    {
        if (driving) return;
        SetDoor(!doorOpen);
    }

    public void StartEngine()
    {
        if (engineOn) return;
        engineOn = true;
        gear = 1; reverse = false; rpm = IdleRpm;
        if (audio != null) audio.StartEngine();
        if (flow != null) flow.OnEngineStarted();
    }

    public void StopEngine()
    {
        if (!engineOn) return;
        engineOn = false;
        speed = 0f;
        if (audio != null) audio.StopEngine();
    }

    static float Torque01(float r)
    {
        // heavy-duty diesel: flat peak 1000-1400 rpm, falling off above 1800
        if (r < 1000f) return Mathf.Lerp(0.62f, 1f, Mathf.InverseLerp(650f, 1000f, r));
        if (r < 1400f) return 1f;
        return Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(1400f, 2100f, r));
    }

    float RpmFor(int g, float v) => Mathf.Abs(v) / WheelRadius * Ratios[g] * FinalDrive * 60f / (2f * Mathf.PI);

    void Update()
    {
        if (!driving || controlLock) { UpdateVisuals(0f); return; }

        float v = Input.GetAxisRaw("Vertical");
        float throttleIn = Mathf.Clamp01(v + touchThrottle);
        float brakeIn = Mathf.Clamp01((v < -0.1f ? -v : 0f) + touchBrake);
        float steer = Mathf.Clamp(Input.GetAxisRaw("Horizontal") + touchSteer, -1f, 1f);
        bool parkBrake = Input.GetKey(KeyCode.Space);
        float dt = Time.deltaTime;

        // reverse: hold S at a standstill to select R, hold W at a standstill to go back to drive
        if (Mathf.Abs(speed) < 0.2f)
        {
            if (!reverse && brakeIn > 0.5f) { stoppedBrakeTime += dt; if (stoppedBrakeTime > 0.45f) { reverse = true; stoppedBrakeTime = 0f; } }
            else if (reverse && throttleIn > 0.5f) { stoppedBrakeTime += dt; if (stoppedBrakeTime > 0.45f) { reverse = false; stoppedBrakeTime = 0f; } }
            else stoppedBrakeTime = 0f;
        }
        else stoppedBrakeTime = 0f;
        // in reverse the pedals swap: S drives backwards, W brakes
        float drive = reverse ? brakeIn : throttleIn;
        float brake = reverse ? throttleIn : brakeIn;
        if (parkBrake) brake = Mathf.Max(brake, 0.55f);

        throttleSmooth = Mathf.MoveTowards(throttleSmooth, drive, (drive > throttleSmooth ? 2.2f : 4f) * dt);
        // air brakes take a moment to build pressure and to release
        brakeSmooth = Mathf.MoveTowards(brakeSmooth, brake, (brake > brakeSmooth ? 3.2f : 5f) * dt);

        float a = 0f;
        float vAbs = Mathf.Abs(speed);
        // coupled to a loaded 53 footer the rig weighs ~33 t instead of ~9 t bobtail
        bool loaded = hooked != null;
        float mass = loaded ? 33000f : 9000f;
        int minGear = loaded ? 0 : 1;
        if (engineOn)
        {
            if (!reverse)
            {
                // automated shifts: up near 1650 rpm (earlier on light throttle), down near 1050
                if (shiftTimer > 0f) shiftTimer -= dt;
                else
                {
                    float upAt = Mathf.Lerp(1350f, 1700f, throttleSmooth);
                    if (gear < Ratios.Length - 1 && RpmFor(gear, speed) > upAt)
                    {
                        int g = gear + 1;
                        // skip-shift when the next-but-one gear still pulls above 1150 rpm
                        if (g < Ratios.Length - 1 && RpmFor(g + 1, speed) > 1150f && throttleSmooth < 0.7f) g++;
                        gear = g; shiftTimer = 0.42f;
                    }
                    else if (gear > minGear && RpmFor(gear, speed) < (throttleSmooth > 0.3f ? 1050f : 800f))
                    {
                        gear--; shiftTimer = 0.3f;
                    }
                }
                if (vAbs < 0.5f && gear > minGear) gear = minGear;
            }
            int gi = reverse ? 0 : gear;
            float wheelRpm = RpmFor(gi, speed);
            float targetRpm = Mathf.Max(IdleRpm + throttleSmooth * (shiftTimer > 0f ? 0f : 250f) * (vAbs < 1.5f ? 1f : 0f), wheelRpm);
            if (shiftTimer > 0f) targetRpm = Mathf.Max(IdleRpm, Mathf.Lerp(rpm, wheelRpm, 0.5f));
            rpm = Mathf.Lerp(rpm, Mathf.Min(targetRpm, 2100f), 1f - Mathf.Exp(-9f * dt));

            // traction: wheel force through the gear, capped by tyre grip (bobtail)
            float force = (shiftTimer > 0f ? 0f : throttleSmooth) * Torque01(rpm) * 2350f * Ratios[gi] * FinalDrive * 0.85f / WheelRadius;
            float aDrive = Mathf.Min(force / mass, reverse ? (loaded ? 0.6f : 1.2f) : (loaded ? 1.1f : 2.3f));
            if (!reverse && vAbs > GovernedSpeed) aDrive = 0f;
            if (reverse && vAbs > 3.5f) aDrive = 0f;
            float engineBrake = throttleSmooth < 0.05f && vAbs > 1f ? 0.18f + 0.22f * Mathf.InverseLerp(900f, 1900f, rpm) : 0f;
            a = aDrive - engineBrake * (loaded ? 0.55f : 1f);
        }
        else rpm = Mathf.Lerp(rpm, 0f, 1f - Mathf.Exp(-2f * dt));

        float resist = (loaded ? 0.09f : 0.07f) + (loaded ? 9.5f : 6.5f) * 0.6f * vAbs * vAbs / mass;   // rolling + aero
        float brakeDecel = brakeSmooth * (loaded ? 5.0f : 6.8f);
        float dir = reverse ? -1f : 1f;
        // integrate along the selected direction, never letting drag/brakes reverse the motion
        float s = speed * dir;            // speed along the drive direction
        s += a * dt;
        float decel = (resist + brakeDecel) * dt;
        if (s > 0f) s = Mathf.Max(0f, s - decel); else s = Mathf.Min(0f, s + decel);
        speed = s * dir;
        if (Mathf.Abs(speed) < 0.02f && throttleSmooth < 0.02f) speed = 0f;

        // air hiss when the truck settles at a stop with the brake held
        if (Mathf.Abs(speed) < 0.05f && brakeSmooth > 0.4f) { if (!airHissDone && audio != null && lastSpeed > 0.5f) { audio.AirBrake(0.45f); airHissDone = true; } }
        else if (Mathf.Abs(speed) > 1f) airHissDone = false;

        Move(steer);

        if (hooked != null && !hooked.airSupplied && engineOn && !reverse && throttleSmooth > 0.3f && tugOffset > 0.075f)
        {
            // pulling against the trailer's spring brakes: a locked kingpin holds
            tugHold += dt;
            if (!hooked.tugPassed && tugHold > 1.0f) hooked.PassTug();
            else if (hooked.tugPassed && tugHold > 1.6f && Time.time - lastHeldMsg > 5f && flow != null)
            {
                lastHeldMsg = Time.time;
                flow.ShowSubtitle(hooked.linesConnected && hooked.gearUp
                    ? "The trailer brakes are still set. Push in the red trailer-air valve."
                    : "The trailer brakes are set: connect the air lines and raise the gear first.", 3f);
            }
        }
        else tugHold = Mathf.Max(0f, tugHold - dt * 2f);

        if (audio != null) audio.SetEngineLoad(engineOn ? Mathf.InverseLerp(IdleRpm, 2000f, rpm) : 0f, throttleSmooth > 0.05f && shiftTimer <= 0f);

        if (Input.GetKeyDown(KeyCode.H) && audio != null) audio.Horn();
        if (Input.GetKeyDown(KeyCode.F)) ToggleHeadlights();
    }

    void Move(float steerInput)
    {
        float dt = Time.deltaTime;
        float rate = Mathf.Abs(steerInput) > Mathf.Abs(steerSmooth) ? 1.6f : 2.4f;
        steerSmooth = Mathf.MoveTowards(steerSmooth, steerInput, rate * dt);
        float maxAngle = Mathf.Lerp(36f, 5f, Mathf.Clamp01(Mathf.Abs(speed) / 22f));
        float angle = steerSmooth * maxAngle * Mathf.Deg2Rad;
        float targetYaw = speed * Mathf.Tan(angle) / wheelBase * Mathf.Rad2Deg;
        yawRate = Mathf.Lerp(yawRate, targetYaw, 1f - Mathf.Exp(-8f * dt));

        Vector3 prevPos = transform.position;
        Quaternion prevRot = transform.rotation;
        float step = speed * dt;
        if (hooked != null && !hooked.airSupplied)
        {
            // trailer spring brakes are on: the rig only rocks on the kingpin slack
            float next = Mathf.Clamp(tugOffset + step, -0.04f, 0.09f);
            if (Mathf.Abs(next - tugOffset - step) > 1e-5f) { speed = 0f; yawRate = 0f; }
            step = next - tugOffset;
            tugOffset = next;
        }
        if (Mathf.Abs(step) > 1e-5f && Blocked(step, out float allowed))
        {
            // hit something solid: stop at the contact, with a jolt for the camera
            step = allowed;
            if (Mathf.Abs(speed) > 2.5f && audio != null) audio.AirBrake(0.3f);
            speed = 0f; throttleSmooth = 0f;
        }
        if (Mathf.Abs(step) > 1e-5f || Mathf.Abs(yawRate) > 0.01f)
        {
            // turn about the rear tandem so the tail tracks inside the cab, like a real tractor
            Vector3 pivot = transform.TransformPoint(new Vector3(0f, 0f, rearAxleZ));
            if (Mathf.Abs(speed) > 0.01f) transform.RotateAround(pivot, Vector3.up, yawRate * dt);
            transform.position += transform.forward * step;
        }
        if (hooked == null && hookTarget != null && step < 0f) CheckCoupling();
        if (hooked != null && !hooked.Follow(transform, FifthWheelLocal, dt))
        {
            // the trailer can't go there (jackknife or obstacle): take the step back
            transform.SetPositionAndRotation(prevPos, prevRot);
            speed = 0f; throttleSmooth = 0f; yawRate = 0f;
        }
        float a = dt > 1e-5f ? (speed - lastSpeed) / dt : 0f;
        longAccel = Mathf.Lerp(longAccel, Mathf.Clamp(a, -10f, 10f), 1f - Mathf.Exp(-4f * dt));
        lastSpeed = speed;
        UpdateVisuals(angle * Mathf.Rad2Deg);
    }

    // Two casts: the cab and sleeper at full height, and the frame, tandem and
    // fifth wheel kept low so the tractor can back under a trailer's upper
    // coupler. A coupled trailer moves with the tractor and is skipped.
    bool Blocked(float step, out float allowed)
    {
        allowed = step;
        if (hull == null) return false;
        float front = hull.center.z + hull.size.z * 0.5f, back = hull.center.z - hull.size.z * 0.5f;
        const float split = -2.35f;
        float w = hull.size.x * 0.96f;
        Vector3 dir = transform.forward * Mathf.Sign(step);
        float dist = Mathf.Abs(step) + 0.15f;
        float best = float.MaxValue;
        Cast(new Vector3(0f, 2.3f, (front + split) * 0.5f), new Vector3(w, Mathf.Max(0.5f, hull.size.y - 0.7f), front - split), dir, dist, ref best);
        Cast(new Vector3(0f, 0.68f, (split + back) * 0.5f), new Vector3(w, 0.62f, split - back), dir, dist, ref best);
        if (best == float.MaxValue) return false;
        allowed = Mathf.Sign(step) * Mathf.Max(0f, best - 0.15f);
        return true;
    }

    void Cast(Vector3 localCentre, Vector3 size, Vector3 dir, float dist, ref float best)
    {
        Vector3 centre = transform.TransformPoint(localCentre);
        var hits = Physics.BoxCastAll(centre, size * 0.5f, dir, transform.rotation, dist, ~(1 << 2), QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (hooked != null && h.collider.transform.IsChildOf(hooked.transform)) continue;
            if (h.distance <= 0f && h.point == Vector3.zero) continue;   // started overlapping
            if (h.distance < best) best = h.distance;
        }
    }

    // Backing under the trailer: when the fifth wheel reaches the kingpin the
    // jaws close if the kingpin is inside the throat (about +/-0.24 m and 12
    // degrees); otherwise the kingpin hits the plate and the tractor stops.
    void CheckCoupling()
    {
        var tr = hookTarget.transform;
        Vector3 lp = tr.InverseTransformPoint(transform.TransformPoint(FifthWheelLocal));
        float over = lp.z - hookTarget.kingpinLocal.z;
        float lateral = lp.x - hookTarget.kingpinLocal.x;
        if (over < 0f || over > 1.5f || Mathf.Abs(lateral) > 1.45f) return;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        float angle = Vector3.Angle(fwd, hookTarget.Forward);
        if (angle > 50f) return;
        transform.position += fwd * (over / Mathf.Max(0.3f, Mathf.Cos(angle * Mathf.Deg2Rad)));
        float hitSpeed = Mathf.Abs(speed);
        speed = 0f; throttleSmooth = 0f; yawRate = 0f;
        if (Mathf.Abs(lateral) <= 0.24f && angle <= 12f)
        {
            hooked = hookTarget;
            tugOffset = 0f; tugHold = 0f;
            Debug.Log("D2D HITCH locked lateral=" + lateral.ToString("F2") + " angle=" + angle.ToString("F1") + " speed=" + hitSpeed.ToString("F2"));
            hooked.Couple(this, hitSpeed);
        }
        else if (Time.time - lastMissTime > 2f)
        {
            lastMissTime = Time.time;
            if (audio != null) audio.PlaySfx("door_close", 0.5f, 0.6f);
            Debug.Log("D2D HITCH missed lateral=" + lateral.ToString("F2") + " angle=" + angle.ToString("F1"));
            if (flow != null) flow.OnKingpinMissed(lateral, angle);
        }
    }

    void LateUpdate()
    {
        // body sway keeps running while parked or when automation moves the truck
        float dt = Time.deltaTime;
        if (bodyPivot == null || dt <= 0f) return;
        float aLat = speed * yawRate * Mathf.Deg2Rad;
        float rollTarget = Mathf.Clamp(aLat * 0.6f, -2.4f, 2.4f);
        float pitchTarget = Mathf.Clamp(-longAccel * 0.28f, -1.3f, 1.3f);
        const float w = 2f * Mathf.PI * 1.15f, z = 0.42f;
        rollVel += (w * w * (rollTarget - rollAng) - 2f * z * w * rollVel) * dt; rollAng += rollVel * dt;
        pitchVel += (w * w * (pitchTarget - pitchAng) - 2f * z * w * pitchVel) * dt; pitchAng += pitchVel * dt;
        bodyPivot.localRotation = Quaternion.Euler(pitchAng, 0f, rollAng);
    }

    void UpdateVisuals(float roadAngleDeg)
    {
        float dt = Time.deltaTime;
        if (spinPivots != null)
        {
            wheelAngle = Mathf.Repeat(wheelAngle + speed / WheelRadius * Mathf.Rad2Deg * dt, 360f);
            for (int i = 0; i < spinPivots.Length; i++)
            {
                if (spinPivots[i] != null) spinPivots[i].localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
                if (steerPivots[i] != null) steerPivots[i].localRotation = Quaternion.Euler(0f, roadAngleDeg, 0f);
            }
        }
        if (wheelPivot == null)
        {
            var w = FindDeep(transform, "WheelPivot");
            if (w != null) { wheelPivot = w; wheelRest = w.localRotation; }
        }
        // steering ratio ~16:1: full lock at a crawl is about 1.6 turns of the wheel
        if (wheelPivot != null)
            wheelPivot.localRotation = wheelRest * Quaternion.Euler(0f, -roadAngleDeg * 16f, 0f);
        // cluster needles sweep 240 degrees: tach 0-3000 rpm, speedo 0-90 mph
        if (!gaugesFound)
        {
            tachNeedle = FindDeep(transform, "TachNeedle"); speedoNeedle = FindDeep(transform, "SpeedoNeedle");
            gaugesFound = true;
        }
        if (!lampFound)
        {
            var l = FindDeep(transform, "FifthWheelLamp");
            if (l != null) fifthWheelLamp = l.GetComponent<Renderer>();
            lampFound = true;
        }
        if (fifthWheelLamp != null && lampOn != (hooked != null))
        {
            lampOn = hooked != null;
            fifthWheelLamp.material.color = lampOn ? new Color(0.25f, 1f, 0.3f) : new Color(0.03f, 0.07f, 0.035f);
        }
        if (tachNeedle != null) tachNeedle.localRotation = Quaternion.Euler(0f, 0f, 120f - Mathf.Clamp01(rpm / 3000f) * 240f);
        if (speedoNeedle != null) speedoNeedle.localRotation = Quaternion.Euler(0f, 0f, 120f - Mathf.Clamp01(Mathf.Abs(speed) * 2.23694f / 90f) * 240f);
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
        return null;
    }

    void ToggleHeadlights()
    {
        if (headlights == null) return;
        headlightTimer = headlightTimer > 0 ? 0 : 1;
        foreach (var l in headlights) if (l != null) l.enabled = headlightTimer > 0;
    }
}
