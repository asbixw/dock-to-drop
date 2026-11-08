using UnityEngine;

// Keeps a distant cloud card turned toward the camera around the vertical axis.
// The cards sit far enough away that the parallax reads as a real sky layer.
public class SkyBillboard : MonoBehaviour
{
    Transform cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            var c = Camera.main;
            if (c == null) return;
            cam = c.transform;
        }
        Vector3 d = transform.position - cam.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        // Unity's built-in quad faces -Z, so point +Z away from the camera.
        transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }
}
