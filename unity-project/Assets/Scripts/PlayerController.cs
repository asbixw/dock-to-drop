using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Camera cam;
    public float walkSpeed = 2.6f;
    public float runSpeed = 5.4f;
    // Exponential response rates (1/s): the first step lands at once, the
    // last few percent of speed take a moment, so starts read as weighty
    // without the input feeling late.
    public float accelRate = 13f;
    public float decelRate = 11f;
    public DriverBody body;
    public float lookSensitivity = 2.0f;
    public float gravity = -18f;
    public AudioManager audioManager;

    public Vector2 touchMove;
    public Vector2 touchLook;
    public bool touchRun;

    CharacterController cc;
    float pitch;
    float yaw;
    float vy;
    float stepTimer;
    bool seated;
    bool uiLock;
    bool wantCursorLock = true;
    bool cursorFree;
    Vector3 camHome;
    Transform seatParent;
    Vector3 planarVel;
    Vector2 smoothLook;
    float bobPhase, bobAmount;
    float baseFov = -1f;
    bool thirdPerson;
    bool tpInit;
    Vector3 seatHeadOffset;
    bool chaseCam;
    Vector3 chasePos;
    bool chaseInit;
    float chaseDist; bool chaseDistInit;
    bool forceFirstPerson;
    // third person: the body turns toward the camera-relative input
    // (Starter Assets-style), the camera keeps its own yaw
    float bodyYaw, bodyYawVel;
    float tpDist = 3.9f;
    Vector3 tpPivot;
    bool aimLock;
    Vector3 aimPoint;
    float aimLockUntil;
    // Title screen: the camera leaves the player and shows a composed yard view.
    // Without this the camera sits at the first-person eye point while the
    // driver model is visible in third person, so the menu frames the inside of
    // the driver's head and arms.
    bool menuView;
    int menuSavedCullingMask = -1;

    public bool MenuView => menuView;
    public bool IsSeated => seated;
    bool thirdPersonBeforeSeat;
    TruckController seatTruck;
    public bool ControlEnabled { get; set; } = true;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cam != null) camHome = cam.transform.localPosition;
        yaw = transform.eulerAngles.y;
    }

    // Smoothed camera points are world positions; keep them in the shifted frame
    // so the chase and third-person cameras do not fly in from the old origin.
    void OnEnable() { FloatingOrigin.OnShift += OnOriginShift; }
    void OnDisable() { FloatingOrigin.OnShift -= OnOriginShift; }
    void OnOriginShift(Vector3 shift)
    {
        chasePos -= shift;
        tpPivot -= shift;
        aimPoint -= shift;
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;
        lookSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 2.0f);
        LockCursor(true);
        // On foot the player starts in third person so the driver is visible;
        // automation keeps first person unless -thirdperson is passed.
        bool tpArg = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-thirdperson");
        if (!Application.isMobilePlatform && (!AutomationRun || tpArg)) SetThirdPerson(true);
    }

    bool UseThirdPerson => thirdPerson && !forceFirstPerson;
    public bool ChaseCam => seated && chaseCam && !forceFirstPerson;

    /// <summary>
    /// Compose the title screen from a fixed yard camera. The player's body is
    /// hidden while the menu owns the view; <see cref="EndMenuView"/> puts the
    /// camera back on the player before the shift starts. Gameplay cameras are
    /// untouched: this only detaches the transform for the duration of the menu.
    /// </summary>
    public void ShowMenuView(Vector3 eye, Vector3 look, float fov)
    {
        menuView = true;
        if (body != null)
        {
            body.Visible = false;
            if (body.model != null) body.model.gameObject.SetActive(false);
        }
        if (cam == null) return;
        // Belt and braces: a scripted camera move or a late renderer toggle must
        // never put the driver mesh in the title view. The body has its own
        // layer, so the menu camera simply does not see it.
        if (menuSavedCullingMask < 0) menuSavedCullingMask = cam.cullingMask;
        cam.cullingMask = menuSavedCullingMask & ~(1 << DriverBody.Layer);
        cam.transform.SetParent(null, true);
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
        cam.fieldOfView = fov;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Restore the normal first/third-person camera after the menu.</summary>
    public void EndMenuView()
    {
        if (!menuView) return;
        menuView = false;
        if (body != null && body.model != null)
        {
            body.model.gameObject.SetActive(true);
            body.Visible = true;
            body.ThirdPerson = thirdPerson && !forceFirstPerson;
        }
        if (cam != null)
        {
            if (menuSavedCullingMask >= 0) cam.cullingMask = menuSavedCullingMask;
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = camHome.sqrMagnitude > 0.01f ? camHome : new Vector3(0f, 1.66f, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        tpInit = false;
        chaseInit = false;
        aimLock = false;
    }

    /// Eye point used for interaction rays in third person.
    public Vector3 EyeWorld => transform.position + transform.rotation *
        ((camHome.sqrMagnitude > 0.01f ? camHome : new Vector3(0f, 1.66f, 0f)) + new Vector3(0f, 0f, 0.14f));

    /// Reference point for interaction reach and waypoint distance. On foot this
    /// is the driver's eye; seated it is the head, which is the seat transform.
    public Vector3 InteractOrigin => seated ? transform.position : EyeWorld;

    /// The phone camera always frames from the eyes.
    public bool ForceFirstPerson
    {
        get => forceFirstPerson;
        set
        {
            if (forceFirstPerson == value) return;
            forceFirstPerson = value;
            tpInit = false; chaseInit = false;
            if (body != null) body.ThirdPerson = thirdPerson && !value;
            if (body != null) body.Visible = !menuView;
            if (cam == null) return;
            if (seated) { cam.transform.localPosition = Vector3.zero; ApplySeatedLook(); }
            else if (!UseThirdPerson)
            {
                cam.transform.localPosition = (camHome.sqrMagnitude > 0.01f ? camHome : new Vector3(0f, 1.66f, 0f)) + new Vector3(0f, 0f, 0.14f);
                cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }
    }

    public void Teleport(Vector3 pos, float newYaw)
    {
        // CharacterController caches its own position; toggling it guarantees the
        // teleport is applied before the next interaction ray is cast.
        if (cc != null) cc.enabled = false;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        if (cc != null) cc.enabled = true;
        vy = 0f;
        yaw = newYaw;
        bodyYaw = newYaw;
        pitch = 0f;
        aimLock = false;
        if (cam != null) cam.transform.localRotation = Quaternion.identity;
        // A teleport is a cut, not a camera move. Snap the chase pivot so the
        // next frame does not show the camera flying across the lot.
        if (thirdPerson) { tpInit = false; tpPivot = transform.position + Vector3.up * 1.52f; }
    }

    public void LookAt(Vector3 worldPoint)
    {
        if (seated || cam == null) return;
        if (UseThirdPerson)
        {
            // Aim the actual chase camera, not the driver's eyes. The camera
            // position depends on yaw/pitch, so solve it with a fixed-point
            // iteration. The chase camera also shortens and collides, so the
            // final look direction is locked to the requested point for a
            // moment; that keeps the centre ray exactly on the target instead
            // of a few degrees off after the camera settles.
            float y = yaw, p = pitch;
            for (int i = 0; i < 10; i++)
            {
                Vector3 camPos = ThirdPersonCameraPosition(y, p);
                Vector3 d = worldPoint - camPos;
                if (d.sqrMagnitude < 0.001f) break;
                y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                p = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            }
            yaw = y;
            pitch = Mathf.Clamp(p, -40f, 65f);
            bodyYaw = yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            aimLock = true;
            aimPoint = worldPoint;
            aimLockUntil = Time.unscaledTime + 1.5f;
            Vector3 eyeCam = ThirdPersonCameraPosition(yaw, pitch);
            Vector3 look = worldPoint - eyeCam;
            if (look.sqrMagnitude > 0.0001f)
            {
                cam.transform.position = eyeCam;
                cam.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
            return;
        }
        // First person: the camera child's world position can still be the
        // pre-teleport one, so derive the eye from the player transform.
        // The eye carries a small forward offset, so turning the body moves
        // it. Iterate: aim, turn, re-derive the eye, aim again. A single pass
        // leaves the centre ray off target when the previous view (chase cam
        // or a different yaw) was far away, which is exactly the seal-photo
        // case at arm's length.
        Vector3 eye = transform.position + transform.rotation * cam.transform.localPosition;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = worldPoint - eye;
            if (dir.sqrMagnitude < 0.001f) return;
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            bodyYaw = yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            eye = transform.position + transform.rotation * cam.transform.localPosition;
        }
    }

    static readonly RaycastHit[] camHits = new RaycastHit[32];

    /// <summary>
    /// Swept camera probe that ignores the player and the rig they are standing
    /// beside. Without the filter the follow arm treats the truck and trailer as
    /// walls and snaps in every time the driver walks past their own equipment.
    /// </summary>
    bool CameraBlocked(Vector3 origin, float radius, Vector3 dir, float dist, out float hitDistance)
    {
        hitDistance = dist;
        bool hitSomething = false;
        int mask = ~((1 << 2) | (1 << DriverBody.Layer));
        int n = Physics.SphereCastNonAlloc(origin, radius, dir, camHits, dist, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = camHits[i].collider;
            if (c == null) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<DriverBody>() != null) continue;
            if (c.GetComponentInParent<TruckController>() != null) continue;
            if (c.GetComponentInParent<TrailerRig>() != null) continue;
            float d = camHits[i].distance <= 0f ? 0f : camHits[i].distance;
            if (d < hitDistance) { hitDistance = d; hitSomething = true; }
        }
        return hitSomething;
    }

    Vector3 ThirdPersonCameraPosition(float yawDeg, float pitchDeg)
    {
        Vector3 pivot = transform.position + Vector3.up * 1.52f;
        float p = Mathf.Clamp(pitchDeg, -40f, 65f);
        Quaternion orbit = Quaternion.Euler(p, yawDeg, 0f);
        Vector3 shoulder = pivot + orbit * new Vector3(0.55f, 0.32f, 0f);
        float d = tpInit ? Mathf.Max(0.35f, tpDist) : 3.9f;
        return shoulder + orbit * Vector3.back * d;
    }

    public void SetLook(float yawDeg, float pitchDeg)
    {
        aimLock = false;
        yaw = yawDeg;
        pitch = Mathf.Clamp(pitchDeg, -78f, 78f);
        if (seated)
        {
            ApplySeatedLook();
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cam != null) cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    public bool CursorFree => cursorFree;

    // Explicit release/capture. A released cursor stays released until the
    // player clicks to resume, even if a menu closes or the window regains focus.
    public void ReleaseCursor()
    {
        cursorFree = true;
        ApplyCursor(false);
    }

    public const string SensitivityKey = "D2D.MouseSensitivity";
    public float Sensitivity
    {
        get { return lookSensitivity; }
        set { SetSensitivity(value); }
    }

    public void SetSensitivity(float value)
    {
        lookSensitivity = Mathf.Clamp(value, 0.2f, 5f);
        PlayerPrefs.SetFloat(SensitivityKey, lookSensitivity);
        PlayerPrefs.Save();
    }

    public void AdjustSensitivity(float delta)
    {
        SetSensitivity(lookSensitivity + delta);
    }

    public void CaptureCursor()
    {
        cursorFree = false;
        // A menu, phone, guard panel or pause overlay owns the pointer. Closing
        // or entering the cab must never steal it back while one of those is up.
        if (uiLock || menuView)
        {
            ApplyCursor(false);
            return;
        }
        ApplyCursor(true);
    }

    public void LockCursor(bool locked)
    {
        if (locked && cursorFree) return;
        ApplyCursor(locked);
    }

    void ApplyCursor(bool locked)
    {
        wantCursorLock = locked;
        if (Application.isMobilePlatform) return;
        // Never hide the cursor while another app has the keyboard: the player
        // would be left with no cursor and no keys. OnApplicationFocus re-locks
        // as soon as the game becomes active.
        if (locked && !Application.isFocused && !Application.isBatchMode && !AutomationRun) locked = false;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void SetUILock(bool locked)
    {
        uiLock = locked;
        // Menus always hand the mouse back. Closing one never grabs it again;
        // GameFlow decides when the player has explicitly clicked to resume.
        ReleaseCursor();
    }

    static readonly bool AutomationRun = System.Array.Exists(System.Environment.GetCommandLineArgs(),
        a => a == "-autoshot" || a == "-autoplay" || a == "-uishot" || a == "-perfprobe");

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !Application.isMobilePlatform)
        {
            // give the cursor back to the desktop; wantCursorLock remembers to re-lock
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        if (hasFocus && wantCursorLock && !uiLock && !cursorFree && !Application.isMobilePlatform)
            ApplyCursor(true);
    }

    public void SetCameraHome(Vector3 localPos)
    {
        camHome = localPos;
        if (cam != null && !seated) cam.transform.localPosition = localPos;
    }

    public void AddLook(Vector2 delta)
    {
        touchLook += delta;
    }

    void Update()
    {
        if (uiLock || menuView) return;

        float mx = 0f;
        float my = 0f;
        if (Application.isMobilePlatform)
        {
            mx = touchLook.x * 0.06f;
            my = touchLook.y * 0.06f;
        }
        else if (Cursor.lockState == CursorLockMode.Locked)
        {
            mx = Input.GetAxisRaw("Mouse X") * lookSensitivity + touchLook.x * 0.06f;
            my = Input.GetAxisRaw("Mouse Y") * lookSensitivity + touchLook.y * 0.06f;
        }
        touchLook = Vector2.zero;
        // Light exponential smoothing (~25 ms) takes the jitter out of raw mouse
        // deltas without adding noticeable lag.
        float k = 1f - Mathf.Exp(-60f * Time.deltaTime);
        smoothLook = Vector2.Lerp(smoothLook, new Vector2(mx, my), k);
        if (AutomationRun) smoothLook = new Vector2(mx, my);
        mx = smoothLook.x; my = smoothLook.y;
        if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f) aimLock = false;

        yaw += mx;
        pitch = Mathf.Clamp(pitch - my, -78f, 78f);
        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        if (baseFov < 0f) baseFov = cam.fieldOfView;
        if (seated)
        {
            // seated: the head turns over the shoulder and down to the pedals,
            // while the truck itself turns the body
            if (Input.GetKeyDown(KeyCode.V) && !Application.isMobilePlatform) SetChaseCam(!chaseCam);
            ApplySeatedLook();
            return;
        }
        if (Input.GetKeyDown(KeyCode.V) && !Application.isMobilePlatform) SetThirdPerson(!thirdPerson);

        if (!UseThirdPerson) { bodyYaw = yaw; transform.rotation = Quaternion.Euler(0f, yaw, 0f); }

        if (!ControlEnabled) { UpdateCameraFeel(0f, false); return; }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        if (Application.isMobilePlatform)
        {
            x = touchMove.x;
            z = touchMove.y;
        }
        Vector2 move = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
        bool run = Input.GetKey(KeyCode.LeftShift) || touchRun;
        float speed = run ? runSpeed : walkSpeed;
        Vector3 world;
        if (UseThirdPerson)
        {
            // Move along the input direction relative to the camera and turn the
            // body to face it, so the forward walk clip always matches the motion.
            float inputYaw = yaw + Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
            if (move.sqrMagnitude > 0.01f)
                bodyYaw = Mathf.SmoothDampAngle(bodyYaw, inputYaw, ref bodyYawVel, run ? 0.14f : 0.10f);
            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            world = Quaternion.Euler(0f, inputYaw, 0f) * Vector3.forward * move.magnitude;
        }
        else world = transform.right * move.x + transform.forward * move.y;
        // Body weight: velocity eases toward the target instead of snapping.
        Vector3 target = world * speed;
        float rate = target.sqrMagnitude > planarVel.sqrMagnitude ? accelRate : decelRate;
        planarVel = Vector3.Lerp(planarVel, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
        planarVel = Vector3.MoveTowards(planarVel, target, 0.6f * Time.deltaTime);
        if (cc.isGrounded && vy < 0) vy = -2f;
        vy += gravity * Time.deltaTime;
        cc.Move((planarVel + Vector3.up * vy) * Time.deltaTime);
        float spd = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        UpdateCameraFeel(spd, run && move.magnitude > 0.1f);

        if (move.magnitude > 0.1f && cc.isGrounded)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                stepTimer = run ? 0.32f : 0.5f;
                if (audioManager != null) audioManager.PlayFootstep(run ? 0.80f : 0.65f);
            }
        }
        if (transform.position.y < -4f) transform.position = new Vector3(transform.position.x, 1.0f, transform.position.z);
    }

    void UpdateCameraFeel(float spd, bool running)
    {
        // Head bob: a soft figure-eight tied to stride, fading in with speed.
        float targetAmt = cc.isGrounded ? Mathf.Clamp01(spd / runSpeed) : 0f;
        bobAmount = Mathf.Lerp(bobAmount, targetAmt, 1f - Mathf.Exp(-6f * Time.deltaTime));
        bobPhase += Time.deltaTime * Mathf.Lerp(5.2f, 9.0f, Mathf.Clamp01((spd - walkSpeed) / (runSpeed - walkSpeed))) * Mathf.Clamp01(spd / 0.8f);
        float amp = Mathf.Lerp(0.018f, 0.034f, bobAmount);
        Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * amp * 0.55f, Mathf.Abs(Mathf.Sin(bobPhase)) * amp - amp * 0.5f, 0f) * bobAmount;
        float fovTarget = baseFov + (running ? 4.5f : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovTarget, 1f - Mathf.Exp(-4f * Time.deltaTime));
        Vector3 home = camHome.sqrMagnitude > 0.01f ? camHome : new Vector3(0f, 1.66f, 0f);
        if (UseThirdPerson)
        {
            // Over-the-shoulder follow camera. The pivot trails the body a touch
            // (weight), the arm pulls back at a run, and on collision the arm
            // snaps in at once but eases back out, so it never sits inside a
            // wall or pole for a frame (GDC "50 Camera Mistakes").
            Vector3 pivotRaw = transform.position + Vector3.up * 1.52f;
            if (!tpInit) { tpPivot = pivotRaw; tpDist = 3.9f; }
            tpPivot = Vector3.Lerp(tpPivot, pivotRaw, 1f - Mathf.Exp(-14f * Time.deltaTime));
            float p = Mathf.Clamp(pitch, -40f, 65f);
            Quaternion orbit = Quaternion.Euler(p, yaw, 0f);
            // a little higher and further when looking down, closer when looking up
            float full = Mathf.Lerp(3.9f, 4.5f, Mathf.Clamp01(spd / runSpeed)) * Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(-p / 40f));
            Vector3 shoulder = tpPivot + orbit * new Vector3(0.55f, 0.32f, 0f);
            Vector3 back = orbit * Vector3.back;
            float wantDist = full;
            // first keep the shoulder offset itself out of walls
            Vector3 shDir = shoulder - tpPivot;
            if (shDir.sqrMagnitude > 1e-6f && CameraBlocked(tpPivot, 0.18f, shDir.normalized, shDir.magnitude, out float shDist))
                shoulder = tpPivot + shDir.normalized * Mathf.Max(0f, shDist - 0.05f);
            // A sphere sweep against the booth, barrier arm and fence stops the
            // camera crossing their faces, not just their pivot point.
            if (CameraBlocked(shoulder, 0.24f, back, full, out float camDist))
                wantDist = Mathf.Max(0.35f, camDist - 0.08f);
            if (!tpInit) { tpDist = wantDist; tpInit = true; }
            tpDist = wantDist < tpDist ? wantDist : Mathf.Lerp(tpDist, wantDist, 1f - Mathf.Exp(-3.5f * Time.deltaTime));
            cam.transform.position = shoulder + back * tpDist + bob * 0.35f;
            cam.transform.rotation = orbit;
            if (aimLock)
            {
                if (Time.unscaledTime >= aimLockUntil) aimLock = false;
                else
                {
                    Vector3 look = aimPoint - cam.transform.position;
                    if (look.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                }
            }
            // fade the driver out rather than filling the screen with his back
            if (body != null) body.Visible = tpDist > 1.0f;
        }
        else
        {
            // eye sits slightly ahead of the neck so the chest is visible below
            cam.transform.localPosition = home + new Vector3(0f, 0f, 0.14f) + bob;
        }
    }

    public bool ThirdPerson => thirdPerson;
    public bool ThirdPersonActive => UseThirdPerson && !seated;
    public Vector3 ViewDir => Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

    public void SetThirdPerson(bool on)
    {
        aimLock = false;
        thirdPerson = on;
        tpInit = false;
        bodyYaw = yaw; bodyYawVel = 0f;
        if (body != null) body.Visible = !menuView;
        if (body != null) body.ThirdPerson = on && !forceFirstPerson;
        if (!on && cam != null) cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void SetChaseCam(bool on)
    {
        chaseCam = on;
        chaseInit = false;
        if (!on && cam != null && seated) { cam.transform.localPosition = Vector3.zero; ApplySeatedLook(); }
    }

    void LateUpdate()
    {
        if (!seated || cam == null || seatParent == null || uiLock || cam.transform.parent != transform) return;
        // Seated head inertia: the head lags the cab a touch when the truck
        // turns or brakes, then settles (critically damped, no bounce).
        if (seatTruck == null) seatTruck = seatParent.GetComponentInParent<TruckController>();
        var tc = seatTruck;
        if (tc == null) return;
        if (ChaseCam)
        {
            // Chase view: high behind the cab, eased so the rig swings into turns.
            var tr = tc.transform;
            Quaternion orbit = tr.rotation * Quaternion.Euler(Mathf.Clamp(pitch, -20f, 35f) + 10f, yaw, 0f);
            Vector3 pivot = tr.position + Vector3.up * 3.2f + tr.forward * -1.2f;
            Vector3 want = pivot + orbit * new Vector3(0f, 0.8f, -13.5f);
            want.y = Mathf.Max(want.y, tr.position.y + 1.2f);
            if (!chaseInit) { chasePos = want; chaseInit = true; }
            chasePos = Vector3.Lerp(chasePos, want, 1f - Mathf.Exp(-7f * Time.deltaTime));
            // keep the chase camera out of buildings and trailers: pull in at once, ease back out
            Vector3 toCam = chasePos - pivot;
            float full = toCam.magnitude, allowed = full;
            if (full > 0.01f && CameraBlocked(pivot, 0.45f, toCam / full, full, out float chaseHit))
                allowed = Mathf.Max(2.0f, chaseHit - 0.1f);
            if (!chaseDistInit) { chaseDist = allowed; chaseDistInit = true; }
            chaseDist = allowed < chaseDist ? allowed : Mathf.Lerp(chaseDist, allowed, 1f - Mathf.Exp(-3f * Time.deltaTime));
            chasePos = pivot + toCam / Mathf.Max(0.01f, full) * Mathf.Max(2.5f, chaseDist);
            cam.transform.position = chasePos;
            cam.transform.rotation = Quaternion.LookRotation(pivot + tr.forward * 3f - chasePos, Vector3.up);
            return;
        }
        float yawRate = tc.YawRate, accelLong = tc.LongAccel;
        Vector3 targetOff = new Vector3(Mathf.Clamp(-yawRate * 0.0009f * Mathf.Abs(tc.speed), -0.05f, 0.05f), 0f,
                                        Mathf.Clamp(-accelLong * 0.012f, -0.05f, 0.05f));
        seatHeadOffset = Vector3.Lerp(seatHeadOffset, targetOff, 1f - Mathf.Exp(-5f * Time.deltaTime));
        cam.transform.localPosition = seatHeadOffset;
    }

    void ApplySeatedLook()
    {
        // clamp the stored values too, so swinging past the limit does not
        // leave a dead zone to unwind
        if (!ChaseCam) yaw = Mathf.Clamp(yaw, -115f, 115f);
        pitch = Mathf.Clamp(pitch, -50f, 60f);
        cam.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void EnterTruck(Transform seat)
    {
        seated = true;
        seatParent = seat;
        cc.enabled = false;
        ControlEnabled = false;
        thirdPersonBeforeSeat = thirdPerson;
        if (thirdPerson) SetThirdPerson(false);
        transform.SetParent(seat, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
        pitch = 0f;
        yaw = 0f;
        aimLock = false;
        CaptureCursor();
    }

    public void ExitTruck(Vector3 worldPos, Quaternion worldRot)
    {
        transform.SetParent(null, true);
        transform.position = worldPos;
        transform.rotation = Quaternion.Euler(0f, worldRot.eulerAngles.y, 0f);
        yaw = transform.eulerAngles.y;
        bodyYaw = yaw;
        pitch = 8f;
        cam.transform.localPosition = camHome.sqrMagnitude > 0.01f ? camHome : new Vector3(0f, 1.66f, 0f);
        cam.transform.localRotation = Quaternion.identity;
        planarVel = Vector3.zero;
        tpInit = false;
        seated = false;
        seatParent = null;
        chaseInit = false;
        cc.enabled = true;
        ControlEnabled = true;
        if (thirdPersonBeforeSeat) SetThirdPerson(true);
        aimLock = false;
        CaptureCursor();
    }
}
