using UnityEngine;

// The player's visible body: a rigged CC0 character (Quaternius Universal Base
// Characters + Universal Animation Library) driven by legacy animation clips.
// First person hides only the head bone, so looking down shows the chest,
// arms and legs; third person (V) shows the whole driver.
public class DriverBody : MonoBehaviour
{
    // The driver lives on its own layer so the title camera can exclude him
    // even if a renderer is briefly re-enabled by a scripted shot.
    public const int Layer = 8;
    public PlayerController pc;
    public Transform model;
    Animation anim;
    Transform head, rig;
    string idle, walk, jog, drive, sit, interact;
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
        SetLayerRecursive(inst.transform, Layer);
        db.Setup();
        return db;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayerRecursive(c, layer);
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
                else if (n.EndsWith("Sit")) sit = n;
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
        Debug.Log("D2D DRIVER clips idle=" + idle + " walk=" + walk + " jog=" + jog + " drive=" + drive
            + " sit=" + sit + " interact=" + interact + " head=" + (head != null));
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
            visible = value;
            if (model == null) return;
            if (rends == null) rends = model.GetComponentsInChildren<Renderer>(true);
            // Always apply: a previous CineShot or menu may have disabled the
            // renderers without changing the cached bool.
            foreach (var r in rends) if (r != null) r.enabled = value;
        }
    }
    public bool ForceHead { get; set; }   // composed autoshot frames

    public void PlayInteract()
    {
        if (anim == null || interact == null || pc.IsSeated) return;
        anim.CrossFade(interact, 0.15f);
        current = interact;
        // Let the clip finish instead of cutting it at a guessed 1.4 s; stop it
        // early only if the player starts walking away.
        interactUntil = Time.time + Mathf.Max(0.4f, anim[interact].length * 0.92f);
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

        // Seated: Sit while the rig is parked, Drive once it rolls. Both clips
        // keep the hands on the wheel; the crossfade keeps a stop from snapping.
        string special;
        if (pc.IsSeated)
        {
            bool rolling = planarSpeed > 0.35f;
            special = rolling ? (drive ?? sit ?? idle) : (sit ?? drive ?? idle);
        }
        else
        {
            // A real interaction plays through; walking away cancels it so the
            // feet never slide through the last half of the clip.
            if (Time.time < interactUntil && planarSpeed > 0.35f) interactUntil = 0f;
            special = Time.time < interactUntil ? interact : null;
        }
        if (special != null)
        {
            if (special != current)
            {
                float fade = (current == sit || current == drive) ? 0.30f : 0.25f;
                anim.CrossFade(special, current == null ? 0f : fade);
                current = special;
            }
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
        // On foot: use GroundContact so the soles follow the same pavement the
        // player collides with. Flat ground gives zero, so the common case is
        // unchanged; slab edges and slopes no longer float or sink the feet.
        Vector3 target;
        if (pc.IsSeated) target = new Vector3(0f, -1.36f, 0.27f);
        else
        {
            float ground = GroundContact.HeightAt(transform.position, transform, null, transform.position.y);
            target = new Vector3(0f, ground - transform.position.y, 0f);
        }
        model.localPosition = Vector3.Lerp(model.localPosition, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
        return null;
    }
}
