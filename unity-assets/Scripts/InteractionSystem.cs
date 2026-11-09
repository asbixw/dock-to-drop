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
    public bool HasPrompt => current != null;
    public Vector3 PromptPoint => current != null ? current.transform.position : Vector3.zero;

    void Update()
    {
        if (cam == null || flow == null) return;
        if (flow.BlocksInteraction)
        {
            SetCurrent(null);
            return;
        }

        // Cast from the driver's eyes/head, not the camera body. The camera still
        // supplies the screen-centre direction, but a third-person camera sitting
        // metres back no longer reaches objects the driver could not touch.
        var pc = flow.Player;
        Vector3 origin = pc != null ? pc.InteractOrigin : cam.transform.position;
        Vector3 dir = cam.transform.forward;
        float reach = distance;

        int count = Physics.RaycastNonAlloc(origin, dir, hits, reach, mask, QueryTriggerInteraction.Collide);
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
            if (it != null && it.CanUse && Vector3.Distance(origin, nearest.point) <= reach)
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
