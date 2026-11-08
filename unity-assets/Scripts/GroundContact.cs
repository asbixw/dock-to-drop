using UnityEngine;

// One place to ask "what is the pavement height here?" so the player, the
// tractor and the trailer all stand on the same surface. The visible slabs are
// colliders now, so this simply returns the highest solid surface under a point
// while ignoring the vehicle that is asking.
public static class GroundContact
{
    static readonly RaycastHit[] hits = new RaycastHit[32];

    public static float HeightAt(Vector3 worldPos, Transform ignoreA = null, Transform ignoreB = null, float fallback = 0f)
    {
        Vector3 origin = new Vector3(worldPos.x, worldPos.y + 40f, worldPos.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 90f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            var c = h.collider;
            if (c == null) continue;
            if (ignoreA != null && c.transform.IsChildOf(ignoreA)) continue;
            if (ignoreB != null && c.transform.IsChildOf(ignoreB)) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            // A trailer body or bridge overhead is not the pavement under our feet.
            if (h.point.y > worldPos.y + 0.6f) continue;
            if (h.point.y > best) { best = h.point.y; found = true; }
        }
        return found ? best : fallback;
    }
}
