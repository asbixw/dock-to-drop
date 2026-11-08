using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class UIFactory
{
    public static Font Font()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    public static Canvas Canvas(string name, int order, bool raycaster = true, bool landscape = false)
    {
        var go = new GameObject(name);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (landscape)
        {
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
        }
        if (raycaster) go.AddComponent<GraphicRaycaster>();
        return c;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static Image Panel(Transform parent, string name, Vector2 size, Vector2 pos, Color color, bool rounded = false, float radius = 24f)
    {
        var rt = Rect(name, parent);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        if (rounded)
        {
            img.sprite = RoundedSprite(Color.white, 128, (int)radius);
            bool slice = radius < 60f;
            img.type = slice ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    public static Image Full(Transform parent, string name, Color color)
    {
        var rt = Rect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button Button(Transform parent, string name, string label, Vector2 size, Vector2 pos, Color bg, Color fg, int fontSize = 30, System.Action onClick = null)
    {
        var rt = Rect(name, parent);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg;
        img.sprite = RoundedSprite(Color.white, 128, 22);
        img.type = Image.Type.Sliced;
        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.82f);
        colors.pressedColor = new Color(0.9f, 0.8f, 0.7f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        var t = Label(rt, "Text", label, fontSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero;
        t.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }

    public static Image Img(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var rt = Rect(name, parent);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var i = rt.gameObject.AddComponent<Image>();
        i.color = color;
        return i;
    }

    public static Sprite RoundedSprite(Color color, int size, int radius)
    {
        int safeRadius = Mathf.Clamp(radius, 2, size / 2 - 2);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var cols = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Max(safeRadius - x, 0), x - (size - safeRadius));
            float dy = Mathf.Max(Mathf.Max(safeRadius - y, 0), y - (size - safeRadius));
            bool inside = dx * dx + dy * dy <= safeRadius * safeRadius;
            cols[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }
        tex.SetPixels32(cols); tex.Apply();
        var border = new Vector4(safeRadius, safeRadius, safeRadius, safeRadius);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    public static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void TopRight(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-pos.x, pos.y);
        rt.sizeDelta = size;
    }

    public static void BottomCenter(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
