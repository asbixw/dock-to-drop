using UnityEngine;

// The tractor's red (emergency) and blue (service) air lines and the green
// seven-way light cord. Stowed, they hang in loops off the back of the cab.
// Connected, they sag across to the trailer's glad hands and are rebuilt every
// frame so they stretch and slacken as the trailer swings.
public class AirLines : MonoBehaviour
{
    static readonly Color[] Colors = { new Color(0.55f, 0.05f, 0.04f), new Color(0.05f, 0.15f, 0.45f), new Color(0.07f, 0.26f, 0.09f) };
    static readonly float[] Radius = { 0.019f, 0.019f, 0.014f };
    // where the lines leave the back of the cab, in truck space
    static readonly Vector3[] Anchors = { new Vector3(-0.36f, 1.95f, -2.30f), new Vector3(-0.20f, 1.95f, -2.30f), new Vector3(-0.02f, 1.98f, -2.30f) };
    const int Seg = 18, Sides = 8;

    Transform truck;
    TrailerRig target;
    Mesh[] meshes;
    Transform[] ends;
    Vector3[] verts;
    Vector3[] norms;

    public bool Connected => target != null;

    public void Build(Transform truckRoot)
    {
        truck = truckRoot;
        meshes = new Mesh[3];
        ends = new Transform[3];
        verts = new Vector3[(Seg + 1) * Sides];
        norms = new Vector3[verts.Length];
        var tris = new int[Seg * Sides * 6];
        int k = 0;
        for (int s = 0; s < Seg; s++)
            for (int j = 0; j < Sides; j++)
            {
                int a = s * Sides + j, b = s * Sides + (j + 1) % Sides;
                int c = a + Sides, d = b + Sides;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }
        var steel = MeshFactory.Mat("GladHandSteel", new Color(0.42f, 0.43f, 0.44f), 0.8f, 0.45f);
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(i == 0 ? "AirLineRed" : i == 1 ? "AirLineBlue" : "LightCord");
            go.transform.SetParent(transform, false);
            var m = new Mesh { name = go.name };
            m.MarkDynamic();
            m.vertices = verts;
            m.triangles = tris;
            meshes[i] = m;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MeshFactory.Mat(go.name + "Rubber", Colors[i], 0f, 0.38f);
            // coupler on the free end: a glad hand for the air lines, a plug for the cord
            var end = MeshFactory.Box(go.name + "End", i < 2 ? new Vector3(0.07f, 0.045f, 0.12f) : new Vector3(0.06f, 0.06f, 0.09f),
                Vector3.zero, i < 2 ? steel : mr.sharedMaterial, transform, false);
            ends[i] = end.transform;
        }
        Stow();
    }

    public void Connect(TrailerRig rig)
    {
        target = rig;
        Rebuild();
    }

    void Stow()
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 a = truck.TransformPoint(Anchors[i]);
            Vector3 hang = truck.TransformPoint(Anchors[i] + new Vector3(0.05f, -0.48f, -0.07f));
            Vector3 ctrl = truck.TransformPoint(Anchors[i] + new Vector3(0.03f, -0.85f, -0.22f));
            Tube(i, a, ctrl, hang);
        }
    }

    void LateUpdate()
    {
        if (target != null) Rebuild();
    }

    void Rebuild()
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 a = truck.TransformPoint(Anchors[i]);
            Vector3 b = target.GladHandWorld(i);
            float dist = Vector3.Distance(a, b);
            Vector3 ctrl = (a + b) * 0.5f + Vector3.down * (0.18f + 0.12f * dist);
            Tube(i, a, ctrl, b);
        }
    }

    // Quadratic Bezier tube from a through the pull of ctrl to b, in world space.
    void Tube(int i, Vector3 a, Vector3 ctrl, Vector3 b)
    {
        Vector3 prevN = Vector3.zero;
        for (int s = 0; s <= Seg; s++)
        {
            float t = s / (float)Seg, u = 1f - t;
            Vector3 p = u * u * a + 2f * u * t * ctrl + t * t * b;
            Vector3 tan = (2f * u * (ctrl - a) + 2f * t * (b - ctrl)).normalized;
            Vector3 n = Vector3.Cross(tan, Vector3.up);
            if (n.sqrMagnitude < 1e-4f) n = prevN.sqrMagnitude > 0f ? prevN : Vector3.right;
            n.Normalize();
            prevN = n;
            Vector3 bn = Vector3.Cross(n, tan);
            for (int j = 0; j < Sides; j++)
            {
                float ang = j * Mathf.PI * 2f / Sides;
                Vector3 dir = n * Mathf.Cos(ang) + bn * Mathf.Sin(ang);
                verts[s * Sides + j] = transform.InverseTransformPoint(p + dir * Radius[i]);
                norms[s * Sides + j] = transform.InverseTransformDirection(dir);
            }
        }
        var m = meshes[i];
        m.vertices = verts;
        m.normals = norms;
        m.RecalculateBounds();
        Vector3 endTan = (b - ctrl).normalized;
        ends[i].position = b;
        if (endTan.sqrMagnitude > 0f) ends[i].rotation = Quaternion.LookRotation(endTan, Vector3.up);
    }
}
