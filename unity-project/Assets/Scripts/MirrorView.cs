using UnityEngine;

// One live mirror per side. The mirror quads used to be plain reflective
// material, so they showed the reflection probe and never the trailer. This
// renders a small rear-facing camera to a texture on the glass every few
// frames. The camera sits just ahead of the glass, looks back along the
// truck, and is offset slightly outward so each mirror shows its own side.
public class MirrorView : MonoBehaviour
{
    public Transform truck;
    public float side = -1f;        // -1 driver side, +1 passenger side
    public int width = 192;
    public int height = 384;

    Camera cam;
    RenderTexture rt;

    void Start()
    {
        if (truck == null) return;
        // The built-in shader can be stripped from a player build. Try the
        // mirror shader first, then Standard, and skip the material entirely
        // rather than passing a null shader into Material.
        var shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("D2D MIRROR no Unlit/Texture or Standard shader on " + name);
            return;
        }

        rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        rt.name = name + "_RT";
        rt.Create();

        var go = new GameObject(name + "_Cam");
        go.transform.SetParent(truck, false);
        cam = go.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 320f;
        cam.fieldOfView = 55f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.enabled = false;
        cam.cullingMask &= ~(1 << 8);   // DriverBody

        var mat = new Material(shader);
        mat.name = name + "_Mat";
        mat.mainTexture = rt;
        // A mirror flips left and right; the rear-facing camera alone would read
        // like a rear window instead of a mirror.
        mat.mainTextureScale = new Vector2(-1f, 1f);
        mat.mainTextureOffset = new Vector2(1f, 0f);
        var mr = GetComponent<MeshRenderer>();
        if (mr == null) Debug.LogWarning("D2D MIRROR no MeshRenderer on " + name);
        else mr.sharedMaterial = mat;
    }

    void LateUpdate()
    {
        if (cam == null || truck == null) return;
        Vector3 p = transform.position;
        Vector3 back = -truck.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 1e-4f) back = Vector3.back;
        back.Normalize();
        Vector3 dir = back + truck.right * (side * 0.14f);
        dir.y = -0.06f;
        dir.Normalize();
        // Sit just ahead of the glass so the housing and the glass itself are
        // behind the camera and never show in the mirror.
        cam.transform.position = p - dir * 0.10f;
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        if (Time.frameCount % 3 == 0) cam.Render();
    }

    void OnDestroy()
    {
        if (rt != null) rt.Release();
    }
}
