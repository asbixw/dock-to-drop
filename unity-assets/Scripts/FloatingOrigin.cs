using UnityEngine;
using System;
using System.Collections.Generic;

// Recentering the world keeps the truck's coordinates small enough for stable
// physics over a 32/60-mile (51-96 km) deadhead. When the truck strays past
// the threshold, the whole world root (truck, trailer, destination, road
// chunks) is shifted back by the truck's position; the player shifts with it
// so the rig stays together. GameFlow consumes PendingShift to keep the
// odometer in the original frame, so distance is never lost across a shift.
public class FloatingOrigin : MonoBehaviour
{
    public Transform root;      // World root: truck, trailer, destination, road
    public Transform player;    // the driver, which lives outside the root
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
        // Use the truck if it is a child of the root, otherwise the root itself.
        Transform truck = root.Find("Freightliner_Cascadia");
        CullChunks(truck);
        Vector3 world = truck != null ? truck.position : root.position;
        if (world.sqrMagnitude < threshold * threshold) return;

        Vector3 shift = world;
        root.position -= shift;
        if (player != null) player.position -= shift;
        PendingShift += shift;
        ShiftCount++;
        TotalShifts = ShiftCount;
        Debug.Log("D2D ORIGIN shift=" + shift.ToString("F1") + " count=" + ShiftCount);
        if (OnShift != null) OnShift(shift);
    }

    // Keep physics colliders only near the truck; the route is 50-96 km long.
    public Transform chunkRoot { get; set; }
    void CullChunks(Transform truck)
    {
        if (chunkRoot == null || truck == null) return;
        float r2 = 3000f * 3000f;
        foreach (Transform c in chunkRoot)
        {
            if (!c.name.StartsWith("RouteAsphalt")) continue;
            var col = c.GetComponent<Collider>();
            if (col == null) continue;
            bool near = (c.position - truck.position).sqrMagnitude < r2;
            if (col.enabled != near) col.enabled = near;
        }
    }
}
