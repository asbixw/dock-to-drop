using UnityEditor;
using UnityEngine;
public static class InspectMaterials
{
    [MenuItem("Dock to Drop/Inspect Materials")]
    public static void Inspect()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/Freightliner_Cascadia.fbx");
        var inst = Object.Instantiate(go);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && (m.name.ToLower().Contains("glass") || m.name.ToLower().Contains("window") || m.name.ToLower().Contains("wind")))
                    Debug.Log("MAT " + r.name + " -> " + m.name + " shader=" + m.shader.name + " color=" + (m.HasProperty("_Color") ? m.color.ToString() : "?") + " alpha=" + (m.HasProperty("_Color") ? m.color.a.ToString() : "?"));
            }
        }
        Object.DestroyImmediate(inst);
    }
}
