using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Spawns the realistic CC0 props (Poly Haven, converted by build_props.py)
// and rebuilds their PBR materials from Resources/Textures/Props.
public static class PropLibrary
{
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, string> kinds = new Dictionary<string, string>();
    static readonly Dictionary<string, Material> tinted = new Dictionary<string, Material>();
    static Shader foliage;

    public static bool Has(string name) { return Prefab(name) != null; }

    static GameObject Prefab(string name)
    {
        if (!prefabs.TryGetValue(name, out var p))
        {
            p = Resources.Load<GameObject>("Models/Props/" + name);
            prefabs[name] = p;
            if (p == null) Debug.LogWarning("D2D prop missing: " + name);
        }
        return p;
    }

    public static GameObject Spawn(string name, Vector3 pos, float yaw, float scale, Transform parent, bool collider = false)
    {
        var p = Prefab(name);
        if (p == null) return null;
        var go = Object.Instantiate(p, parent);
        go.name = name;
        go.transform.position = pos;
        // Keep the import rotation and scale: Blender FBX roots arrive rotated
        // -90 deg about X, and overwriting it laid every tree on its side.
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * p.transform.rotation;
        go.transform.localScale = p.transform.localScale * scale;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = Remap(ms[i]);
            r.sharedMaterials = ms;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
        if (collider)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var bc = go.AddComponent<BoxCollider>();
            bool tree = name.StartsWith("Tree") || name == "PowerPole";
            Vector3 size = go.transform.InverseTransformVector(b.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (tree) size = new Vector3(0.6f, size.y, 0.6f);
            bc.size = size;
            bc.center = go.transform.InverseTransformPoint(b.center);
            if (tree) bc.center = new Vector3(0f, bc.center.y, 0f);
        }
        return go;
    }

    // Spawn a vehicle and give its "paint" materials (marked in <mat>_pbr.txt) this
    // colour, so parked copies of one model don't look cloned.
    public static GameObject Spawn(string name, Vector3 pos, float yaw, float scale, Transform parent, bool collider, Color paint)
    {
        var go = Spawn(name, pos, yaw, scale, parent, collider);
        if (go == null) return null;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials; bool hit = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null || !kinds.TryGetValue(ms[i].name, out var k) || k != "paint") continue;
                string key = ms[i].name + "#" + ColorUtility.ToHtmlStringRGB(paint);
                if (!tinted.TryGetValue(key, out var t))
                {
                    t = new Material(ms[i]) { name = ms[i].name };
                    t.color = paint;
                    tinted[key] = t;
                }
                ms[i] = t; hit = true;
            }
            if (hit) r.sharedMaterials = ms;
        }
        return go;
    }

    static readonly string[] trees = { "Tree_Oak_A", "Tree_Oak_B", "Tree_Small", "Tree_Oak_A", "Tree_Oak_B" };
    static readonly float[] treeScale = { 1.6f, 1.95f, 1.55f, 1.6f, 1.95f };

    // A real tree with a cheap distance LOD. The variant, yaw and size jitter
    // come from the position so the world is the same on every run.
    public static GameObject SpawnTree(Vector3 pos, float scale, Transform parent)
    {
        int h = Mathf.Abs((int)(pos.x * 73.1f) ^ (int)(pos.z * 37.7f));
        int v = h % trees.Length;
        string n = trees[v];
        if (!Has(n)) return null;
        float s = scale * treeScale[v] * (0.9f + (h % 7) * 0.035f);
        float yaw = (h % 360);
        var root = new GameObject("Tree").transform;
        root.SetParent(parent, false);
        root.position = pos;
        var lod0 = Spawn(n, pos, yaw, s, root);
        var lod1 = Has(n + "_LOD1") ? Spawn(n + "_LOD1", pos, yaw, s, root) : null;
        var group = root.gameObject.AddComponent<LODGroup>();
        // Cross-fading between two different tree meshes also cross-fades the
        // shadow caster, which made roadside shadows blink. Hard-switch the LODs
        // instead; the switch is far enough away that the pop is invisible.
        group.fadeMode = LODFadeMode.None;
        group.animateCrossFading = false;
        var r0 = lod0.GetComponentsInChildren<Renderer>();
        if (lod1 != null)
        {
            var r1 = lod1.GetComponentsInChildren<Renderer>();
            group.SetLODs(new[] { new LOD(0.10f, r0), new LOD(0.006f, r1) });
        }
        else group.SetLODs(new[] { new LOD(0.01f, r0) });
        group.RecalculateBounds();
        var col = root.gameObject.AddComponent<CapsuleCollider>();
        col.radius = 0.35f * s; col.height = 4f * s; col.center = new Vector3(0f, 2f * s, 0f);
        return root.gameObject;
    }

    static Material Remap(Material src)
    {
        if (src == null) return null;
        string n = src.name.Replace(" (Instance)", "");
        int sp = n.IndexOf(' ');
        if (sp > 0) n = n.Substring(0, sp);
        if (mats.TryGetValue(n, out var m)) return m;
        var diff = Resources.Load<Texture2D>("Textures/Props/" + n + "_diff");
        var nor = Resources.Load<Texture2D>("Textures/Props/" + n + "_nor");
        var msk = Resources.Load<Texture2D>("Textures/Props/" + n + "_ms");
        var pbr = Resources.Load<TextAsset>("Textures/Props/" + n + "_pbr");
        if (pbr != null)
        {
            m = PbrMaterial(n, pbr.text, diff, nor, msk);
            mats[n] = m;
            return m;
        }
        string l = n.ToLowerInvariant();
        bool leafy = l.Contains("leaves") || l.Contains("shrub") || l.Contains("twig") || (l.Contains("searsia") && !l.EndsWith("searsia_lucida"));
        if (leafy)
        {
            if (foliage == null) foliage = Shader.Find("DockToDrop/Foliage");
            m = new Material(foliage != null ? foliage : Shader.Find("Standard")) { name = n };
            if (diff != null) m.SetTexture("_MainTex", diff);
            if (nor != null) m.SetTexture("_BumpMap", nor);
            m.SetFloat("_Glossiness", 0.22f);
            m.SetColor("_Color", new Color(0.92f, 0.95f, 0.88f));
            m.SetFloat("_Wind", l.Contains("shrub") ? 0.02f : 0.045f);
        }
        else
        {
            m = new Material(Shader.Find("Standard")) { name = n };
            if (diff != null) m.mainTexture = diff;
            if (nor != null) { m.EnableKeyword("_NORMALMAP"); m.SetTexture("_BumpMap", nor); }
            if (msk != null)
            {
                m.EnableKeyword("_METALLICGLOSSMAP");
                m.SetTexture("_MetallicGlossMap", msk);
                m.SetFloat("_GlossMapScale", 1f);
                m.SetTexture("_OcclusionMap", msk);
                m.SetFloat("_OcclusionStrength", 0.8f);
            }
            else m.SetFloat("_Glossiness", 0.3f);
        }
        if (diff == null) Debug.LogWarning("D2D prop texture missing: " + n);
        mats[n] = m;
        return m;
    }

    // Sketchfab props (build_vehicles.py) describe each material in one line:
    // "r g b a metallic smoothness kind", colour in linear space, kind one of
    // opaque, paint, glass or lamp. Textures, when present, override the factors.
    static Material PbrMaterial(string n, string text, Texture2D diff, Texture2D nor, Texture2D msk)
    {
        var f = text.Trim().Split(' ');
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        float P(int i) { return i < f.Length && float.TryParse(f[i], System.Globalization.NumberStyles.Float, inv, out var v) ? v : 1f; }
        string kind = f.Length > 6 ? f[6] : "opaque";
        kinds[n] = kind;
        if (kind == "glass") { var g = MeshFactory.GlassMat(n); g.name = n; return g; }
        var m = new Material(Shader.Find("Standard")) { name = n };
        m.color = new Color(P(0), P(1), P(2), 1f).gamma;
        m.SetFloat("_Metallic", Mathf.Clamp01(P(4)));
        m.SetFloat("_Glossiness", Mathf.Clamp(P(5), 0f, 0.92f));
        if (diff != null) m.mainTexture = diff;
        if (nor != null) { m.EnableKeyword("_NORMALMAP"); m.SetTexture("_BumpMap", nor); }
        if (msk != null)
        {
            m.EnableKeyword("_METALLICGLOSSMAP");
            m.SetTexture("_MetallicGlossMap", msk);
            m.SetFloat("_GlossMapScale", 0.92f);
        }
        if (kind == "lamp") { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", m.color * 0.4f); }
        return m;
    }
}
