using UnityEngine;

public class InspectionPoint : MonoBehaviour
{
    public string id;
    public string title;
    [TextArea] public string details;
    public bool inspected;
    public Renderer marker;
    public Transform markerRoot;
    public float markerHeight = 2.8f;
    float t;
    Vector3 markerBase;

    public void Setup(string id, string title, string details)
    {
        this.id = id; this.title = title; this.details = details;
    }

    public void SetMarker(Transform root)
    {
        markerRoot = root;
        markerBase = root.localPosition;
    }

    void Update()
    {
        if (markerRoot == null) return;
        t += Time.deltaTime;
        markerRoot.localPosition = markerBase + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.045f);
        var cam = Camera.main;
        if (cam != null)
            markerRoot.rotation = Quaternion.LookRotation(markerRoot.position - cam.transform.position, Vector3.up);
    }

    public void Mark()
    {
        inspected = true;
        if (markerRoot != null) markerRoot.gameObject.SetActive(false);
        else if (marker != null) marker.gameObject.SetActive(false);
    }
}
