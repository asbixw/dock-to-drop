using UnityEngine;

// A small built-in-pipeline grade: filmic contrast, cool shadows, warm highlights,
// and a soft vignette. Kept deliberately subtle so the dawn lighting still reads.
public class CinematicGrade : MonoBehaviour
{
    Material mat;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null)
        {
            var sh = Shader.Find("Hidden/DockToDrop/Grade");
            if (sh == null)
            {
                Graphics.Blit(src, dst);
                return;
            }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        Graphics.Blit(src, dst, mat);
    }

    void OnDestroy()
    {
        // HideAndDontSave materials are never collected on their own.
        if (mat != null) Destroy(mat);
    }
}
