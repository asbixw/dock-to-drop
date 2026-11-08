using UnityEngine;

// The player's visible body: a rigged CC0 character (Quaternius Universal Base
// Characters + Universal Animation Library) driven by legacy animation clips.
// First person hides only the head bone, so looking down shows the chest,
// arms and legs; third person (V) shows the whole driver.
public class DriverBody : MonoBehaviour
{
    public PlayerController pc;
    public Transform model;
    Animation anim;
    Transform head, rig;
    string idle, walk, jog, drive, interact;
    string current;
    Vector3 lastPos;
    float planarSpeed;
    float interactUntil;

    public static DriverBody Create(PlayerController pc)
    {
        var prefab = Resources.Load<GameObject>("Models/Driver");
        if (prefab == null) { Debug.LogWarning("D2D driver model missing"); return null; }
        var db = pc.gameObject.AddComponent<DriverBody>();
        db.pc = pc;
        var inst = Object.Instantiate(prefab, pc.transform);
        inst.name = "DriverModel";
        db.model = inst.transform;
        db.Setup();
        return db;
    }

    void Setup()
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        anim = model.GetComponentInChildren<Animation>();
        head = FindDeep(model, "Head");
        rig = anim != null ? anim.transform : model;
        if (anim != null)
        {
            foreach (AnimationState s in anim)
            {
                string n = s.name;
                if (n.EndsWith("Idle")) idle = n;
                else if (n.EndsWith("Walk")) walk = n;
                else if (n.EndsWith("Jog")) jog = n;
                else if (n.EndsWith("Drive")) drive = n;
                else if (n.EndsWith("Interact")) interact = n;
                s.wrapMode = n.EndsWith("Interact") ? WrapMode.Once : WrapMode.Loop;
            }
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
        }
        ApplyMaterials();
        // Face the model along +Z: sample the idle pose and point the toes forward.
        if (anim != null && idle != null)
        {
            anim.Play(idle);
            anim.Sample();
        }
        var foot = FindDeep(model, "foot_l");
        var ball = FindDeep(model, "ball_l");
        if (foot != null && ball != null)
        {
            Vector3 d = pc.transform.InverseTransformDirection(ball.position - foot.position);
            d.y = 0f;
            if (d.sqrMagnitude > 1e-6f)
                model.localRotation = Quaternion.FromToRotation(d.normalized, Vector3.forward) * model.localRotation;
        }
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        lastPos = transform.position;
        Debug.Log("D2D DRIVER clips idle=" + idle + " walk=" + walk + " jog=" + jog + " drive=" + drive + " head=" + (head != null));
    }

    void ApplyMaterials()
    {
        Texture2D skinTex = Resources.Load<Texture2D>("Textures/driver_skin");
        Texture2D skinNor = Resources.Load<Texture2D>("Textures/driver_skin_nor");
        Texture2D eyeTex = Resources.Load<Texture2D>("Textures/driver_eye");
        var skin = MeshFactory.Mat("DriverSkin", Color.white, 0f, 0.38f);
        if (skinTex != null) skin.mainTexture = skinTex; else skin.color = new Color(0.78f, 0.58f, 0.46f);
        if (skinNor != null) { skin.EnableKeyword("_NORMALMAP"); skin.SetTexture("_BumpMap", skinNor); }
        var shirt = MeshFactory.Mat("DriverShirt", new Color(0.12f, 0.13f, 0.15f), 0f, 0.14f);
        // worn hi-vis polyester: bright but not self-lit (the emission read as neon)
        var vest = MeshFactory.Mat("DriverVest", new Color(0.60f, 0.74f, 0.12f), 0f, 0.22f);
        var jeans = MeshFactory.Mat("DriverJeans", new Color(0.10f, 0.15f, 0.25f), 0f, 0.12f);
        var boots = MeshFactory.Mat("DriverBoots", new Color(0.19f, 0.12f, 0.065f), 0.0f, 0.34f);
        var hair = MeshFactory.Mat("DriverHair", new Color(0.055f, 0.042f, 0.032f), 0f, 0.22f);
        var eyes = MeshFactory.Mat("DriverEyes", Color.white, 0f, 0.75f);
        if (eyeTex != null) eyes.mainTexture = eyeTex;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                if (n.Contains("skin")) mats[i] = skin;
                else if (n.Contains("shirt")) mats[i] = shirt;
                else if (n.Contains("vest")) mats[i] = vest;
                else if (n.Contains("jeans")) mats[i] = jeans;
                else if (n.Contains("boots")) mats[i] = boots;
                else if (n.Contains("eyes")) mats[i] = eyes;
                else mats[i] = hair;
            }
            r.sharedMaterials = mats;
        }
    }

    public bool ThirdPerson { get; set; }

    bool visible = true;
    Renderer[] rends;
    /// Hidden when the follow camera is pushed right up against the driver.
    public bool Visible
    {
        get => visible;
        set
        {
            if (visible == value) return;
            visible = value;
            if (rends == null) rends = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends) if (r != null) r.enabled = value;
        }
    }
    public bool ForceHead { get; set; }   // composed autoshot frames

    public void PlayInteract()
    {
        if (anim == null || interact == null || pc.IsSeated) return;
        anim.CrossFade(interact, 0.15f);
        current = interact;
        interactUntil = Time.time + 1.4f;
    }

    void LateUpdate()
    {
        if (anim == null) return;
        Vector3 p = transform.position;
        Vector3 dv = p - lastPos; dv.y = 0f;
        lastPos = p;
        float inst = Time.deltaTime > 1e-5f ? dv.magnitude / Time.deltaTime : 0f;
        if (inst > 30f) inst = 0f; // teleports
        planarSpeed = Mathf.Lerp(planarSpeed, inst, 1f - Mathf.Exp(-10f * Time.deltaTime));

        // Locomotion is a speed-driven blend of idle, walk and jog with the two
        // gait cycles phase-locked (same normalized time), so speeding up or
        // slowing down never pops between clips or slides the feet.
        string special = pc.IsSeated ? (drive ?? idle) : (Time.time < interactUntil ? interact : null);
        if (special != null)
        {
            if (special != current) { anim.CrossFade(special, current == null ? 0f : 0.25f); current = special; }
            anim[special].speed = 1f;
        }
        else if (walk != null && jog != null && idle != null)
        {
            if (current != "loco")
            {
                if (current != null && current != idle && current != walk && current != jog) anim.Blend(current, 0f, 0.25f);
                current = "loco";
            }
            float sp = planarSpeed;
            float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.9f, sp));
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.7f, 4.6f, sp));
            var sw = anim[walk]; var sj = anim[jog];
            anim.Blend(idle, 1f - m, 0.12f);
            anim.Blend(walk, m * (1f - w), 0.12f);
            anim.Blend(jog, m * w, 0.12f);
            // clip ground speeds: walk ~1.55 m/s, jog ~4.6 m/s
            float rw = Mathf.Clamp(sp / 1.55f, 0.55f, 1.9f), rj = Mathf.Clamp(sp / 4.6f, 0.7f, 1.35f);
            float ns = Mathf.Lerp(rw / Mathf.Max(0.1f, sw.length), rj / Mathf.Max(0.1f, sj.length), w);
            sw.normalizedSpeed = ns; sj.normalizedSpeed = ns;
            sj.normalizedTime = sw.normalizedTime;
            anim[idle].speed = 1f;
        }
        else
        {
            string want = planarSpeed > 3.6f && jog != null ? jog : planarSpeed > 0.35f ? walk : idle;
            if (want != null && want != current) { anim.CrossFade(want, current == null ? 0f : 0.25f); current = want; }
        }

        if (head != null)
            head.localScale = (ForceHead || (ThirdPerson && !pc.IsSeated) || pc.ChaseCam) ? Vector3.one : Vector3.one * 0.001f;

        // Seated: the drive clip's head sits ~1.24 m above the rig root and
        // ~0.35 m behind the hands; place the rig so the head lands on the
        // camera (player origin = eye point in the seat).
        if (pc.IsSeated)
        {
            model.localPosition = Vector3.Lerp(model.localPosition, new Vector3(0f, -1.36f, 0.27f), 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
        else
        {
            model.localPosition = Vector3.Lerp(model.localPosition, Vector3.zero, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
        return null;
    }
}
