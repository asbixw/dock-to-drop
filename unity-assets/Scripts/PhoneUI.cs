using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class PhoneUI : MonoBehaviour
{
    public GameFlow flow;
    public AudioManager audio;
    public PhotoTarget sealTarget;

    Canvas canvas;
    GameObject root;
    Image dimImage;
    GameObject phoneBezel;
    RectTransform phone;
    RectTransform body;
    GameObject loadView, chatView, photosView, cameraView, previewView, callView;
    Text callName, callText, callStatus, cameraHint, previewStatus;
    RawImage previewImage, cameraPreview;
    RectTransform photosGrid;
    Button sendButton;
    bool previewValid;
    Button callAnswer, callDecline, callNext;
    Texture2D lastPhoto;
    bool callAnswered;
    bool cameraActive;
    bool previewOpen;
    bool fovSaved;
    float gameplayFov = 68f;
    Action onAnswer, onDecline, onNext;
    readonly List<Texture2D> photos = new List<Texture2D>();
    Text chatBody;
    Text photosHint;
    RectTransform chatContent;
    Button[] tabButtons;
    static readonly Vector2 CameraRectMin = new Vector2(0.331f, 0.14f);
    static readonly Vector2 CameraRectMax = new Vector2(0.669f, 0.92f);

    public bool IsOpen => root != null && root.activeSelf;
    public bool PreviewReady => previewOpen && previewValid;
    public bool CameraActive => cameraActive;
    public bool PreviewOpen => previewOpen;
    public Texture2D LastPhoto => lastPhoto;

    public void Build(GameFlow gameFlow)
    {
        flow = gameFlow;
        audio = flow.Audio;
        UIFactory.EnsureEventSystem();
        canvas = UIFactory.Canvas("PhoneCanvas", 40, true);
        canvas.transform.SetParent(transform, false);
        root = canvas.gameObject;
        root.SetActive(false);

        dimImage = UIFactory.Full(canvas.transform, "Dim", new Color(0.01f, 0.02f, 0.03f, 0.86f));
        dimImage.raycastTarget = true;
        var bezel = UIFactory.Panel(canvas.transform, "PhoneBezel", new Vector2(920, 1620), Vector2.zero, new Color(0.008f, 0.012f, 0.016f), true, 58f);
        bezel.rectTransform.anchorMin = bezel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bezel.rectTransform.anchoredPosition = new Vector2(0, -6);
        phoneBezel = bezel.gameObject;
        phone = UIFactory.Panel(canvas.transform, "Phone", new Vector2(880, 1580), Vector2.zero, new Color(0.045f, 0.058f, 0.075f), true, 50f).rectTransform;
        phone.anchorMin = phone.anchorMax = new Vector2(0.5f, 0.5f);
        phone.anchoredPosition = new Vector2(0, -6);

        var header = UIFactory.Panel(phone, "Header", new Vector2(880, 130), new Vector2(0, 725), new Color(0.075f, 0.11f, 0.145f), false);
        header.rectTransform.anchorMin = header.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var accent = UIFactory.Img(header.rectTransform, "Accent", new Vector2(880, 5), Vector2.zero, new Color(1f, 0.48f, 0.10f, 0.95f));
        accent.rectTransform.anchorMin = new Vector2(0.5f, 1);
        accent.rectTransform.anchorMax = new Vector2(0.5f, 1);
        accent.rectTransform.pivot = new Vector2(0.5f, 1);
        accent.rectTransform.anchoredPosition = Vector2.zero;
        var logo = UIFactory.Label(header.rectTransform, "Logo", "RELAY", 46, new Color(1f, 0.52f, 0.12f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(logo.rectTransform, new Vector2(34, -28), new Vector2(380, 70));
        var status = UIFactory.Label(header.rectTransform, "Status", "DISPATCH  ·  MIKE", 22, new Color(0.62f, 0.74f, 0.82f), TextAnchor.MiddleRight);
        UIFactory.TopRight(status.rectTransform, new Vector2(102, -40), new Vector2(360, 52));
        var close = UIFactory.Button(phone, "Close", "×", new Vector2(66, 66), Vector2.zero, new Color(1f, 1f, 1f, 0.10f), Color.white, 42, Close);
        close.GetComponent<RectTransform>().anchorMin = close.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
        close.GetComponent<RectTransform>().pivot = new Vector2(1, 1);
        close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30, -30);

        body = UIFactory.Rect("Body", phone);
        body.anchorMin = new Vector2(0, 0);
        body.anchorMax = new Vector2(1, 1);
        body.offsetMin = new Vector2(28, 150);
        body.offsetMax = new Vector2(-28, -140);

        BuildLoad();
        BuildChat();
        BuildPhotos();
        BuildCamera();
        BuildPreview();
        BuildCall();
        BuildTabs();

        ShowView(loadView);
    }

    void BuildTabs()
    {
        var bar = UIFactory.Panel(phone, "Tabs", new Vector2(880, 120), new Vector2(0, -730), new Color(0.03f, 0.045f, 0.06f, 1f), false);
        bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        string[] names = { "LOAD", "CHAT", "PHOTOS" };
        tabButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = UIFactory.Button(bar.rectTransform, "Tab" + i, names[i], new Vector2(260, 82), new Vector2(-280 + i * 280, 0), new Color(0.03f, 0.045f, 0.06f), i == 0 ? new Color(1f, 0.52f, 0.12f) : new Color(0.58f, 0.66f, 0.72f), 30, () => ShowTab(idx));
            b.GetComponent<RectTransform>().anchorMin = b.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
            tabButtons[i] = b;
        }
    }

    void BuildLoad()
    {
        loadView = UIFactory.Rect("LoadView", body).gameObject;
        UIFactory.Stretch(loadView.GetComponent<RectTransform>(), 0, 0, 0, 0);
        var title = UIFactory.Label(loadView.transform, "Title", "ACTIVE LOAD  ·  #D2D-0612", 36, new Color(1f, 0.55f, 0.16f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, new Vector2(10, -6), new Vector2(780, 48));
        var pay = UIFactory.Label(loadView.transform, "Pay", "$2,450", 92, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(pay.rectTransform, new Vector2(10, -56), new Vector2(700, 108));
        var route = UIFactory.Label(loadView.transform, "Route", "DALLAS, TX   →   HOUSTON, TX", 38, new Color(0.90f, 0.93f, 0.95f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(route.rectTransform, new Vector2(10, -164), new Vector2(804, 54));
        var appt = UIFactory.Label(loadView.transform, "Appt", "PICKUP 07:30   ·   DELIVERY 14:00", 30, new Color(0.66f, 0.74f, 0.80f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(appt.rectTransform, new Vector2(10, -216), new Vector2(804, 46));
        var rule = UIFactory.Img(loadView.transform, "Rule", new Vector2(804, 2), Vector2.zero, new Color(1f, 1f, 1f, 0.10f));
        UIFactory.TopLeft(rule.rectTransform, new Vector2(10, -268), new Vector2(804, 2));

        string rows = "SHIPPER     Amazon.com Services LLC\nFACILITY    Amazon DFW9\nADDRESS     4800 Industrial Blvd\nRECEIVER    Amazon DWH1\nADDRESS     9200 Harbor Road\nEQUIPMENT   53' dry container\nWEIGHT      38,500 lb\nSEAL        482913\nCOMMODITY   Palletized retail\nDEADHEAD    " + flow.DeadheadAssignedMiles + " mi empty · travel center → DFW9\nLINEHAUL    240 mi loaded · DFW9 → DWH1";
        var table = UIFactory.Label(loadView.transform, "Table", rows, 30, new Color(0.82f, 0.87f, 0.91f), TextAnchor.UpperLeft);
        table.lineSpacing = 1.08f;
        UIFactory.TopLeft(table.rectTransform, new Vector2(12, -292), new Vector2(800, 480));

        var card = UIFactory.Panel(loadView.transform, "SealCard", new Vector2(804, 180), Vector2.zero, new Color(0.075f, 0.11f, 0.145f), true, 20);
        card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 1);
        card.rectTransform.pivot = new Vector2(0.5f, 1);
        card.rectTransform.anchoredPosition = new Vector2(0, -800);
        var cardTitle = UIFactory.Label(card.rectTransform, "T", "SEAL NUMBER", 24, new Color(1f, 0.55f, 0.16f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(cardTitle.rectTransform, new Vector2(26, -22), new Vector2(500, 34));
        var cardBody = UIFactory.Label(card.rectTransform, "B", "482913", 56, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(cardBody.rectTransform, new Vector2(26, -56), new Vector2(500, 72));
        var cardNote = UIFactory.Label(card.rectTransform, "N", "MUST MATCH GATE\nPAPERWORK", 22, new Color(0.62f, 0.72f, 0.78f), TextAnchor.UpperRight);
        UIFactory.TopRight(cardNote.rectTransform, new Vector2(26, -56), new Vector2(300, 70));

        var verify = UIFactory.Button(loadView.transform, "Verify", "VERIFY LOAD", new Vector2(804, 92), new Vector2(0, -1060), new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 38, () => { flow.VerifyLoad(); Close(); });
        verify.GetComponent<RectTransform>().anchorMin = verify.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        verify.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
        verify.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -1060);
    }

    void BuildChat()
    {
        chatView = UIFactory.Rect("ChatView", body).gameObject;
        UIFactory.Stretch(chatView.GetComponent<RectTransform>(), 0, 0, 0, 0);
        var title = UIFactory.Label(chatView.transform, "Title", "DISPATCH  ·  MIKE", 34, new Color(1f, 0.55f, 0.16f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, new Vector2(10, -6), new Vector2(700, 44));
        chatContent = UIFactory.Rect("Messages", chatView.transform);
        UIFactory.TopLeft(chatContent, new Vector2(10, -58), new Vector2(804, 1060));
        var vlg = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 20;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        AddChat("Morning, driver. Amazon load is booked. Details below.", false);
        AddChat("Deadhead " + flow.DeadheadAssignedMiles + " miles to DFW9 first. Empty, then the loaded run to Houston.", false);
        AddDispatchShot();
        AddChat("Copy. Reviewing now.", true);
        var send = UIFactory.Button(chatView.transform, "Send", "SEAL PHOTO", new Vector2(804, 88), new Vector2(0, -1120), new Color(0.13f, 0.20f, 0.26f), Color.white, 30, () => { ShowCamera(sealTarget); });
        send.GetComponent<RectTransform>().anchorMin = send.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        send.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -1120);
    }

    void BuildPhotos()
    {
        photosView = UIFactory.Rect("PhotosView", body).gameObject;
        UIFactory.Stretch(photosView.GetComponent<RectTransform>(), 0, 0, 0, 0);
        var title = UIFactory.Label(photosView.transform, "Title", "PROOF PHOTOS", 34, new Color(1f, 0.55f, 0.16f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, new Vector2(10, -6), new Vector2(700, 44));
        var hint = UIFactory.Label(photosView.transform, "Hint", "Seal and POD photos will appear here.", 26, new Color(0.58f, 0.67f, 0.73f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(hint.rectTransform, new Vector2(10, -58), new Vector2(700, 60));
        photosHint = hint;
        photosGrid = UIFactory.Rect("Grid", photosView.transform);
        UIFactory.TopLeft(photosGrid, new Vector2(10, -116), new Vector2(804, 1040));
    }

    void BuildCamera()
    {
        cameraView = UIFactory.Rect("CameraView", canvas.transform).gameObject;
        UIFactory.Stretch(cameraView.GetComponent<RectTransform>(), 0, 0, 0, 0);
        var dim = UIFactory.Full(cameraView.transform, "Dim", new Color(0f, 0f, 0f, 0.10f));
        dim.raycastTarget = false;
        var look = UIFactory.Rect("CameraLook", cameraView.transform);
        UIFactory.Stretch(look, 0, 0, 0, 0);
        var lookImg = look.gameObject.AddComponent<Image>();
        lookImg.color = new Color(0, 0, 0, 0.001f);
        var tla = look.gameObject.AddComponent<TouchLookArea>();
        tla.flow = flow;

        var topBar = UIFactory.Panel(cameraView.transform, "TopBar", new Vector2(0, 140), Vector2.zero, new Color(0.008f, 0.014f, 0.02f, 0.72f), false);
        var tbr = topBar.rectTransform;
        tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
        tbr.pivot = new Vector2(0.5f, 1); tbr.anchoredPosition = Vector2.zero; tbr.sizeDelta = new Vector2(0, 140);
        var top = UIFactory.Label(topBar.rectTransform, "Top", "PROOF CAMERA", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(top.rectTransform, new Vector2(54, -32), new Vector2(700, 64));
        var seal = UIFactory.Label(topBar.rectTransform, "Seal", "SEAL 482913", 30, new Color(1f, 0.72f, 0.36f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.TopRight(seal.rectTransform, new Vector2(54, -36), new Vector2(500, 60));
        var hint = UIFactory.Label(topBar.rectTransform, "Hint", "CENTER THE SEAL NUMBER IN THE FRAME", 30, new Color(1f, 0.88f, 0.70f), TextAnchor.MiddleCenter, FontStyle.Bold);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 1);
        hint.rectTransform.pivot = new Vector2(0.5f, 1);
        hint.rectTransform.anchoredPosition = new Vector2(0, -32);
        hint.rectTransform.sizeDelta = new Vector2(1300, 64);
        cameraHint = hint;

        var frame = UIFactory.Panel(cameraView.transform, "Frame", Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.015f), false);
        var fr = frame.rectTransform;
        fr.anchorMin = CameraRectMin; fr.anchorMax = CameraRectMax;
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        var outline = frame.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.55f);
        outline.effectDistance = new Vector2(2f, 2f);
        AddCorner(frame.rectTransform, "TL", new Vector2(0, 1), 1, -1);
        AddCorner(frame.rectTransform, "TR", new Vector2(1, 1), -1, -1);
        AddCorner(frame.rectTransform, "BL", new Vector2(0, 0), 1, 1);
        AddCorner(frame.rectTransform, "BR", new Vector2(1, 0), -1, 1);
        AddGridLine(frame.rectTransform, true, 1f / 3f);
        AddGridLine(frame.rectTransform, true, 2f / 3f);
        AddGridLine(frame.rectTransform, false, 1f / 3f);
        AddGridLine(frame.rectTransform, false, 2f / 3f);

        var ring = UIFactory.Panel(cameraView.transform, "ShutterRing", new Vector2(184, 184), Vector2.zero, new Color(1f, 1f, 1f, 0.18f), true, 92);
        ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0);
        ring.rectTransform.anchoredPosition = new Vector2(0, 160);
        var cap = UIFactory.Button(cameraView.transform, "Capture", "CAPTURE", new Vector2(150, 150), Vector2.zero, new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 26, Capture);
        var capRt = cap.GetComponent<RectTransform>();
        capRt.anchorMin = capRt.anchorMax = new Vector2(0.5f, 0);
        capRt.anchoredPosition = new Vector2(0, 160);
        var capImg = cap.GetComponent<Image>();
        capImg.sprite = UIFactory.RoundedSprite(Color.white, 128, 64);
        capImg.type = Image.Type.Simple;

        var close = UIFactory.Button(cameraView.transform, "Close", "CLOSE", new Vector2(190, 72), Vector2.zero, new Color(1f, 1f, 1f, 0.12f), Color.white, 24, CloseCamera);
        var closeRt = close.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1, 0);
        closeRt.anchoredPosition = new Vector2(-54, 52);
        cameraView.SetActive(false);
    }

    void AddCorner(RectTransform frame, string name, Vector2 anchor, int xDir, int yDir)
    {
        var h = UIFactory.Img(frame, name + "H", new Vector2(62, 5), Vector2.zero, new Color(1f, 1f, 1f, 0.9f));
        h.rectTransform.anchorMin = h.rectTransform.anchorMax = anchor;
        h.rectTransform.pivot = anchor;
        h.rectTransform.anchoredPosition = new Vector2(6 * xDir, 6 * yDir);
        var v = UIFactory.Img(frame, name + "V", new Vector2(5, 62), Vector2.zero, new Color(1f, 1f, 1f, 0.9f));
        v.rectTransform.anchorMin = v.rectTransform.anchorMax = anchor;
        v.rectTransform.pivot = anchor;
        v.rectTransform.anchoredPosition = new Vector2(6 * xDir, 6 * yDir);
    }

    void AddGridLine(RectTransform frame, bool vertical, float at)
    {
        var line = UIFactory.Img(frame, vertical ? "GridV" : "GridH", vertical ? new Vector2(2, 0) : new Vector2(0, 2), Vector2.zero, new Color(1f, 1f, 1f, 0.16f));
        var rt = line.rectTransform;
        if (vertical)
        {
            rt.anchorMin = new Vector2(at, 0); rt.anchorMax = new Vector2(at, 1);
            rt.sizeDelta = new Vector2(2, 0);
        }
        else
        {
            rt.anchorMin = new Vector2(0, at); rt.anchorMax = new Vector2(1, at);
            rt.sizeDelta = new Vector2(0, 2);
        }
        rt.anchoredPosition = Vector2.zero;
    }

    void BuildPreview()
    {
        previewView = UIFactory.Rect("PreviewView", canvas.transform).gameObject;
        UIFactory.Stretch(previewView.GetComponent<RectTransform>(), 0, 0, 0, 0);
        UIFactory.Full(previewView.transform, "Dim", new Color(0f, 0f, 0f, 0.85f));
        var panel = UIFactory.Panel(previewView.transform, "Panel", new Vector2(880, 1290), new Vector2(0, 0), new Color(0.045f, 0.06f, 0.075f), true, 32);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var title = UIFactory.Label(panel.rectTransform, "T", "PROOF PHOTO", 42, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, new Vector2(40, -28), new Vector2(600, 56));
        var img = UIFactory.Rect("Photo", panel.rectTransform);
        img.anchorMin = img.anchorMax = new Vector2(0.5f, 1);
        img.pivot = new Vector2(0.5f, 1);
        img.sizeDelta = new Vector2(640, 832);
        img.anchoredPosition = new Vector2(0, -108);
        previewImage = img.gameObject.AddComponent<RawImage>();
        previewImage.color = Color.white;
        previewStatus = UIFactory.Label(panel.rectTransform, "Status", "", 28, new Color(0.7f, 0.9f, 0.75f), TextAnchor.MiddleCenter);
        UIFactory.BottomCenter(previewStatus.rectTransform, new Vector2(0, 252), new Vector2(800, 64));
        var retake = UIFactory.Button(panel.rectTransform, "Retake", "RETAKE", new Vector2(330, 88), Vector2.zero, new Color(1f, 1f, 1f, 0.10f), Color.white, 30, () => ShowCamera(sealTarget));
        retake.GetComponent<RectTransform>().anchorMin = retake.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        retake.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
        retake.GetComponent<RectTransform>().anchoredPosition = new Vector2(-190, 110);
        sendButton = UIFactory.Button(panel.rectTransform, "Send", "SEND TO DISPATCH", new Vector2(470, 88), Vector2.zero, new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 30, SendPhoto);
        var send = sendButton;
        send.GetComponent<RectTransform>().anchorMin = send.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        send.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
        send.GetComponent<RectTransform>().anchoredPosition = new Vector2(145, 110);
        previewView.SetActive(false);
    }

    void BuildCall()
    {
        callView = UIFactory.Rect("CallView", canvas.transform).gameObject;
        UIFactory.Stretch(callView.GetComponent<RectTransform>(), 0, 0, 0, 0);
        UIFactory.Full(callView.transform, "Dim", new Color(0.01f, 0.02f, 0.03f, 0.55f));
        var skip = UIFactory.Label(callView.transform, "SkipHint", "SPACE  skip line", 24, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
        UIFactory.BottomCenter(skip.rectTransform, new Vector2(0, 120), new Vector2(600, 40));
        var avatar = UIFactory.Panel(callView.transform, "Avatar", new Vector2(220, 220), Vector2.zero, new Color(0.10f, 0.16f, 0.21f), true, 110);
        avatar.rectTransform.anchorMin = avatar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        avatar.rectTransform.anchoredPosition = new Vector2(0, 250);
        var letter = UIFactory.Label(avatar.rectTransform, "L", "M", 88, new Color(1f, 0.52f, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(letter.rectTransform, 0, 0, 0, 0);
        callStatus = UIFactory.Label(callView.transform, "Status", "INCOMING CALL  ·  RELAY DISPATCH", 30, new Color(0.3f, 0.95f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.BottomCenter(callStatus.rectTransform, new Vector2(0, 800), new Vector2(1000, 54));
        callName = UIFactory.Label(callView.transform, "Name", "MIKE  ·  DISPATCH", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.BottomCenter(callName.rectTransform, new Vector2(0, 712), new Vector2(1100, 86));
        callText = UIFactory.Label(callView.transform, "Text", "", 36, new Color(0.90f, 0.94f, 0.97f), TextAnchor.MiddleCenter);
        UIFactory.BottomCenter(callText.rectTransform, new Vector2(0, 470), new Vector2(1000, 200));
        var answer = UIFactory.Button(callView.transform, "Answer", "ANSWER", new Vector2(340, 108), Vector2.zero, new Color(0.12f, 0.75f, 0.42f), Color.white, 34, () => { callAnswered = true; onAnswer?.Invoke(); });
        answer.GetComponent<RectTransform>().anchorMin = answer.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        answer.GetComponent<RectTransform>().anchoredPosition = new Vector2(-195, 230);
        callAnswer = answer;
        var decline = UIFactory.Button(callView.transform, "Decline", "DECLINE", new Vector2(340, 108), Vector2.zero, new Color(0.75f, 0.14f, 0.14f), Color.white, 34, () => { onDecline?.Invoke(); });
        decline.GetComponent<RectTransform>().anchorMin = decline.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        decline.GetComponent<RectTransform>().anchoredPosition = new Vector2(195, 230);
        callDecline = decline;
        callNext = UIFactory.Button(callView.transform, "Next", "NEXT", new Vector2(340, 96), Vector2.zero, new Color(1f, 0.48f, 0.10f), new Color(0.12f, 0.07f, 0.02f), 32, () => onNext?.Invoke());
        callNext.GetComponent<RectTransform>().anchorMin = callNext.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
        callNext.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 230);
        callNext.gameObject.SetActive(false);
        callView.SetActive(false);
    }

    void BuildTabsButtons() { }

    void AddChat(string text, bool me)
    {
        var bubble = UIFactory.Panel(chatContent, "Bubble", new Vector2(700, 120), Vector2.zero, me ? new Color(0.30f, 0.18f, 0.09f) : new Color(0.09f, 0.14f, 0.19f), true, 18);
        var t = UIFactory.Label(bubble.rectTransform, "Text", text, 28, me ? new Color(1f, 0.90f, 0.80f) : new Color(0.85f, 0.90f, 0.94f), TextAnchor.UpperLeft);
        UIFactory.Stretch(t.rectTransform, 18, 18, 16, 16);
        var le = bubble.gameObject.AddComponent<LayoutElement>();
        int lines = Mathf.Max(1, Mathf.CeilToInt(text.Length / 52f));
        float h = 28f * 1.45f * lines + 36f;
        le.minHeight = h;
        le.preferredHeight = h;
    }

    void AddDispatchShot()
    {
        var card = UIFactory.Panel(chatContent, "Shot", new Vector2(780, 280), Vector2.zero, new Color(0.08f, 0.12f, 0.16f), true, 18);
        var t = UIFactory.Label(card.rectTransform, "T", "DISPATCH SCREENSHOT\nAMAZON RELAY · BOOKED\nDEADHEAD " + flow.DeadheadAssignedMiles + " MI  ·  EMPTY\nDFW9 → DWH1  ·  240 MI LOADED\n$2,450  ·  53' DRY  ·  38,500 LB", 28, new Color(0.92f, 0.95f, 0.97f), TextAnchor.UpperLeft);
        UIFactory.Stretch(t.rectTransform, 24, 24, 20, 20);
        var le = card.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 250;
        le.preferredHeight = 300;
    }

    void ShowView(GameObject view)
    {
        loadView.SetActive(view == loadView);
        chatView.SetActive(view == chatView);
        photosView.SetActive(view == photosView);
    }

    public void ShowTab(int tab)
    {
        if (tab == 0) ShowView(loadView);
        else if (tab == 1) ShowView(chatView);
        else { ShowView(photosView); RefreshPhotos(); }
        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                var label = tabButtons[i].GetComponentInChildren<Text>();
                if (label != null) label.color = i == tab ? new Color(1f, 0.52f, 0.12f) : new Color(0.58f, 0.66f, 0.72f);
            }
        }
    }

    public void Open(string tab = "load")
    {
        if (root == null) return;
        root.SetActive(true);
        callView.SetActive(false);
        cameraView.SetActive(false);
        previewView.SetActive(false);
        phone.gameObject.SetActive(true);
        if (phoneBezel != null) phoneBezel.SetActive(true);
        if (dimImage != null) dimImage.enabled = true;
        ShowTab(tab == "chat" ? 1 : 0);
        flow.SetPrompt(null);
        flow.ClearSubtitle();
        flow.SetHudVisible(false);
        flow.Player.SetUILock(true);
    }

    public void Close()
    {
        if (root == null) return;
        root.SetActive(false);
        cameraActive = false;
        flow.Player.ForceFirstPerson = false;
        previewOpen = false;
        if (cameraView != null) cameraView.SetActive(false);
        if (previewView != null) previewView.SetActive(false);
        RestoreFov();
        flow.SetInspectionMarkers(true);
        flow.SetHudVisible(true);
        flow.Player.SetUILock(false);
        flow.ReturnToPause();
    }

    public void ShowIncomingCall(Action answer, Action decline)
    {
        onAnswer = answer; onDecline = decline; onNext = null; callAnswered = false;
        root.SetActive(true);
        if (dimImage != null) dimImage.enabled = false;
        phone.gameObject.SetActive(false);
        if (phoneBezel != null) phoneBezel.SetActive(false);
        cameraView.SetActive(false);
        previewView.SetActive(false);
        callView.SetActive(true);
        callStatus.text = "INCOMING CALL  ·  RELAY DISPATCH";
        callName.text = "MIKE  ·  DISPATCH";
        callText.text = "";
        callAnswer.gameObject.SetActive(true);
        callDecline.gameObject.SetActive(true);
        callNext.gameObject.SetActive(false);
        flow.SetPrompt(null);
        flow.ClearSubtitle();
        flow.SetHudVisible(false);
        flow.Player.SetUILock(true);
    }

    public void BeginCallAnswered()
    {
        callAnswered = true;
        callStatus.text = "CONNECTED  ·  RELAY DISPATCH";
        callAnswer.gameObject.SetActive(false);
        callDecline.gameObject.SetActive(false);
        callNext.gameObject.SetActive(true);
    }

    public void SetCallLine(string text)
    {
        callText.text = text;
    }

    public void SetCallNext(Action next)
    {
        onNext = next;
        callNext.gameObject.SetActive(next != null);
    }

    public void EndCall()
    {
        callView.SetActive(false);
        phone.gameObject.SetActive(true);
        root.SetActive(false);
        flow.SetHudVisible(true);
        flow.Player.SetUILock(false);
        flow.ReturnToPause();
    }

    public void ShowCamera(PhotoTarget target)
    {
        sealTarget = target;
        root.SetActive(true);
        if (dimImage != null) dimImage.enabled = false;
        phone.gameObject.SetActive(false);
        if (phoneBezel != null) phoneBezel.SetActive(false);
        callView.SetActive(false);
        previewView.SetActive(false);
        cameraView.SetActive(true);
        cameraActive = true;
        flow.Player.ForceFirstPerson = true;
        // Re-aim from the eye: the player may have lined the shot up in third
        // person, where the chase camera's parallax puts the seal off-centre
        // once the view moves to the driver's head.
        if (target != null) flow.Player.LookAt(target.transform.position);
        flow.SetInspectionMarkers(false);
        Debug.Log("D2D CAMERA phone=False bezel=False dim=False camera=True");
        cameraHint.text = "CENTER THE SEAL NUMBER IN THE FRAME\nCLICK / SPACE  TAKE PHOTO   ·   ESC  BACK";
        ApplyCameraFov();
        flow.SetPrompt(null);
        flow.ClearSubtitle();
        flow.SetHudVisible(false);
        flow.Player.SetUILock(false);
        flow.Player.CaptureCursor();
    }

    void ApplyCameraFov()
    {
        if (flow == null || flow.Camera == null) return;
        if (!fovSaved)
        {
            gameplayFov = flow.Camera.fieldOfView;
            fovSaved = true;
        }
        flow.Camera.fieldOfView = Mathf.Min(gameplayFov, 48f);
    }

    void RestoreFov()
    {
        if (!fovSaved || flow == null || flow.Camera == null) return;
        flow.Camera.fieldOfView = gameplayFov;
        fovSaved = false;
    }

    public void CloseCamera()
    {
        cameraActive = false;
        flow.Player.ForceFirstPerson = false;
        cameraView.SetActive(false);
        root.SetActive(false);
        RestoreFov();
        flow.SetInspectionMarkers(true);
        flow.SetHudVisible(true);
        flow.Player.SetUILock(false);
        flow.ReturnToPause();
    }

    public void Capture()
    {
        if (!cameraActive || previewOpen) return;
        StartCoroutine(CaptureRoutine());
    }

    IEnumerator CaptureRoutine()
    {
        cameraActive = false;
        cameraView.SetActive(false);
        if (audio != null) audio.Shutter();
        flow.SetHudVisible(false);
        root.SetActive(false);
        yield return new WaitForEndOfFrame();
        var cam = flow.Camera;
        var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        var old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        // Validate against the camera that actually took the shot, before the
        // gameplay camera is restored.
        bool valid = ValidateSeal();
        // The shot is taken from the eyes. Restore the gameplay camera only
        // after the render, otherwise a third-person player photographs the
        // back of their own head and the seal fails validation.
        flow.Player.ForceFirstPerson = false;
        RenderTexture.active = rt;
        int px = Mathf.RoundToInt(Screen.width * CameraRectMin.x);
        int py = Mathf.RoundToInt(Screen.height * CameraRectMin.y);
        int pw = Mathf.RoundToInt(Screen.width * (CameraRectMax.x - CameraRectMin.x));
        int ph = Mathf.RoundToInt(Screen.height * (CameraRectMax.y - CameraRectMin.y));
        var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(px, py, pw, ph), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = old;
        rt.Release();
        Destroy(rt);
        RestoreFov();
        root.SetActive(true);
        lastPhoto = tex;
        ShowPreview(tex, valid);
    }

    bool ValidateSeal()
    {
        if (sealTarget == null) return false;
        var cam = flow.Camera;
        Vector3 vp = cam.WorldToViewportPoint(sealTarget.transform.position);
        float dist = Vector3.Distance(cam.transform.position, sealTarget.transform.position);
        bool ok = !(vp.z < 0 || vp.x < CameraRectMin.x + 0.02f || vp.x > CameraRectMax.x - 0.02f
            || vp.y < CameraRectMin.y + 0.04f || vp.y > CameraRectMax.y - 0.04f || dist > 5.2f);
        Debug.Log("D2D SEALCHECK ok=" + ok + " vp=" + vp + " dist=" + dist.ToString("F2")
            + " cam=" + cam.transform.position + " seal=" + sealTarget.transform.position
            + " fp=" + flow.Player.ForceFirstPerson + " tp=" + flow.Player.ThirdPersonActive);
        return ok;
    }

    void ShowPreview(Texture2D tex, bool valid)
    {
        previewOpen = true;
        previewValid = valid;
        previewView.SetActive(true);
        if (sendButton != null) sendButton.interactable = valid;
        previewImage.texture = tex;
        previewStatus.text = valid ? "Seal 482913 is legible and in frame." : "Seal number is not in frame. Retake.";
        previewStatus.color = valid ? new Color(0.4f, 0.95f, 0.65f) : new Color(1f, 0.45f, 0.4f);
        flow.Player.ReleaseCursor();
    }

    public void SendPhoto()
    {
        if (!previewOpen || lastPhoto == null || !previewValid) return;
        photos.Add(lastPhoto);
        RefreshPhotos();
        flow.OnSealPhotoSent(lastPhoto);
        previewOpen = false;
        previewView.SetActive(false);
        cameraView.SetActive(false);
        root.SetActive(false);
        RestoreFov();
        flow.SetHudVisible(true);
        flow.Player.SetUILock(false);
        flow.ReturnToPause();
    }

    void RefreshPhotos()
    {
        if (photosGrid == null) return;
        if (photosHint != null)
            photosHint.text = photos.Count > 0 ? "Seal photo sent to dispatch. Tap a photo to review." : "Seal and POD photos will appear here.";
        for (int i = photosGrid.childCount - 1; i >= 0; i--) Destroy(photosGrid.GetChild(i).gameObject);
        if (photos.Count == 0)
        {
            var empty = UIFactory.Panel(photosGrid, "Empty", new Vector2(380, 507), Vector2.zero, new Color(0.055f, 0.072f, 0.090f), true, 18);
            var ert = empty.rectTransform;
            ert.anchorMin = ert.anchorMax = new Vector2(0, 1);
            ert.pivot = new Vector2(0, 1);
            ert.anchoredPosition = Vector2.zero;
            var outline = empty.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
            outline.effectDistance = new Vector2(2f, 2f);
            var et = UIFactory.Label(ert, "EmptyText", "NO PHOTOS YET\n\nTake the seal photo\nfrom Chat.", 24, new Color(0.55f, 0.64f, 0.70f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(et.rectTransform, 24, 24, 24, 24);
        }
        for (int i = 0; i < photos.Count; i++)
        {
            var rt = UIFactory.Rect("Photo" + i, photosGrid);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(380, 507);
            rt.anchoredPosition = new Vector2((i % 2) * 420, -(i / 2) * 547);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = photos[i];
        }
    }

    public void AddMessage(string text, bool me = true)
    {
        AddChat(text, me);
    }

    public void AddProofChat()
    {
        AddChat("Seal 482913 photo sent.", true);
        AddChat("Proof received. Seal 482913 matches the BOL.", false);
    }

    public void Update()
    {
        if (cameraActive)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Capture();
            // Escape is handled by GameFlow so the camera and the phone cannot
            // both consume the same key and leave the cursor in a mixed state.
        }
    }
}
