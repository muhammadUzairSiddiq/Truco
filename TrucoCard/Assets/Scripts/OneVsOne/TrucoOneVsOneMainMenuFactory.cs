using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the 1v1 room list UI on MainMenu at runtime if no <see cref="OneVsOneRoomListController"/> exists in the scene (zero manual references).</summary>
public static class TrucoOneVsOneMainMenuFactory
{
    const string RootName = "OneVsOneLobby_Auto";

    public static void EnsureOnMainMenu()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") return;
        if (Object.FindObjectOfType<OneVsOneRoomListController>(true) != null) return;
        if (GameObject.Find(RootName) != null) return;
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;
        Build(canvas.transform);
    }

    static void Build(Transform canvas)
    {
        var font = TMP_Settings.defaultFontAsset;

        var root = new GameObject(RootName);
        root.SetActive(false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.SetParent(canvas, false);
        StretchFull(rootRt);
        var rootCV = root.AddComponent<Canvas>();
        rootCV.overrideSorting = true;
        rootCV.sortingOrder = 32000;
        root.AddComponent<GraphicRaycaster>();
        var rootImg = root.AddComponent<Image>();
        rootImg.color = new Color(0.05f, 0.05f, 0.08f, 0.98f);
        rootImg.raycastTarget = true;

        const float headerH = 100f;
        const float sessionStripH = 200f;
        const float scrollBottomMargin = 12f;

        var header = CreateRect("Header", root.transform);
        SetAnchors(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -headerH), new Vector2(0, 0));
        var hl = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(20, 20, 16, 16);
        hl.spacing = 12;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childForceExpandWidth = false;
        var headerBg = header.gameObject.AddComponent<Image>();
        headerBg.color = new Color(0.1f, 0.1f, 0.14f, 1f);
        headerBg.raycastTarget = false;
        var title = TmpText("Title", header, font, TrucoTextosClient.Partida1v1 + " — " + TrucoTextosClient.SeleccionarMesa, 30);
        var leT = title.gameObject.AddComponent<LayoutElement>();
        leT.flexibleWidth = 1f;
        leT.minWidth = 120;
        var btnRefresh = UiButtonLarge("BtnRefresh", header, font, TrucoTextosClient.ActualizarLista, 28);
        var btnCreate = UiButtonLarge("BtnCreate", header, font, TrucoTextosClient.CrearSala, 28);
        var btnBack = UiButtonLarge("BtnBack", header, font, TrucoTextosClient.Volver, 28);

        var scrollGo = new GameObject("Scroll");
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.SetParent(root.transform, false);
        // Scroll fills under header; session strip (when active) overlays the bottom — no permanent empty gap.
        SetAnchors(scrollRt, Vector2.zero, Vector2.one, new Vector2(0, scrollBottomMargin), new Vector2(0, -headerH));

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.ignoreLayout = true;

        var viewport = CreateRect("Viewport", scrollRt);
        StretchFull(viewport);
        viewport.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.1f, 0.12f, 1f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateRect("Content", viewport);
        StretchFull(content);
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 14;
        v.padding = new RectOffset(20, 20, 16, 16);
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = content;
        scroll.viewport = viewport;

        var rowGo = new GameObject("RowTemplate");
        rowGo.SetActive(false);
        var rowRt = rowGo.AddComponent<RectTransform>();
        rowRt.SetParent(content, false);
        var rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.minHeight = 128;
        rowLe.preferredHeight = 132;
        var rowCard = rowGo.AddComponent<Image>();
        rowCard.color = new Color(0.16f, 0.19f, 0.25f, 1f);
        rowCard.raycastTarget = false;
        var rowHl = rowGo.AddComponent<HorizontalLayoutGroup>();
        rowHl.padding = new RectOffset(20, 20, 14, 14);
        rowHl.spacing = 16;
        rowHl.childAlignment = TextAnchor.MiddleLeft;
        rowHl.childControlWidth = true;
        rowHl.childForceExpandWidth = true;

        var col = CreateRect("TextCol", rowRt);
        var colLe = col.gameObject.AddComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;
        colLe.minWidth = 200;
        var vCol = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vCol.spacing = 6;
        vCol.childAlignment = TextAnchor.UpperLeft;

        var nm = RowLineText("Name", col, font, "—", 24);
        var meta = RowLineText("Meta", col, font, "—", 20);
        var pl = RowLineText("Players", col, font, "—", 20);

        var joinCol = CreateRect("JoinCol", rowRt);
        var jvl = joinCol.gameObject.AddComponent<VerticalLayoutGroup>();
        jvl.spacing = 4f;
        jvl.childAlignment = TextAnchor.UpperCenter;
        jvl.childControlWidth = true;
        jvl.childControlHeight = true;
        jvl.childForceExpandWidth = true;
        jvl.childForceExpandHeight = false;
        var jcolLe = joinCol.gameObject.AddComponent<LayoutElement>();
        jcolLe.minWidth = 152;
        jcolLe.preferredWidth = 168;
        jcolLe.minHeight = 64;
        jcolLe.preferredHeight = 96;

        var jn = UiButtonJoin("Join", joinCol, font, TrucoTextosClient.Entrar);
        var joinLabel = jn.GetComponentInChildren<TextMeshProUGUI>();

        var expGo = new GameObject("ExpiredHint", typeof(RectTransform));
        expGo.transform.SetParent(joinCol, false);
        var expTmp = expGo.AddComponent<TextMeshProUGUI>();
        expTmp.text = string.Empty;
        expTmp.fontSize = 18f;
        expTmp.fontStyle = FontStyles.Bold;
        expTmp.alignment = TextAlignmentOptions.Center;
        expTmp.color = new Color(0.82f, 0.14f, 0.1f, 1f);
        expTmp.raycastTarget = false;
        if (font != null) expTmp.font = font;
        var expLe = expGo.AddComponent<LayoutElement>();
        expLe.minHeight = 22f;
        expLe.preferredHeight = 24f;
        expGo.SetActive(false);

        var row = rowGo.AddComponent<OneVsOneRoomRowView>();
        row.SetRuntimeBinding(nm, meta, pl, jn, joinLabel, null, null, null, null, expTmp);

        var sessionStrip = CreateRect("PhotonSessionStrip", root.transform);
        SetAnchors(sessionStrip, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, sessionStripH));
        sessionStrip.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.98f);
        var svl = sessionStrip.gameObject.AddComponent<VerticalLayoutGroup>();
        svl.padding = new RectOffset(24, 24, 20, 20);
        svl.childAlignment = TextAnchor.MiddleCenter;
        svl.spacing = 8;
        var st = TmpTextCenter("Status", sessionStrip, font, string.Empty, 24f);
        var cd = TmpTextCenter("Countdown", sessionStrip, font, string.Empty, 56f);
        sessionStrip.gameObject.SetActive(false);
        var sessionUi = root.AddComponent<OneVsOnePhotonSessionUi>();
        sessionUi.SetRuntimeWiring(sessionStrip.gameObject, st, cd);

        var pwdRoot = new GameObject("PasswordOverlay");
        pwdRoot.SetActive(false);
        var pwdRt = pwdRoot.AddComponent<RectTransform>();
        pwdRt.SetParent(root.transform, false);
        StretchFull(pwdRt);
        pwdRoot.AddComponent<Image>().color = new Color(0, 0, 0, 0.62f);
        var pwdBox = CreateRect("Box", pwdRt);
        SetAnchors(pwdBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-200, -110), new Vector2(200, 110));
        pwdBox.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f, 1f);
        var pwdField = InputFieldLarge("Password", pwdBox, font, TrucoTextosClient.ContrasenaSala, 20);
        var pwdOk = UiButtonLarge("PwdOk", pwdBox, font, TrucoTextosClient.Unirse, 20);
        var pwdCancel = UiButtonLarge("PwdCancel", pwdBox, font, TrucoTextosClient.Volver, 20);
        SetAnchors(pwdField.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(1, 0.95f), new Vector2(20, 0), new Vector2(-20, 0));
        SetAnchors(pwdOk.GetComponent<RectTransform>(), new Vector2(0, 0.08f), new Vector2(0.48f, 0.45f), new Vector2(20, 0), new Vector2(-6, 0));
        SetAnchors(pwdCancel.GetComponent<RectTransform>(), new Vector2(0.52f, 0.08f), new Vector2(1, 0.45f), new Vector2(6, 0), new Vector2(-20, 0));

        var createRoot = new GameObject("CreateRoomPanel");
        createRoot.SetActive(false);
        var createRt = createRoot.AddComponent<RectTransform>();
        createRt.SetParent(root.transform, false);
        StretchFull(createRt);
        createRoot.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var center = CreateRect("Center", createRt);
        SetAnchors(center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240, -300), new Vector2(240, 300));
        center.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.16f, 0.2f, 1f);
        var cvL = center.gameObject.AddComponent<VerticalLayoutGroup>();
        cvL.padding = new RectOffset(24, 24, 24, 24);
        cvL.spacing = 16;

        var nameIn = InputFieldLarge("RoomName", center, font, TrucoTextosClient.NombreSala, 22);
        var nameLe = nameIn.GetComponent<LayoutElement>();
        if (nameLe != null) { nameLe.minHeight = 56; }
        var pub = ToggleField("Public", center, font, TrucoTextosClient.Publica, true);
        var passGroup = new GameObject("PassGroup");
        passGroup.SetActive(false);
        var passRt = passGroup.AddComponent<RectTransform>();
        passRt.SetParent(center, false);
        var passLe = passGroup.AddComponent<LayoutElement>();
        passLe.minHeight = 56;
        var passIn = InputFieldLarge("RoomPass", passRt, font, TrucoTextosClient.ContrasenaSala, 20);

        var feeRow = CreateRect("Fees", center);
        var feeLe = feeRow.gameObject.AddComponent<LayoutElement>();
        feeLe.minHeight = 52;
        var feeHl = feeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        feeHl.spacing = 12;
        feeHl.childAlignment = TextAnchor.MiddleLeft;
        var t10 = ToggleField("T10", feeRow, font, "10", true);
        var t100 = ToggleField("T100", feeRow, font, "100", false);
        var t500 = ToggleField("T500", feeRow, font, "500", false);

        var btns = CreateRect("Btns", center);
        var btnRowLe = btns.gameObject.AddComponent<LayoutElement>();
        btnRowLe.minHeight = 58;
        var btnsHl = btns.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnsHl.spacing = 20;
        btnsHl.childAlignment = TextAnchor.MiddleCenter;
        var ok = UiButtonLarge("Confirm", btns, font, "OK", 24);
        var cancel = UiButtonLarge("CancelCreate", btns, font, TrucoTextosClient.Volver, 24);
        var okLe = ok.GetComponent<LayoutElement>();
        var caLe = cancel.GetComponent<LayoutElement>();
        if (okLe != null) { okLe.minWidth = 180; okLe.minHeight = 56; }
        if (caLe != null) { caLe.minWidth = 180; caLe.minHeight = 56; }

        var createPanel = createRoot.AddComponent<OneVsOneCreateRoomPanel>();
        createPanel.SetRuntimeBinding(
            nameIn,
            pub,
            passGroup,
            passIn,
            t10,
            t100,
            t500,
            ok,
            cancel,
            null);

        var list = root.AddComponent<OneVsOneRoomListController>();
        list.ApplyRuntimeWiring(
            root,
            content,
            row,
            createPanel,
            btnRefresh,
            btnCreate,
            btnBack,
            pwdRoot,
            pwdField,
            pwdOk,
            pwdCancel,
            null,
            OneVsOnePhotonFlow.EnsureInstance());
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        return r;
    }

    static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static void SetAnchors(RectTransform r, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = offMin;
        r.offsetMax = offMax;
    }

    static TextMeshProUGUI TmpText(string name, Transform parent, TMP_FontAsset font, string text, float size)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        if (font != null) t.font = font;
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 40;
        le.flexibleWidth = 1f;
        return t;
    }

    static TextMeshProUGUI RowLineText(string name, Transform parent, TMP_FontAsset font, string text, float size)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.Left;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Ellipsis;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = size * 1.3f;
        le.preferredHeight = size * 1.3f;
        le.flexibleWidth = 1f;
        return t;
    }

    static TextMeshProUGUI TmpTextCenter(string name, Transform parent, TMP_FontAsset font, string text, float size)
    {
        var t = RowLineText(name, parent, font, text, size);
        t.alignment = TextAlignmentOptions.Center;
        var le = t.GetComponent<LayoutElement>();
        if (le != null) { le.flexibleWidth = 1f; le.minWidth = 100; }
        return t;
    }

    static Button UiButtonLarge(string name, Transform parent, TMP_FontAsset font, string label, int fontSize = 20)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.48f, 0.82f, 1f);
        var b = go.AddComponent<Button>();
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 140;
        le.minHeight = 56;
        le.preferredWidth = 150;
        le.preferredHeight = 56;
        var child = new GameObject("Label");
        var ct = child.AddComponent<RectTransform>();
        ct.SetParent(r, false);
        StretchFull(ct);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        return b;
    }

    static Button UiButtonJoin(string name, Transform parent, TMP_FontAsset font, string label)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.88f, 0.72f, 0.18f, 1f);
        var b = go.AddComponent<Button>();
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 152;
        le.minHeight = 64;
        le.preferredWidth = 160;
        le.preferredHeight = 64;
        var child = new GameObject("Label");
        var ct = child.AddComponent<RectTransform>();
        ct.SetParent(r, false);
        StretchFull(ct);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        return b;
    }

    static TMP_InputField InputFieldLarge(string name, Transform parent, TMP_FontAsset font, string placeholder, int textSize)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.2f, 0.25f, 1f);
        var input = go.AddComponent<TMP_InputField>();
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 48;
        var textRt = CreateRect("Text", r);
        StretchFull(textRt);
        var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = textSize;
        if (font != null) text.font = font;
        var phRt = CreateRect("Placeholder", r);
        StretchFull(phRt);
        var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
        ph.fontSize = textSize;
        ph.text = placeholder;
        ph.fontStyle = FontStyles.Italic;
        ph.color = new Color(1, 1, 1, 0.45f);
        if (font != null) ph.font = font;
        input.textViewport = textRt;
        input.textComponent = text;
        input.placeholder = ph;
        return input;
    }

    static Button UiButton(string name, Transform parent, TMP_FontAsset font, string label)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.45f, 0.75f, 1f);
        var b = go.AddComponent<Button>();
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 90;
        le.preferredHeight = 36;
        var child = new GameObject("Label");
        var ct = child.AddComponent<RectTransform>();
        ct.SetParent(r, false);
        StretchFull(ct);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        return b;
    }

    static TMP_InputField InputField(string name, Transform parent, TMP_FontAsset font, string placeholder)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        var input = go.AddComponent<TMP_InputField>();
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 36;
        var textRt = CreateRect("Text", r);
        StretchFull(textRt);
        var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 15;
        if (font != null) text.font = font;
        var phRt = CreateRect("Placeholder", r);
        StretchFull(phRt);
        var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
        ph.fontSize = 15;
        ph.text = placeholder;
        ph.fontStyle = FontStyles.Italic;
        ph.color = new Color(1, 1, 1, 0.45f);
        if (font != null) ph.font = font;
        input.textViewport = textRt;
        input.textComponent = text;
        input.placeholder = ph;
        return input;
    }

    static Toggle ToggleField(string name, Transform parent, TMP_FontAsset font, string label, bool on)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent, false);
        var t = go.AddComponent<Toggle>();
        t.isOn = on;
        var bg = new GameObject("Bg");
        var br = bg.AddComponent<RectTransform>();
        br.SetParent(r, false);
        SetAnchors(br, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, -10), new Vector2(20, 10));
        bg.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
        var ck = new GameObject("Check");
        var cr = ck.AddComponent<RectTransform>();
        cr.SetParent(br, false);
        StretchFull(cr);
        ck.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f, 1f);
        t.graphic = ck.GetComponent<Image>();
        t.targetGraphic = bg.GetComponent<Image>();

        var lbl = TmpText("Lbl", r, font, label, 15);
        var lr = lbl.GetComponent<RectTransform>();
        SetAnchors(lr, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 0), new Vector2(0, 0));

        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 80;
        le.minHeight = 28;
        return t;
    }
}
