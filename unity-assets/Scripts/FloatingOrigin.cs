using UnityEngine;
using System;
using System.Collections.Generic;

// Recentering the world keeps the truck's coordinates small enough for stable
// physics over a 32/60-mile (51-96 km) deadhead. When the truck strays past
// the threshold, the whole world root (truck, trailer, destination, road
// chunks) is shifted back by the truck's position. The player is shifted too,
// but only while on foot: seated, the player is a child of the truck seat and
// already moves with the root. GameFlow consumes PendingShift to keep the
// odometer in the original frame, so distance is never lost across a shift.
// Anything that caches a world position between frames subscribes to OnShift.
public class FloatingOrigin : MonoBehaviour
{
    public Transform root;      // World root: truck, trailer, destination, road
    public Transform player;    // the driver; outside the root unless seated
    public Transform truck;     // set by GameFlow; looked up once if left empty
    public float threshold = 2000f;
    public int ShiftCount { get; private set; }

    public static Vector3 PendingShift { get; private set; }
    public static int TotalShifts { get; private set; }
    public static event Action<Vector3> OnShift;

    public static Vector3 ConsumeShift()
    {
        Vector3 s = PendingShift;
        PendingShift = Vector3.zero;
        return s;
    }

    void Update()
    {
        if (root == null) return;
        if (truck == null) truck = root.Find("Freightliner_Cascadia");
        CullChunks();
        Vector3 world = truck != null ? truck.position : root.position;
        if (world.sqrMagnitude < threshold * threshold) return;

        Vector3 shift = world;
        root.position -= shift;
        if (player != null && !player.IsChildOf(root)) player.position -= shift;
        // Ground and contact raycasts later this frame must see the moved colliders.
        Physics.SyncTransforms();
        PendingShift += shift;
        ShiftCount++;
        TotalShifts = ShiftCount;
        Debug.Log("D2D ORIGIN shift=" + shift.ToString("F1") + " count=" + ShiftCount);
        if (OnShift != null) OnShift(shift);
    }

    // Keep physics colliders only near the truck; the route is 50-96 km long.
    // Every chunk object sits at the route origin and its mesh holds the real
    // coordinates, so distance is measured to the mesh centre, not the transform.
    public Transform chunkRoot { get; set; }
    struct Chunk { public Collider col; public Transform tr; public Vector3 centre; }
    List<Chunk> chunks;
    float nextCull;
    const float CullRadius = 3000f;

    void CullChunks()
    {
        if (chunkRoot == null || truck == null) return;
        if (chunks == null)
        {
            chunks = new List<Chunk>();
            foreach (Transform c in chunkRoot)
            {
                if (!c.name.StartsWith("RouteAsphalt")) continue;
                var col = c.GetComponent<Collider>();
                var mf = c.GetComponent<MeshFilter>();
                if (col == null || mf == null || mf.sharedMesh == null) continue;
                chunks.Add(new Chunk { col = col, tr = c, centre = mf.sharedMesh.bounds.center });
            }
        }
        if (Time.time < nextCull) return;
        nextCull = Time.time + 0.5f;
        Vector3 at = truck.position;
        float r2 = CullRadius * CullRadius;
        for (int i = 0; i < chunks.Count; i++)
        {
            var ch = chunks[i];
            if (ch.col == null) continue;
            bool near = (ch.tr.TransformPoint(ch.centre) - at).sqrMagnitude < r2;
            if (ch.col.enabled != near) ch.col.enabled = near;
        }
    }
}
