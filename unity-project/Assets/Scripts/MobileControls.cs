using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MobileControls : MonoBehaviour
{
    public GameFlow flow;
    public Canvas canvas;
    TouchJoystick stick;
    Button sprint;
    bool sprintOn;

    public void Build()
    {
        UIFactory.EnsureEventSystem();
        canvas = UIFactory.Canvas("MobileCanvas", 35, true);
        canvas.transform.SetParent(transform, false);

        var look = UIFactory.Rect("LookArea", canvas.transform);
        look.anchorMin = new Vector2(0.38f, 0f);
        look.anchorMax = new Vector2(1f, 1f);
        look.offsetMin = Vector2.zero;
        look.offsetMax = Vector2.zero;
        var limg = look.gameObject.AddComponent<Image>();
        limg.color = new Color(0, 0, 0, 0.001f);
        look.gameObject.AddComponent<TouchLookArea>().flow = flow;

        var joyBg = UIFactory.Panel(canvas.transform, "Joystick", new Vector2(230, 230), new Vector2(0, 0), new Color(1f, 1f, 1f, 0.08f), true, 115);
        joyBg.rectTransform.anchorMin = joyBg.rectTransform.anchorMax = new Vector2(0, 0);
        joyBg.rectTransform.anchoredPosition = new Vector2(150, 170);
        var knob = UIFactory.Panel(joyBg.rectTransform, "Knob", new Vector2(86, 86), Vector2.zero, new Color(1f, 0.55f, 0.16f, 0.85f), true, 43);
        stick = joyBg.gameObject.AddComponent<TouchJoystick>();
        stick.knob = knob.rectTransform;
        stick.radius = 80f;
        stick.flow = flow;

        var inter = UIFactory.Button(canvas.transform, "Interact", "INTERACT", new Vector2(240, 86), new Vector2(0, 0), new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 22, () => flow.Interact());
        inter.GetComponent<RectTransform>().anchorMin = inter.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
        inter.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, 270);
        var phone = UIFactory.Button(canvas.transform, "Phone", "PHONE", new Vector2(180, 70), new Vector2(0, 0), new Color(0.10f, 0.16f, 0.20f, 0.9f), Color.white, 20, () => flow.TogglePhone());
        phone.GetComponent<RectTransform>().anchorMin = phone.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
        phone.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, 190);
        sprint = UIFactory.Button(canvas.transform, "Sprint", "SPRINT", new Vector2(150, 62), new Vector2(0, 0), new Color(0.10f, 0.16f, 0.20f, 0.75f), Color.white, 18, () => { sprintOn = !sprintOn; });
        sprint.GetComponent<RectTransform>().anchorMin = sprint.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
        sprint.GetComponent<RectTransform>().anchoredPosition = new Vector2(-280, 120);
        var exit = UIFactory.Button(canvas.transform, "Exit", "EXIT", new Vector2(150, 58), new Vector2(0, 0), new Color(0.10f, 0.16f, 0.20f, 0.75f), Color.white, 18, () => flow.ExitTruck());
        exit.GetComponent<RectTransform>().anchorMin = exit.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
        exit.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80, 120);
    }

    void Update()
    {
        if (flow == null) return;
        if (flow.Player != null) flow.Player.touchRun = sprintOn;
        if (flow.Truck != null && flow.Player != null && flow.Player.IsSeated)
        {
            flow.Truck.touchSteer = stick != null ? stick.value.x : 0f;
            flow.Truck.touchThrottle = stick != null ? Mathf.Clamp01(stick.value.y) : 0f;
            flow.Truck.touchBrake = stick != null ? Mathf.Clamp01(-stick.value.y) : 0f;
        }
        else if (flow.Player != null)
        {
            flow.Player.touchMove = stick != null ? stick.value : Vector2.zero;
        }
    }
}

public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform knob;
    public float radius = 80f;
    public GameFlow flow;
    public Vector2 value;
    RectTransform rt;

    void Awake() { rt = GetComponent<RectTransform>(); }

    public void OnPointerDown(PointerEventData e) { OnDrag(e); }
    public void OnDrag(PointerEventData e)
    {
        if (rt == null) return;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out local);
        value = Vector2.ClampMagnitude(local / radius, 1f);
        if (knob != null) knob.anchoredPosition = value * radius;
    }
    public void OnPointerUp(PointerEventData e)
    {
        value = Vector2.zero;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
    }
}

public class TouchLookArea : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    public GameFlow flow;
    public void OnPointerDown(PointerEventData e) { }
    public void OnDrag(PointerEventData e)
    {
        if (flow != null && flow.Player != null) flow.Player.AddLook(e.delta);
    }
}
