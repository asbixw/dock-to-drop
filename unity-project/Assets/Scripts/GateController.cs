using UnityEngine;
using System.Collections;

public class GateController : MonoBehaviour
{
    public Transform arm;
    public bool open;
    Quaternion closedRot;
    Quaternion openRot;
    Coroutine routine;
    bool initialized;

    void Init()
    {
        if (initialized || arm == null) return;
        closedRot = arm.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, 0f, -82f);
        initialized = true;
    }

    public void SetOpen(bool value)
    {
        if (arm == null) return;
        Init();
        if (open == value) return;
        open = value;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(value));
    }

    IEnumerator Animate(bool value)
    {
        Quaternion from = arm.localRotation;
        Quaternion to = value ? openRot : closedRot;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.8f;
            arm.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        arm.localRotation = to;
        routine = null;
    }
}

public class GateTrigger : MonoBehaviour
{
    public GameFlow flow;
    public bool fired;
    void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        var truck = other.GetComponentInParent<TruckController>();
        if (truck != null)
        {
            fired = true;
            flow.OnGateApproach(truck);
        }
    }
}
