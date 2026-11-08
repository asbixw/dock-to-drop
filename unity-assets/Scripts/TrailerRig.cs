using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// The 53 ft trailer the shift hooks (A-14).
// Parked, it stands on its landing gear. Coupled, it hangs off the tractor's
// fifth wheel: every frame the tandem follows the kingpin along a tractrix
// (the axle can only roll along its own heading), so the trailer swings out
// behind the tractor, its wheels track inside the tractor's path on turns, and
// reversing is as unstable as the real thing. The coupling steps of the shift
// live here too: jaws locked, tug test, air lines, landing gear, trailer air.
// All dimensions are measured from the model's meshes at start-up, so they
// stay right whatever scale the FBX imports at.
public class TrailerRig : MonoBehaviour
{
    public GameFlow flow;
    public TruckController tractor;
    public bool coupled, tugPassed, linesConnected, gearUp, airSupplied;
    public bool Cranking { get; private set; }
    public float Articulation { get; private set; }     // degrees, tractor relative to trailer

    // measured, in this transform's space (nose toward -z, doors at +z)
    public Vector3 kingpinLocal;      // kingpin centre; y = underside of the upper coupler plate
    public float axleLocalZ, noseLocalZ, rearLocalZ;
    public float halfWidth = 1.3f, bodyTop = 4.1f;
    public Vector3 crankLocal;
    public float crankSide = 1f;

    const float MaxArticulation = 84f, GearStroke = 0.34f;
    float L, wheelRadius = 0.5f, wheelAngle, gearTravel;
    float wheelBottom = 0.01f;
    float rollAng, rollVel, lastYaw, latAccel;
    Vector3 lastAxle;
    BoxCollider gearCollider;
    Vector3 gearCenterRest;
    float gearHeight;
    readonly List<Transform> wheelPivots = new List<Transform>();
    readonly List<Transform> gearParts = new List<Transform>();
    readonly List<Vector3> gearRest = new List<Vector3>();
    Transform crankArm;
    Vector3 bodyCentre, bodyHalf;
    readonly Collider[] overlap = new Collider[24];
    string lastBlocker;

    public Vector3 KingpinWorld => transform.TransformPoint(kingpinLocal);
    public Vector3 Forward { get { var f = -transform.forward; f.y = 0f; return f.normalized; } }
    public Vector3 CrankWorld => transform.TransformPoint(crankLocal);
    public Vector3 CrankStandWorld => transform.TransformPoint(crankLocal + new Vector3(crankSide * 1.25f, 0f, 0f));
    // curb side: facing the nose (local -z), the right hand is local -x
    public string CrankSideName => crankSide < 0f ? "right" : "left";
    public Vector3 GladHandWorld(int i) => transform.TransformPoint(new Vector3(-0.62f + i * 0.20f, kingpinLocal.y + 0.42f, noseLocalZ - 0.05f));

    public void Init(GameFlow f)
    {
        flow = f;
        Bounds kp = default, plate = default, side = default, crank = default, feet = default;
        bool hasKp = false, hasPlate = false, hasSide = false, hasCrank = false, hasFeet = false;
        var wheelRends = new List<Renderer>();
        Material wallMat = null;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            Bounds b = LocalBounds(r);
            if (n.StartsWith("KingpinPlate")) Grow(ref plate, ref hasPlate, b);
            else if (n.StartsWith("Kingpin")) Grow(ref kp, ref hasKp, b);
            else if (n.StartsWith("SideWall")) { Grow(ref side, ref hasSide, b); wallMat = r.sharedMaterial; }
            else if (n.StartsWith("LandingCrank")) Grow(ref crank, ref hasCrank, b);
            else if (n.StartsWith("LandingLeg") || n.StartsWith("LandingFoot"))
            {
                gearParts.Add(r.transform);
                gearRest.Add(transform.InverseTransformPoint(r.transform.position));
                if (n.StartsWith("LandingFoot")) Grow(ref feet, ref hasFeet, b);
            }
            else if (n.StartsWith("Wheel") || n.StartsWith("Lug")) wheelRends.Add(r);
            // the model's front wall was exported lying flat; a standing one is added below
            else if (n.StartsWith("FrontWall")) r.enabled = false;
        }
        if (hasSide)
        {
            halfWidth = Mathf.Max(-side.min.x, side.max.x);
            bodyTop = side.max.y;
            noseLocalZ = side.min.z;
            rearLocalZ = side.max.z;
        }
        else { noseLocalZ = -8.1f; rearLocalZ = 8.1f; }
        float plateBottom = hasPlate ? plate.min.y : (hasKp ? kp.max.y : 1.1f);
        Vector3 kc = hasKp ? kp.center : (hasPlate ? plate.center : new Vector3(0f, 0f, noseLocalZ + 0.9f));
        kingpinLocal = new Vector3(0f, plateBottom, kc.z);

        // wheels: group meshes by hub and spin each group about its own centre
        var groups = new List<List<Renderer>>();
        var centres = new List<Vector3>();
        foreach (var r in wheelRends)
        {
            Vector3 c = transform.InverseTransformPoint(r.bounds.center);
            int gi = -1;
            for (int i = 0; i < centres.Count; i++)
                if (Mathf.Abs(centres[i].x - c.x) < 0.45f && Mathf.Abs(centres[i].z - c.z) < 0.45f) { gi = i; break; }
            if (gi < 0) { groups.Add(new List<Renderer>()); centres.Add(c); gi = groups.Count - 1; }
            groups[gi].Add(r);
        }
        float minWheelY = float.MaxValue;
        foreach (var r in wheelRends)
            minWheelY = Mathf.Min(minWheelY, transform.InverseTransformPoint(r.bounds.min).y);
        if (wheelRends.Count > 0 && minWheelY < float.MaxValue) wheelBottom = minWheelY;

        float axleSum = 0f;
        for (int i = 0; i < groups.Count; i++)
        {
            Renderer big = groups[i][0];
            foreach (var r in groups[i]) if (r.bounds.size.sqrMagnitude > big.bounds.size.sqrMagnitude) big = r;
            Vector3 hub = transform.InverseTransformPoint(big.bounds.center);
            wheelRadius = Mathf.Max(0.3f, big.bounds.extents.y);
            var piv = new GameObject("WheelSpin").transform;
            piv.SetParent(transform, false);
            piv.localPosition = hub;
            foreach (var r in groups[i]) r.transform.SetParent(piv, true);
            wheelPivots.Add(piv);
            axleSum += hub.z;
        }
        axleLocalZ = groups.Count > 0 ? axleSum / groups.Count : rearLocalZ - 2.2f;
        L = Mathf.Max(6f, axleLocalZ - kingpinLocal.z);

        // a standing nose wall, glad hands and the seven-way socket
        if (hasSide)
        {
            MeshFactory.Box("NoseWall", new Vector3(halfWidth * 2f - 0.02f, side.size.y, 0.10f),
                new Vector3(0f, side.center.y, noseLocalZ + 0.05f), wallMat, transform, false);
        }
        var red = MeshFactory.Mat("GladHandRed", new Color(0.62f, 0.06f, 0.04f), 0.3f, 0.45f);
        var blue = MeshFactory.Mat("GladHandBlue", new Color(0.06f, 0.16f, 0.48f), 0.3f, 0.45f);
        var dark = MeshFactory.Mat("SocketBlack", new Color(0.04f, 0.04f, 0.045f), 0.2f, 0.4f);
        var steel = MeshFactory.Mat("CrankSteel", new Color(0.45f, 0.47f, 0.49f), 0.85f, 0.4f);
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = transform.InverseTransformPoint(GladHandWorld(i)) + new Vector3(0f, 0f, 0.02f);
            MeshFactory.Box(i == 0 ? "GladHandEmergency" : i == 1 ? "GladHandService" : "SevenWaySocket",
                i < 2 ? new Vector3(0.08f, 0.05f, 0.07f) : new Vector3(0.09f, 0.09f, 0.06f), p, i == 0 ? red : i == 1 ? blue : dark, transform, false);
        }

        // crank handle that turns while the gear goes up
        if (hasCrank)
        {
            crankSide = crank.center.x >= 0f ? 1f : -1f;
            crankLocal = crank.center;
            crankArm = new GameObject("CrankArm").transform;
            crankArm.SetParent(transform, false);
            crankArm.localPosition = new Vector3(crankSide > 0f ? crank.max.x : crank.min.x, crank.center.y, crank.center.z);
            MeshFactory.Box("CrankArmBar", new Vector3(0.03f, 0.27f, 0.03f), new Vector3(0f, -0.12f, 0f), steel, crankArm, false);
            MeshFactory.CylRot("CrankKnob", 0.022f, 0.12f, new Vector3(crankSide * 0.06f, -0.24f, 0f), Quaternion.Euler(0f, 0f, 90f), dark, crankArm);
        }
        else
        {
            crankLocal = new Vector3(halfWidth - 0.2f, 1.5f, kingpinLocal.z + 3f);
        }
        var crankGo = new GameObject("InteractCrank");
        crankGo.transform.SetParent(transform, false);
        crankGo.transform.localPosition = crankLocal + new Vector3(crankSide * 0.55f, 0f, 0f);
        var cc = crankGo.AddComponent<BoxCollider>(); cc.isTrigger = true; cc.size = new Vector3(0.6f, 0.8f, 0.8f);
        var ci = crankGo.AddComponent<Interactable>(); ci.id = "crank"; ci.prompt = "Crank up the landing gear  [E]";
        ci.onInteract.AddListener(TryCrank);

        // colliders: the body sits above the upper coupler so a tractor frame can slide under it
        float bodyBottom = plateBottom + 0.01f;
        bodyCentre = new Vector3(0f, (bodyBottom + bodyTop) * 0.5f, (noseLocalZ + rearLocalZ) * 0.5f);
        bodyHalf = new Vector3(halfWidth, (bodyTop - bodyBottom) * 0.5f, (rearLocalZ - noseLocalZ) * 0.5f);
        foreach (var old in GetComponents<BoxCollider>()) Destroy(old);
        var body = gameObject.AddComponent<BoxCollider>(); body.center = bodyCentre; body.size = bodyHalf * 2f + new Vector3(0.1f, 0f, 0f);
        var running = gameObject.AddComponent<BoxCollider>();
        running.center = new Vector3(0f, bodyBottom * 0.5f, (axleLocalZ - 1.2f + rearLocalZ + 0.6f) * 0.5f);
        running.size = new Vector3(halfWidth * 2f, bodyBottom, rearLocalZ + 0.6f - (axleLocalZ - 1.2f));
        if (hasFeet)
        {
            gearCollider = gameObject.AddComponent<BoxCollider>();
            gearCenterRest = new Vector3(0f, bodyBottom * 0.5f, feet.center.z);
            gearHeight = bodyBottom;
            gearCollider.center = gearCenterRest;
            gearCollider.size = new Vector3(feet.size.x + 0.1f, gearHeight, 0.6f);
        }
        lastAxle = AxleGround();
        lastYaw = transform.eulerAngles.y;
        Debug.Log("D2D TRAILER kingpin=" + kingpinLocal.ToString("F2") + " axleZ=" + axleLocalZ.ToString("F2") + " nose=" + noseLocalZ.ToString("F2")
            + " rear=" + rearLocalZ.ToString("F2") + " halfW=" + halfWidth.ToString("F2") + " top=" + bodyTop.ToString("F2")
            + " wheels=" + wheelPivots.Count + " r=" + wheelRadius.ToString("F2") + " gearParts=" + gearParts.Count + " crank=" + crankLocal.ToString("F2"));
    }

    Bounds LocalBounds(Renderer r)
    {
        Bounds w = r.bounds;
        Vector3 mn = Vector3.one * float.MaxValue, mx = -mn;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 l = transform.InverseTransformPoint(c);
            mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l);
        }
        var b = new Bounds(); b.SetMinMax(mn, mx); return b;
    }

    static void Grow(ref Bounds acc, ref bool has, Bounds b)
    {
        if (!has) { acc = b; has = true; } else acc.Encapsulate(b);
    }

    Vector3 AxleGround()
    {
        Vector3 a = transform.TransformPoint(new Vector3(0f, 0f, axleLocalZ));
        return GroundAt(a.x, a.z);
    }

    Vector3 GroundAt(float x, float z)
    {
        float g = GroundContact.HeightAt(new Vector3(x, 0f, z), transform, tractor != null ? tractor.transform : null, 0f);
        return new Vector3(x, g - wheelBottom, z);
    }

    // The jaws have closed on the kingpin. The kingpin is pulled into the
    // throat, so the trailer pivots about its tandem to line up with it.
    public void Couple(TruckController t, float hitSpeed)
    {
        tractor = t;
        coupled = true;
        Vector3 hitch = t.transform.TransformPoint(t.FifthWheelLocal);
        Vector3 axle = AxleGround();
        Vector3 d = hitch - axle; d.y = 0f;
        Vector3 f = d.sqrMagnitude > 1e-4f ? d.normalized : Forward;
        Vector3 noseAxle = hitch - f * L;
        noseAxle.y = GroundAt(noseAxle.x, noseAxle.z).y;
        Pose(noseAxle, f, hitch.y, out var pos, out var rot);
        transform.SetPositionAndRotation(pos, rot);
        lastAxle = AxleGround();
        lastYaw = transform.eulerAngles.y;
        Articulation = Vector3.SignedAngle(f, Flat(t.transform.forward), Vector3.up);
        if (flow != null) flow.OnCoupled(hitSpeed);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

    void Pose(Vector3 axleGround, Vector3 f, float hitchY, out Vector3 pos, out Quaternion rot)
    {
        // the nose rides on the fifth wheel; the tandem stays on the ground
        float pitch = Mathf.Atan2(hitchY - (axleGround.y + kingpinLocal.y), L) * Mathf.Rad2Deg;
        rot = Quaternion.LookRotation(-f, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(0f, 0f, rollAng);
        pos = axleGround - rot * new Vector3(0f, 0f, axleLocalZ);
    }

    // Called by the tractor after it moves. Returns false when the move would
    // jackknife the rig or push the trailer into something solid; the tractor
    // then takes its step back.
    public bool Follow(Transform tr, Vector3 fifthWheelLocal, float dt)
    {
        Vector3 hitch = tr.TransformPoint(fifthWheelLocal);
        Vector3 axle = AxleGround();
        Vector3 d = hitch - axle; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return true;
        Vector3 f = d.normalized;
        float art = Vector3.SignedAngle(f, Flat(tr.forward), Vector3.up);
        if (Mathf.Abs(art) > MaxArticulation && Mathf.Abs(art) > Mathf.Abs(Articulation))
        {
            if (flow != null) flow.OnJackknife();
            return false;
        }
        Vector3 newAxle = hitch - f * L;
        newAxle.y = GroundAt(newAxle.x, newAxle.z).y;
        Pose(newAxle, f, hitch.y, out var pos, out var rot);
        if (Blocked(pos, rot, tr)) return false;

        float moved = Vector3.Dot(newAxle - lastAxle, f);
        wheelAngle = Mathf.Repeat(wheelAngle - moved / wheelRadius * Mathf.Rad2Deg, 360f);
        foreach (var p in wheelPivots) p.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        if (dt > 1e-4f)
        {
            float yaw = Quaternion.LookRotation(-f).eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt * Mathf.Deg2Rad;
            latAccel = Mathf.Lerp(latAccel, -moved / dt * yawRate, 1f - Mathf.Exp(-6f * dt));
            lastYaw = yaw;
        }
        lastAxle = newAxle;
        transform.SetPositionAndRotation(pos, rot);
        Articulation = art;
        return true;
    }

    bool Blocked(Vector3 pos, Quaternion rot, Transform tr)
    {
        Vector3 c = pos + rot * bodyCentre;
        int n = Physics.OverlapBoxNonAlloc(c, bodyHalf * 0.97f, overlap, rot, ~(1 << 2), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = overlap[i];
            if (col == null) continue;
            var t = col.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(tr)) continue;
            if (flow != null && flow.Player != null && t.IsChildOf(flow.Player.transform)) continue;
            if (lastBlocker != col.name) { lastBlocker = col.name; Debug.Log("D2D TRAILER blocked by " + col.name); }
            if (flow != null) flow.OnTrailerBlocked();
            return true;
        }
        return false;
    }

    void LateUpdate()
    {
        if (!coupled) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // a loaded box leans on its springs in a turn
        float target = Mathf.Clamp(latAccel * 0.55f, -2.2f, 2.2f);
        const float w = 2f * Mathf.PI * 0.9f, z = 0.35f;
        rollVel += (w * w * (target - rollAng) - 2f * z * w * rollVel) * dt;
        rollAng += rollVel * dt;
        if (!tractor.driving || Mathf.Abs(tractor.speed) < 0.01f)
        {
            // settle the roll while parked; the tractor only calls Follow when it moves
            latAccel = Mathf.Lerp(latAccel, 0f, 1f - Mathf.Exp(-4f * dt));
            Vector3 hitch = tractor.transform.TransformPoint(tractor.FifthWheelLocal);
            Pose(AxleGround(), Forward, hitch.y, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }
    }

    public void PassTug()
    {
        if (tugPassed) return;
        tugPassed = true;
        if (flow != null) flow.OnTugPassed();
    }

    public void ConnectLines()
    {
        if (linesConnected) return;
        if (!coupled) { if (flow != null) flow.ShowSubtitle("Back the tractor under the trailer first.", 2.6f); return; }
        linesConnected = true;
        if (tractor != null && tractor.lines != null) tractor.lines.Connect(this);
        if (flow != null) flow.OnLinesConnected();
    }

    public void TryCrank()
    {
        if (gearUp || Cranking) return;
        if (!coupled || !tugPassed)
        {
            if (flow != null) flow.ShowSubtitle("Not yet. Lock the fifth wheel and pass a tug test before the gear comes up,\nor the trailer drops on its nose.", 4f);
            return;
        }
        StartCoroutine(Crank());
    }

    IEnumerator Crank()
    {
        Cranking = true;
        if (flow != null && flow.Audio != null) flow.Audio.PlaySfx("crank_ratchet", 0.75f);
        float start = gearTravel;
        float remaining = 3.1f * (1f - Mathf.Clamp01(start / GearStroke));
        float t = 0f;
        while (t < remaining)
        {
            t += Time.deltaTime;
            // Letting go of the handle stops the gear where it is. Walk away and
            // the crank does not keep turning by itself.
            if (flow != null && flow.Player != null && !flow.Player.IsSeated &&
                Vector3.Distance(flow.Player.transform.position, CrankStandWorld) > 2.2f)
            {
                Cranking = false;
                flow.ShowSubtitle("You stepped away from the crank. The gear stays where you left it.", 3f);
                yield break;
            }
            gearTravel = Mathf.SmoothStep(start, GearStroke, t / Mathf.Max(0.01f, remaining));
            ApplyGear();
            if (crankArm != null) crankArm.localRotation = Quaternion.Euler(-t * 360f * 1.6f, 0f, 0f);
            yield return null;
        }
        gearTravel = GearStroke;
        ApplyGear();
        Cranking = false;
        gearUp = true;
        if (flow != null) flow.OnGearRaised();
    }

    void ApplyGear()
    {
        for (int i = 0; i < gearParts.Count; i++)
            if (gearParts[i] != null) gearParts[i].position = transform.TransformPoint(gearRest[i] + Vector3.up * gearTravel);
        if (gearCollider != null)
        {
            gearCollider.enabled = !gearUp;
            gearCollider.center = gearCenterRest + Vector3.up * gearTravel;
        }
    }

    public void SupplyAir()
    {
        if (airSupplied || !linesConnected || !gearUp) return;
        airSupplied = true;
        if (flow != null) flow.OnTrailerAir();
    }
}
