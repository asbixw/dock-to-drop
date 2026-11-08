using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class Interactable : MonoBehaviour
{
    public string id;
    public string prompt = "Interact";
    public UnityEvent onInteract = new UnityEvent();
    public bool oneShot;
    public bool highlight = true;
    public Color highlightColor = new Color(1f, 0.45f, 0.05f);
    // The interaction collider often lives on an empty marker. Point this at the
    // visible object (the door, the seat, the hoses) so the highlight has
    // something to light up.
    public Transform highlightRoot;

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    bool used;

    void Awake()
    {
        if (highlight) CollectRenderers(highlightRoot != null ? highlightRoot : transform);
    }

    public bool CanUse => !used;

    // Called after AddComponent when the visible object is known. Awake runs
    // before the caller can assign a field, so the renderer list is rebuilt here.
    public void SetHighlightRoot(Transform root)
    {
        highlightRoot = root;
        if (highlight) CollectRenderers(root != null ? root : transform);
    }

    void CollectRenderers(Transform root)
    {
        renderers.Clear();
        blocks.Clear();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            // MaterialPropertyBlock can set _EmissionColor but cannot turn on the
            // shader keyword. Enable it on the shared material once; the property
            // block still keeps the glow local to the highlighted renderers.
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.HasProperty("_EmissionColor")) m.EnableKeyword("_EMISSION");
            }
            renderers.Add(r);
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            blocks.Add(b);
        }
    }

    public void SetHighlight(bool on)
    {
        if (!highlight) return;
        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var b = blocks[i];
            r.GetPropertyBlock(b);
            b.SetColor("_EmissionColor", on ? highlightColor * 0.55f : Color.black);
            r.SetPropertyBlock(b);
        }
    }

    public void Use()
    {
        if (used) return;
        if (oneShot) used = true;
        onInteract?.Invoke();
    }
}
