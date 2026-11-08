using UnityEditor;
using UnityEngine;

public static class InspectHeights
{
    [MenuItem("Dock to Drop/Inspect Heights")]
    public static void Inspect()
    {
        foreach (var path in new[] { "Assets/Resources/Models/Freightliner_Cascadia.fbx", "Assets/Resources/Models/Amazon_53ft_Container.fbx" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var inst = Object.Instantiate(go);
            Debug.Log("HEIGHTS " + path);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (n.StartsWith("Wheel") || n.StartsWith("Landing") || n.StartsWith("Lug") || n.StartsWith("Axle") || n.StartsWith("Kingpin") || n.StartsWith("SideWall") || n.StartsWith("Chassis"))
                {
                    Bounds b = r.bounds;
                    Vector3 lmin = inst.transform.InverseTransformPoint(b.min);
                    Vector3 lmax = inst.transform.InverseTransformPoint(b.max);
                    Debug.Log("HEIGHT " + n + " worldMinY=" + b.min.y.ToString("F4") + " worldMaxY=" + b.max.y.ToString("F4") + " localMin=" + lmin.ToString("F3") + " localMax=" + lmax.ToString("F3"));
                }
            }
            Object.DestroyImmediate(inst);
        }
    }
}
