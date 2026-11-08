using UnityEditor;
using UnityEngine;

public static class InspectModels
{
    [MenuItem("Dock to Drop/Inspect Models")]
    public static void Inspect()
    {
        foreach (var path in new[] { "Assets/Resources/Models/Freightliner_Cascadia.fbx", "Assets/Resources/Models/Amazon_53ft_Container.fbx" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Debug.Log("INSPECT missing " + path); continue; }
            var inst = Object.Instantiate(go);
            var rs = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Debug.Log("INSPECT " + path + " renderers=" + rs.Length + " bounds=" + b.min.ToString("F3") + " .. " + b.max.ToString("F3") + " size=" + b.size.ToString("F3"));
            for (int i = 0; i < Mathf.Min(inst.transform.childCount, 12); i++)
            {
                var c = inst.transform.GetChild(i);
                Debug.Log("INSPECT child " + c.name + " localPos=" + c.localPosition.ToString("F3") + " localScale=" + c.localScale.ToString("F3"));
            }
            foreach (var target in new[] { "Hood", "DriverDoor", "SeatBase", "Windshield", "Door_L", "SealTag" })
            {
                var t = Find(inst.transform, target);
                Debug.Log("INSPECT obj " + target + " = " + (t == null ? "null" : t.localPosition.ToString("F3") + " lossyScale=" + t.lossyScale.ToString("F3")));
            }
            Object.DestroyImmediate(inst);
        }
        var asset = AssetImporter.GetAtPath("Assets/Resources/Models/Freightliner_Cascadia.fbx") as ModelImporter;
        if (asset != null) Debug.Log("INSPECT importer scale=" + asset.globalScale + " useFileScale=" + asset.useFileScale + " fileScale=" + asset.fileScale);
    }

    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
        return null;
    }
}
