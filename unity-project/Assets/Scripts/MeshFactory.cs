using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public static class MeshFactory
{
    public static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.45f,
        Texture2D albedo = null, Texture2D normal = null, Vector2 tiling = default(Vector2))
    {
        var shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var m = new Material(shader) { name = name };
        if (m.HasProperty("_Color")) m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (albedo != null)
        {
            if (m.HasProperty("_MainTex")) m.mainTexture = albedo;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
            Vector2 t = tiling == default(Vector2) ? Vector2.one : tiling;
            m.mainTextureScale = t;
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", t);
        }
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_BumpScale", 0.9f);
        }
        return m;
    }

    public static Material TexMat(string name, Texture2D albedo, Texture2D normal, Vector2 tiling, float smoothness = 0.3f)
    {
        var m = Mat(name, Color.white, 0f, smoothness, albedo, normal, tiling);
        if (normal != null)
        {
            m.SetTextureScale("_BumpMap", tiling);
        }
        return m;
    }

    public static Material TransparentMat(string name, Color color, float smoothness = 0.95f)
    {
        var m = Mat(name, color, 0f, smoothness);
        m.SetFloat("_Mode", 3f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        return m;
    }

    public static Material GlassMat(string name)
    {
        var sh = Shader.Find("DockToDrop/Glass");
        if (sh == null) return TransparentMat(name, new Color(0.16f, 0.24f, 0.30f, 0.42f), 0.95f);
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_Color")) m.color = new Color(0.16f, 0.24f, 0.30f, 0.42f);
        return m;
    }

    public static Material ClearGlassMat(string name)
    {
        var sh = Shader.Find("DockToDrop/Glass");
        if (sh == null) return TransparentMat(name, new Color(0.55f, 0.62f, 0.66f, 0.10f), 0.95f);
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_Color")) m.color = new Color(0.55f, 0.62f, 0.66f, 0.10f);
        return m;
    }

    public static Material SignMat(string name, Texture2D tex, Color tint)
    {
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_MainTex")) m.mainTexture = tex;
        if (m.HasProperty("_Color")) m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        return m;
    }

    // Opaque, single-sided sign face. Sprites/Default is transparent and
    // double-sided; for a sign with a white backing board that allowed the
    // face to be composited more than once (the doubled "1515" report).
    public static Material SignOpaqueMat(string name, Texture2D tex, Color tint)
    {
        var sh = Shader.Find("Unlit/Texture");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_MainTex")) m.mainTexture = tex;
        if (m.HasProperty("_Color")) m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        return m;
    }

    public static GameObject Box(string name, Vector3 size, Vector3 pos, Material mat, Transform parent = null, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        if (!collider) Object.Destroy(go.GetComponent<BoxCollider>());
        return go;
    }

    public static GameObject BoxRot(string name, Vector3 size, Vector3 pos, Quaternion rot, Material mat, Transform parent = null, bool collider = true)
    {
        var go = Box(name, size, pos, mat, parent, collider);
        go.transform.localRotation = rot;
        return go;
    }

    public static GameObject Cyl(string name, float radius, float height, Vector3 pos, Material mat, Transform parent = null, bool collider = false, int segments = 20)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!collider) Object.Destroy(go.GetComponent<CapsuleCollider>());
        return go;
    }

    public static GameObject CylRot(string name, float radius, float height, Vector3 pos, Quaternion rot, Material mat, Transform parent = null, bool collider = false)
    {
        var go = Cyl(name, radius, height, pos, mat, parent, collider);
        go.transform.localRotation = rot;
        return go;
    }

    public static GameObject Sphere(string name, float radius, Vector3 pos, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(go.GetComponent<SphereCollider>());
        return go;
    }

    public static GameObject Torus(string name, float major, float minor, Vector3 pos, Quaternion rot,
        Material mat, Transform parent = null, int majorSeg = 32, int minorSeg = 10)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        var verts = new Vector3[majorSeg * minorSeg];
        var norms = new Vector3[verts.Length];
        var uv = new Vector2[verts.Length];
        for (int i = 0; i < majorSeg; i++)
        {
            float u = Mathf.PI * 2f * i / majorSeg;
            var center = new Vector3(Mathf.Cos(u) * major, 0f, Mathf.Sin(u) * major);
            for (int j = 0; j < minorSeg; j++)
            {
                float v = Mathf.PI * 2f * j / minorSeg;
                var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                int k = i * minorSeg + j;
                verts[k] = center + n * minor;
                norms[k] = n;
                uv[k] = new Vector2((float)i / majorSeg, (float)j / minorSeg);
            }
        }
        var tris = new int[majorSeg * minorSeg * 6];
        int t = 0;
        for (int i = 0; i < majorSeg; i++)
            for (int j = 0; j < minorSeg; j++)
            {
                int a = i * minorSeg + j;
                int b = i * minorSeg + (j + 1) % minorSeg;
                int c = ((i + 1) % majorSeg) * minorSeg + (j + 1) % minorSeg;
                int d = ((i + 1) % majorSeg) * minorSeg + j;
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = a; tris[t++] = c; tris[t++] = d;
            }
        var mesh = new Mesh { name = name + "Mesh" };
        mesh.vertices = verts; mesh.normals = norms; mesh.uv = uv; mesh.triangles = tris;
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    public static GameObject Quad(string name, Vector2 size, Vector3 pos, Quaternion rot, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Object.Destroy(go.GetComponent<MeshCollider>());
        return go;
    }

    public static TextMesh Text3D(string name, string text, Vector3 pos, float size, Color color, Transform parent = null, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = Font();
        if (tm.font == null) { Debug.LogError("No built-in font available for TextMesh"); return tm; }
        tm.fontSize = 96;
        tm.characterSize = size * 0.1f;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.richText = false;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        if (tm.font != null) mr.sharedMaterial = TextMat(tm.font);
        return tm;
    }

    static Material _textMat;
    static Material TextMat(Font font)
    {
        if (_textMat != null) return _textMat;
        var sh = Shader.Find("DockToDrop/Text3D");
        if (sh == null) return font.material;
        _textMat = new Material(sh) { name = "Text3DMat" };
        _textMat.mainTexture = font.material.mainTexture;
        _textMat.renderQueue = 3000;
        return _textMat;
    }

    static Font _font;
    public static Font Font()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    public static void FaceBack(TextMesh tm)
    {
        if (tm == null) return;
        tm.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var s = tm.transform.localScale; s.x = -Mathf.Abs(s.x); tm.transform.localScale = s;
    }

    public static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    public static void AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
    {
        var bc = go.AddComponent<BoxCollider>();
        bc.center = center;
        bc.size = size;
    }

    public static GameObject Empty(string name, Vector3 pos, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        return go;
    }

    /// Surface of revolution from a (radius, height) profile, bottom to top.
    public static GameObject Lathe(string name, Vector2[] profile, Vector3 pos, Material mat, Transform parent = null, int segments = 24)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        for (int j = 0; j < profile.Length; j++)
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(Mathf.Cos(a) * profile[j].x, profile[j].y, Mathf.Sin(a) * profile[j].x));
                uvs.Add(new Vector2((float)i / segments, (float)j / (profile.Length - 1)));
            }
        int row = segments + 1;
        for (int j = 0; j < profile.Length - 1; j++)
            for (int i = 0; i < segments; i++)
            {
                int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        var m = new Mesh { name = name };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        return go;
    }

    /// Extrudes a closed (z, y) profile along local X from x0 to x1, in the
    /// parent's local space. Corners sharper than smoothDeg keep hard normals;
    /// the end caps are ear-clipped, so the profile may be concave (no
    /// self-intersections, no collinear points). UVs are in metres.
    public static GameObject Extrude(string name, Vector2[] zy, float x0, float x1, Material mat, Transform parent, float smoothDeg = 40f, bool caps = true)
    {
        int n = zy.Length;
        float area = 0f;
        for (int i = 0; i < n; i++) { var a = zy[i]; var b = zy[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
        if (area < 0f) { zy = (Vector2[])zy.Clone(); System.Array.Reverse(zy); }
        var en = new Vector3[n];   // outward normal of edge i -> i+1
        for (int i = 0; i < n; i++)
        {
            var d = zy[(i + 1) % n] - zy[i];
            en[i] = new Vector3(0f, -d.x, d.y).normalized;
        }
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        void Tri(int a, int b, int c, Vector3 want)
        {
            var nrm = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(nrm, want) < 0f) { int t = b; b = c; c = t; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }
        float cos = Mathf.Cos(smoothDeg * Mathf.Deg2Rad), u = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 na = Vector3.Dot(en[(i + n - 1) % n], en[i]) > cos ? (en[(i + n - 1) % n] + en[i]).normalized : en[i];
            Vector3 nb = Vector3.Dot(en[i], en[j]) > cos ? (en[i] + en[j]).normalized : en[i];
            float len = (zy[j] - zy[i]).magnitude;
            int k = verts.Count;
            verts.Add(new Vector3(x0, zy[i].y, zy[i].x)); norms.Add(na); uvs.Add(new Vector2(u, x0));
            verts.Add(new Vector3(x1, zy[i].y, zy[i].x)); norms.Add(na); uvs.Add(new Vector2(u, x1));
            verts.Add(new Vector3(x0, zy[j].y, zy[j].x)); norms.Add(nb); uvs.Add(new Vector2(u + len, x0));
            verts.Add(new Vector3(x1, zy[j].y, zy[j].x)); norms.Add(nb); uvs.Add(new Vector2(u + len, x1));
            Tri(k, k + 1, k + 2, en[i]); Tri(k + 1, k + 3, k + 2, en[i]);
            u += len;
        }
        if (caps)
        {
            var ear = EarClip(zy);
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? x0 : x1; var want = s == 0 ? Vector3.left : Vector3.right;
                int k = verts.Count;
                for (int i = 0; i < n; i++) { verts.Add(new Vector3(x, zy[i].y, zy[i].x)); norms.Add(want); uvs.Add(new Vector2(zy[i].x, zy[i].y)); }
                for (int t = 0; t + 2 < ear.Count; t += 3) Tri(k + ear[t], k + ear[t + 1], k + ear[t + 2], want);
            }
        }
        var m = new Mesh { name = name };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    // Ear clipping for a counter-clockwise simple polygon.
    static List<int> EarClip(Vector2[] p)
    {
        var idx = new List<int>(); for (int i = 0; i < p.Length; i++) idx.Add(i);
        var tris = new List<int>();
        while (idx.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count && !clipped; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                Vector2 a = p[ia], b = p[ib], c = p[ic];
                if (Cross2(b - a, c - b) <= 1e-7f) continue;
                bool inside = false;
                foreach (int j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    var q = p[j];
                    if (Cross2(b - a, q - a) >= 0f && Cross2(c - b, q - b) >= 0f && Cross2(a - c, q - c) >= 0f) { inside = true; break; }
                }
                if (inside) continue;
                tris.Add(ia); tris.Add(ib); tris.Add(ic); idx.RemoveAt(i); clipped = true;
            }
            if (!clipped) break;
        }
        if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
        return tris;
    }
}
