using UnityEngine;

public class InteractionSystem : MonoBehaviour
{
    public Camera cam;
    public float distance = 3.4f;
    public LayerMask mask = ~0;
    public GameFlow flow;
    Interactable current;
    readonly RaycastHit[] hits = new RaycastHit[16];

    public Interactable Current => current;

    void Update()
    {
        if (cam == null || flow == null) return;
        if (flow.BlocksInteraction)
        {
            SetCurrent(null);
            return;
        }

        // Aim from the actual camera so the center of the screen is the cursor.
        // Reach is still judged from the driver, not the camera, so a third-person
        // camera sitting 4 m back does not give the player 4 m of extra arms.
        var pc = flow.Player;
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        Vector3 driver = pc != null ? pc.InteractOrigin : origin;
        float reach = distance;
        float max = reach;
        if (pc != null && pc.ThirdPersonActive)
            max += Vector3.Distance(origin, driver);

        int count = Physics.RaycastNonAlloc(origin, dir, hits, max, mask, QueryTriggerInteraction.Collide);
        bool found = false;
        RaycastHit nearest = default;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            // The third-person camera starts behind the player; ignore the
            // player's own capsule instead of letting it block every ray.
            if (pc != null && h.collider.GetComponentInParent<PlayerController>() != null) continue;
            // Trigger volumes that are not interactables (gate triggers, ambient
            // zones) should not block the view of a real object behind them.
            if (h.collider.isTrigger && h.collider.GetComponentInParent<Interactable>() == null) continue;
            if (h.distance < nearestDist)
            {
                nearestDist = h.distance;
                nearest = h;
                found = true;
            }
        }

        if (found)
        {
            var it = nearest.collider.GetComponentInParent<Interactable>();
            if (it != null && it.CanUse && Vector3.Distance(driver, nearest.point) <= reach)
            {
                SetCurrent(it);
                return;
            }
        }
        SetCurrent(null);
    }

    void SetCurrent(Interactable it)
    {
        if (current == it) return;
        if (current != null) current.SetHighlight(false);
        current = it;
        if (current != null) current.SetHighlight(true);
        flow.SetPrompt(current != null ? current.prompt : null);
    }

    public void TryInteract()
    {
        if (current == null || !current.CanUse) return;
        var it = current;
        SetCurrent(null);
        it.Use();
        var body = GetComponent<DriverBody>();
        if (body != null) body.PlayInteract();
        flow.Audio.PlaySfx("seat", 0.3f);
    }
}
