using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds 1v1 room row + create-room form when the scene has empty Content / minimal create UI.</summary>
public static class TrucoRuntimeUiBuilders
{
    /// <summary>Wood-brown bar, large bold text (title / meta / players colors), compact green Entrar.</summary>
    public static OneVsOneRoomRowView CreateRoomRowTemplate(
        Transform contentParent,
        TMP_FontAsset font = null,
        Sprite rowPanelSprite = null,
        Sprite joinButtonSprite = null)
    {
        if (contentParent == null) return null;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        _ = rowPanelSprite;
        _ = joinButtonSprite;
        var rowGo = new GameObject("RoomRow_Pill", typeof(RectTransform));
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.SetParent(contentParent, false);
        rowGo.SetActive(false);
        var rowLe = rowGo.AddComponent<LayoutElement>();
        // ~2x prior row height for large touch targets and legible type on mobile
        rowLe.minHeight = 280f;
        rowLe.preferredHeight = 300f;
        rowLe.flexibleHeight = 0f;
        var rowBg = rowGo.AddComponent<Image>();
        rowBg.raycastTarget = false;
        rowBg.sprite = null;
        rowBg.type = Image.Type.Simple;
        rowBg.color = TrucoUiTheme.RoomListRowBar;
        var sh = rowGo.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
        sh.effectDistance = new Vector2(1.5f, -2f);
        sh.useGraphicAlpha = true;

        var hl = rowGo.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(32, 28, 24, 24);
        hl.spacing = 20;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = false;

        var col = new GameObject("Texts", typeof(RectTransform));
        col.transform.SetParent(rowRt, false);
        col.AddComponent<VerticalLayoutGroup>();
        var v = col.GetComponent<VerticalLayoutGroup>();
        v.spacing = 8;
        v.padding = new RectOffset(0, 0, 0, 0);
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        var colLe = col.AddComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;
        colLe.minWidth = 80f;

        const float nameSize = 54f;
        const float subSize = 42f;
        var t1 = RoomRowTextLine(col.transform, f, "Name", nameSize, FontStyles.Bold, TrucoUiTheme.RoomListRowTitle);
        var t2 = RoomRowTextLine(col.transform, f, "Meta", subSize, FontStyles.Bold, TrucoUiTheme.RoomListRowMeta);
        var t3 = RoomRowTextLine(col.transform, f, "Players", subSize, FontStyles.Bold, TrucoUiTheme.RoomListRowPlayers);

        var codeStrip = new GameObject("CodeStrip", typeof(RectTransform));
        codeStrip.transform.SetParent(rowRt, false);
        var codeV = codeStrip.AddComponent<VerticalLayoutGroup>();
        codeV.spacing = 4;
        codeV.childAlignment = TextAnchor.MiddleCenter;
        codeV.childControlWidth = true;
        codeV.childControlHeight = true;
        codeV.childForceExpandWidth = true;
        codeV.childForceExpandHeight = false;
        var csl = codeStrip.AddComponent<LayoutElement>();
        csl.minWidth = 0f;
        csl.minHeight = 72f;
        csl.preferredWidth = 0f;
        csl.preferredHeight = 120f;
        csl.flexibleWidth = 0f;
        csl.flexibleHeight = 0f;

        var hostCodeGo = new GameObject("HostCode", typeof(RectTransform));
        hostCodeGo.transform.SetParent(codeStrip.transform, false);
        var hLe = hostCodeGo.AddComponent<LayoutElement>();
        hLe.minHeight = 64f;
        hLe.minWidth = 160f;
        hLe.preferredWidth = 240f;
        hLe.preferredHeight = 72f;
        hLe.flexibleWidth = 1f;
        var hostT = hostCodeGo.AddComponent<TextMeshProUGUI>();
        hostT.alignment = TextAlignmentOptions.Center;
        hostT.fontSize = 44f;
        hostT.fontStyle = FontStyles.Bold;
        hostT.color = TrucoUiTheme.RoomListRowCodeDisplay;
        hostT.enableAutoSizing = false;
        if (f != null) hostT.font = f;
        hostT.raycastTarget = false;
        hostT.gameObject.SetActive(false);

        var joinInpRoot = new GameObject("JoinCodeInput", typeof(RectTransform));
        joinInpRoot.transform.SetParent(codeStrip.transform, false);
        var jLe2 = joinInpRoot.AddComponent<LayoutElement>();
        jLe2.minHeight = 64f;
        jLe2.minWidth = 180f;
        jLe2.preferredWidth = 280f;
        jLe2.preferredHeight = 70f;
        jLe2.flexibleWidth = 1f;
        var jImg = joinInpRoot.AddComponent<Image>();
        jImg.raycastTarget = true;
        jImg.type = Image.Type.Simple;
        jImg.color = new Color(0.98f, 0.96f, 0.92f, 1f);
        var jOutline = joinInpRoot.AddComponent<Outline>();
        jOutline.effectColor = TrucoUiTheme.RoomListCodeInputBorder;
        jOutline.effectDistance = new Vector2(1.2f, -1.2f);
        var jInp = joinInpRoot.AddComponent<TMP_InputField>();
        jInp.characterLimit = OneVsOnePrivateRoomCode.MaxPasswordLength;
        var jtRt = new GameObject("JText", typeof(RectTransform));
        jtRt.transform.SetParent(joinInpRoot.transform, false);
        var jtr = jtRt.GetComponent<RectTransform>();
        jtr.anchorMin = Vector2.zero; jtr.anchorMax = Vector2.one;
        jtr.offsetMin = new Vector2(8, 4); jtr.offsetMax = new Vector2(-8, -4);
        var jTcmp = jtRt.AddComponent<TextMeshProUGUI>();
        jTcmp.fontSize = 36f;
        jTcmp.fontStyle = FontStyles.Bold;
        jTcmp.color = TrucoUiTheme.CreateFormSubLabelCream;
        if (f != null) jTcmp.font = f;
        var jPh = new GameObject("JPh", typeof(RectTransform));
        jPh.transform.SetParent(joinInpRoot.transform, false);
        var jpr = jPh.GetComponent<RectTransform>();
        jpr.anchorMin = Vector2.zero; jpr.anchorMax = Vector2.one;
        jpr.offsetMin = new Vector2(8, 4); jpr.offsetMax = new Vector2(-8, -4);
        var jPht = jPh.AddComponent<TextMeshProUGUI>();
        jPht.text = "mín. 6 car.";
        jPht.fontSize = 24f;
        jPht.color = new Color(0.4f, 0.35f, 0.3f, 0.6f);
        if (f != null) jPht.font = f;
        jInp.textViewport = jtr;
        jInp.textComponent = jTcmp;
        jInp.placeholder = jPht;
        joinInpRoot.SetActive(false);

        var capLabGo = new GameObject("CodeCaption", typeof(RectTransform));
        capLabGo.transform.SetParent(codeStrip.transform, false);
        capLabGo.transform.SetAsFirstSibling();
        var ccap = capLabGo.AddComponent<TextMeshProUGUI>();
        ccap.text = TrucoTextosClient.CodigoParaUnir;
        ccap.fontSize = 20f;
        ccap.color = new Color(0.85f, 0.8f, 0.7f, 0.95f);
        if (f != null) ccap.font = f;
        ccap.alignment = TextAlignmentOptions.Center;
        ccap.raycastTarget = false;
        var capLe = capLabGo.AddComponent<LayoutElement>();
        capLe.minHeight = 22f;
        capLe.flexibleWidth = 1f;
        capLabGo.SetActive(false);

        var codeStripRoot = codeStrip;
        var joinGo = new GameObject("Join", typeof(RectTransform));
        joinGo.transform.SetParent(rowRt, false);
        var joinImg = joinGo.AddComponent<Image>();
        joinImg.raycastTarget = true;
        joinImg.sprite = null;
        joinImg.type = Image.Type.Simple;
        joinImg.color = TrucoUiTheme.RoomListJoinButtonBg;
        var jLe = joinGo.AddComponent<LayoutElement>();
        jLe.minWidth = 180f;
        jLe.preferredWidth = 196f;
        jLe.minHeight = 88f;
        jLe.preferredHeight = 96f;
        jLe.flexibleWidth = 0f;
        jLe.flexibleHeight = 0f;
        var btn = joinGo.AddComponent<Button>();
        btn.targetGraphic = joinImg;
        var jl = new GameObject("L", typeof(RectTransform));
        jl.transform.SetParent(joinGo.transform, false);
        var jrt = jl.GetComponent<RectTransform>();
        jrt.anchorMin = Vector2.zero;
        jrt.anchorMax = Vector2.one;
        jrt.offsetMin = new Vector2(6, 4);
        jrt.offsetMax = new Vector2(-6, -4);
        var jtmp = jl.AddComponent<TextMeshProUGUI>();
        jtmp.text = TrucoTextosClient.Entrar;
        jtmp.alignment = TextAlignmentOptions.Center;
        jtmp.fontSize = 32f;
        jtmp.fontStyle = FontStyles.Bold;
        jtmp.color = TrucoUiTheme.RoomListJoinButtonText;
        jtmp.enableAutoSizing = false;
        jtmp.enableWordWrapping = false;
        jtmp.raycastTarget = false;
        if (f != null) jtmp.font = f;

        var view = rowGo.AddComponent<OneVsOneRoomRowView>();
        view.SetRuntimeBinding(t1, t2, t3, btn, jtmp, codeStripRoot, ccap, hostT, jInp);
        return view;
    }

    static TextMeshProUGUI RoomRowTextLine(Transform parent, TMP_FontAsset font, string name, float size, FontStyles style, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = "—";
        t.fontSize = size;
        t.fontStyle = style;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = c;
        t.enableAutoSizing = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        t.margin = new Vector4(0, 0, 10, 0);
        t.characterSpacing = 0.25f;
        t.lineSpacing = 0f;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 14f;
        le.preferredHeight = size + 14f;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;
        return t;
    }

    /// <summary>Large-type create-room form: wood frame, cream inset, bottom action buttons, no tiny text.</summary>
    public static OneVsOneCreateRoomPanel EnsureCreateRoomForm(
        Transform roomCreationRoot,
        float formScale = 1.35f,
        RectTransform formParent = null)
    {
        if (roomCreationRoot == null) return null;
        if (HasNamedDescendant(roomCreationRoot, "Truco1v1CreateFormV5"))
            return roomCreationRoot.GetComponent<OneVsOneCreateRoomPanel>();
        DestroyLegacyFormIfAny(roomCreationRoot);
        DestroyTruco1v1CreateFormV1IfAny(roomCreationRoot);
        DestroyTruco1v1CreateFormV2IfAny(roomCreationRoot);
        DestroyTruco1v1CreateFormV3IfAny(roomCreationRoot);
        DestroyTruco1v1CreateFormV4IfAny(roomCreationRoot);

        var f = TMP_Settings.defaultFontAsset;
        var existing = roomCreationRoot.GetComponent<OneVsOneCreateRoomPanel>();
        var panel = existing != null ? existing : roomCreationRoot.gameObject.AddComponent<OneVsOneCreateRoomPanel>();

        var parentForForm = formParent != null ? (Transform)formParent : roomCreationRoot;
        var useEmbeddedLayout = formParent != null;
        // ~2.4–2.5× base font scale vs first pass; no flexible spacer (keeps actions above bottom bar)
        const int kNameFont = 58;
        const int kLabel = 50;
        const int kPublic = 58;
        const int kBtnCaption = 44;
        const int kFeeAmt = 44;

        var center = new GameObject("Truco1v1CreateFormV5", typeof(RectTransform));
        var cRt = center.GetComponent<RectTransform>();
        cRt.SetParent(parentForForm, false);
        if (useEmbeddedLayout)
        {
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;
            cRt.localScale = Vector3.one;
        }
        else
        {
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.sizeDelta = new Vector2(520, 700);
            cRt.anchoredPosition = Vector2.zero;
            cRt.localScale = Vector3.one * formScale;
        }

        var bg = center.AddComponent<Image>();
        bg.raycastTarget = true;
        bg.sprite = null;
        bg.color = TrucoUiTheme.CreateFormPanelCard;
        var frSh = center.AddComponent<Shadow>();
        frSh.effectColor = new Color(0f, 0f, 0f, 0.45f);
        frSh.effectDistance = new Vector2(3f, -3f);

        var leRoot = center.AddComponent<LayoutElement>();
        leRoot.minWidth = 10;
        leRoot.flexibleWidth = 1f;

        var inner = new GameObject("FormInner", typeof(RectTransform));
        inner.transform.SetParent(center.transform, false);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(12, 12);
        irt.offsetMax = new Vector2(-12, -12);
        var innerImg = inner.AddComponent<Image>();
        innerImg.raycastTarget = false;
        innerImg.color = TrucoUiTheme.CreateFormPanelInner;

        var v = inner.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(32, 32, 28, 32);
        v.spacing = 22;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;

        void AddFlexSpacer(string nm)
        {
            var sgo = new GameObject(nm, typeof(RectTransform));
            sgo.transform.SetParent(inner.transform, false);
            var sle = sgo.AddComponent<LayoutElement>();
            sle.minHeight = 0f;
            sle.flexibleHeight = 1f;
        }

        AddFlexSpacer("FormSpacerTop");
        var nameIn = CreateFormInputField("RoomName", inner.transform, f, TrucoTextosClient.NombreSala, kNameFont);
        var (pubT, privT) = CreateFormPublicPrivateAccessRow(inner.transform, f, kPublic);

        var passG = new GameObject("PassGroup", typeof(RectTransform));
        passG.transform.SetParent(inner.transform, false);
        var passGLe = passG.AddComponent<LayoutElement>();
        passGLe.minHeight = 140f;
        passGLe.preferredHeight = 160f;
        passGLe.flexibleHeight = 0f;
        passG.AddComponent<VerticalLayoutGroup>();
        var passGvl = passG.GetComponent<VerticalLayoutGroup>();
        passGvl.childControlHeight = true;
        passGvl.childControlWidth = true;
        passGvl.childForceExpandHeight = false;
        passGvl.childForceExpandWidth = true;
        passG.SetActive(false);
        var codeLbl = new GameObject("CodeLabel", typeof(RectTransform));
        codeLbl.transform.SetParent(passG.transform, false);
        var cLe = codeLbl.AddComponent<LayoutElement>();
        cLe.minHeight = 36f; cLe.flexibleWidth = 1f;
        var cLtmp = codeLbl.AddComponent<TextMeshProUGUI>();
        cLtmp.text = TrucoTextosClient.CodigoSala4;
        cLtmp.fontSize = 38f;
        cLtmp.fontStyle = FontStyles.Bold;
        cLtmp.color = TrucoUiTheme.CreateFormLabel;
        cLtmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (f != null) cLtmp.font = f;
        cLtmp.raycastTarget = false;
        var passIn = CreateFormInputField("PrivateCode", passG.transform, f, "mín. 6 caracteres", 44);
        if (passIn != null) passIn.characterLimit = OneVsOnePrivateRoomCode.MaxPasswordLength;

        var feeLblGo = new GameObject("FeeLabel", typeof(RectTransform));
        feeLblGo.transform.SetParent(inner.transform, false);
        var feeLE = feeLblGo.AddComponent<LayoutElement>();
        feeLE.minHeight = 56f;
        feeLE.preferredHeight = 56f;
        feeLE.flexibleHeight = 0f;
        var feeTmp = feeLblGo.AddComponent<TextMeshProUGUI>();
        feeTmp.text = TrucoTextosClient.EntradaMonedas;
        feeTmp.fontSize = kLabel;
        feeTmp.fontStyle = FontStyles.Bold;
        if (f != null) feeTmp.font = f;
        feeTmp.color = TrucoUiTheme.CreateFormLabel;
        feeTmp.alignment = TextAlignmentOptions.MidlineLeft;
        feeTmp.raycastTarget = false;
        var feeRow = new GameObject("Fees", typeof(RectTransform));
        feeRow.transform.SetParent(inner.transform, false);
        var feeRowLe = feeRow.AddComponent<LayoutElement>();
        feeRowLe.minHeight = 76f;
        feeRowLe.preferredHeight = 76f;
        feeRowLe.flexibleHeight = 0f;
        var feeH = feeRow.AddComponent<HorizontalLayoutGroup>();
        feeH.padding = new RectOffset(0, 0, 0, 0);
        feeH.spacing = 20;
        feeH.childAlignment = TextAnchor.MiddleLeft;
        feeH.childControlWidth = true;
        feeH.childControlHeight = true;
        feeH.childForceExpandWidth = false;
        feeH.childForceExpandHeight = false;
        var t10 = CreateFormFeeToggle(feeRow.transform, f, "10", true, kFeeAmt, out _);
        var t100 = CreateFormFeeToggle(feeRow.transform, f, "100", false, kFeeAmt, out _);
        var t500 = CreateFormFeeToggle(feeRow.transform, f, "500", false, kFeeAmt, out _);

        var btnRow = new GameObject("Btns", typeof(RectTransform));
        btnRow.transform.SetParent(inner.transform, false);
        var btnRowLe = btnRow.AddComponent<LayoutElement>();
        btnRowLe.minHeight = 104f;
        btnRowLe.preferredHeight = 104f;
        btnRowLe.flexibleHeight = 0f;
        var btnH = btnRow.AddComponent<HorizontalLayoutGroup>();
        btnH.padding = new RectOffset(0, 0, 0, 0);
        btnH.spacing = 20;
        btnH.childAlignment = TextAnchor.MiddleCenter;
        btnH.childControlWidth = true;
        btnH.childControlHeight = true;
        btnH.childForceExpandWidth = true;
        btnH.childForceExpandHeight = false;
        var ok = CreateFormButton("OK", btnRow.transform, f, TrucoTextosClient.CrearSala, true, kBtnCaption);
        var cancel = CreateFormButton("Cancel", btnRow.transform, f, TrucoTextosClient.Volver, false, kBtnCaption);
        AddFlexSpacer("FormSpacerBottom");

        panel.SetRuntimeBinding(
            nameIn,
            pubT,
            passG,
            passIn,
            t10,
            t100,
            t500,
            ok,
            cancel,
            privT);

        return panel;
    }

    /// <summary>Pública and Privada side by side; mutual exclusive via <see cref="OneVsOneCreateRoomPanel.Open"/>.</summary>
    static (Toggle publicToggle, Toggle privateToggle) CreateFormPublicPrivateAccessRow(Transform parent, TMP_FontAsset font, int labelFont)
    {
        var rowGo = new GameObject("AccessTypeRow", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        var rel = rowGo.AddComponent<LayoutElement>();
        rel.minHeight = 96f;
        rel.preferredHeight = 104f;
        rel.flexibleHeight = 0f;
        var h = rowGo.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 28;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 0, 0);
        int boxPx = Mathf.Max(48, labelFont - 6);
        var pubT = CreateFormAccessTogglePair(h.transform, "Pub", font, TrucoTextosClient.Publica, true, boxPx, labelFont);
        var privT = CreateFormAccessTogglePair(h.transform, "Priv", font, TrucoTextosClient.Privada, false, boxPx, labelFont);
        return (pubT, privT);
    }

    static Toggle CreateFormAccessTogglePair(Transform parent, string id, TMP_FontAsset font, string label, bool isOn, int boxPx, int labelFont)
    {
        var box = new GameObject("Tgl_" + id, typeof(RectTransform));
        box.transform.SetParent(parent, false);
        var row = box.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 14;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = false;
        var boxLe = box.AddComponent<LayoutElement>();
        boxLe.minWidth = 0f; boxLe.preferredWidth = 280f; boxLe.flexibleWidth = 1f;
        var tglGo = new GameObject("Box", typeof(RectTransform));
        tglGo.transform.SetParent(box.transform, false);
        var tglRt = tglGo.GetComponent<RectTransform>();
        tglRt.sizeDelta = new Vector2(boxPx, boxPx);
        var bLe = tglGo.AddComponent<LayoutElement>();
        bLe.minWidth = bLe.preferredWidth = boxPx;
        bLe.minHeight = bLe.preferredHeight = boxPx;
        var t = tglGo.AddComponent<Toggle>();
        t.isOn = isOn;
        var bgG = new GameObject("Bg", typeof(RectTransform));
        bgG.transform.SetParent(tglGo.transform, false);
        var br = bgG.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        var bgI = bgG.AddComponent<Image>();
        bgI.color = new Color(0.42f, 0.33f, 0.26f, 1f);
        var ck = new GameObject("C", typeof(RectTransform));
        ck.transform.SetParent(br, false);
        StretchFull(ck.GetComponent<RectTransform>());
        var ckI = ck.AddComponent<Image>();
        ckI.color = new Color(0.25f, 0.55f, 0.32f, 1f);
        t.graphic = ckI;
        t.targetGraphic = bgI;
        t.transition = Selectable.Transition.ColorTint;
        var cb = t.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        t.colors = cb;
        var lab = new GameObject("L", typeof(RectTransform));
        lab.transform.SetParent(box.transform, false);
        var ltmp = lab.AddComponent<TextMeshProUGUI>();
        ltmp.text = label;
        ltmp.fontSize = labelFont;
        ltmp.fontStyle = FontStyles.Bold;
        ltmp.alignment = TextAlignmentOptions.MidlineLeft;
        ltmp.color = TrucoUiTheme.CreateFormSubLabelCream;
        ltmp.raycastTarget = false;
        if (font != null) ltmp.font = font;
        var lLe = lab.AddComponent<LayoutElement>();
        lLe.minHeight = 48f; lLe.flexibleWidth = 1f;
        return t;
    }

    static Toggle CreateFormPublicRow(Transform parent, TMP_FontAsset font, int labelFont, out GameObject rowGo)
    {
        rowGo = new GameObject("PublicRow", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        var rel = rowGo.AddComponent<LayoutElement>();
        rel.minHeight = 88f;
        rel.preferredHeight = 88f;
        rel.flexibleHeight = 0f;
        var h = rowGo.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 18;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 0, 0);

        int boxPx = Mathf.Max(44, labelFont - 8);
        var box = new GameObject("ToggleBox", typeof(RectTransform));
        box.transform.SetParent(rowGo.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.sizeDelta = new Vector2(boxPx, boxPx);
        var boxLe = box.AddComponent<LayoutElement>();
        boxLe.minWidth = boxPx;
        boxLe.minHeight = boxPx;
        boxLe.preferredWidth = boxPx;
        boxLe.preferredHeight = boxPx;
        boxLe.flexibleWidth = 0f;
        boxLe.flexibleHeight = 0f;
        var t = box.AddComponent<Toggle>();
        t.isOn = true;
        var bgG = new GameObject("Bg", typeof(RectTransform));
        bgG.transform.SetParent(box.transform, false);
        var br = bgG.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        var bgI = bgG.AddComponent<Image>();
        bgI.color = new Color(0.42f, 0.33f, 0.26f, 1f);
        var ck = new GameObject("C", typeof(RectTransform));
        ck.transform.SetParent(br, false);
        StretchFull(ck.GetComponent<RectTransform>());
        var ckI = ck.AddComponent<Image>();
        ckI.color = new Color(0.25f, 0.55f, 0.32f, 1f);
        t.graphic = ckI;
        t.targetGraphic = bgI;
        t.transition = Selectable.Transition.ColorTint;
        var cb = t.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        t.colors = cb;

        var lab = new GameObject("Plabel", typeof(RectTransform));
        lab.transform.SetParent(rowGo.transform, false);
        var ltmp = lab.AddComponent<TextMeshProUGUI>();
        ltmp.text = TrucoTextosClient.Publica;
        ltmp.fontSize = labelFont;
        ltmp.fontStyle = FontStyles.Bold;
        ltmp.alignment = TextAlignmentOptions.MidlineLeft;
        ltmp.color = TrucoUiTheme.CreateFormSubLabelCream;
        ltmp.raycastTarget = false;
        if (font != null) ltmp.font = font;
        var lLe = lab.AddComponent<LayoutElement>();
        lLe.minHeight = 48f;
        lLe.flexibleWidth = 1f;
        return t;
    }

    static Toggle CreateFormFeeToggle(Transform parent, TMP_FontAsset font, string amount, bool isOn, float amountFont, out GameObject row)
    {
        row = new GameObject("Fee_" + amount, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minWidth = 140f;
        rowLe.preferredWidth = 156f;
        rowLe.minHeight = 68f;
        rowLe.preferredHeight = 68f;
        rowLe.flexibleWidth = 0f;
        var hr = row.AddComponent<HorizontalLayoutGroup>();
        hr.spacing = 10;
        hr.childAlignment = TextAnchor.MiddleLeft;
        hr.childControlWidth = true;
        hr.childControlHeight = true;
        hr.childForceExpandWidth = false;
        hr.childForceExpandHeight = false;

        int boxPx = Mathf.Clamp(Mathf.RoundToInt(amountFont + 8f), 40, 56);
        var box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(row.transform, false);
        var t = box.AddComponent<Toggle>();
        t.isOn = isOn;
        var boxLe = box.AddComponent<LayoutElement>();
        boxLe.minWidth = boxPx;
        boxLe.minHeight = boxPx;
        boxLe.preferredWidth = boxPx;
        boxLe.preferredHeight = boxPx;
        var bgG = new GameObject("Bg", typeof(RectTransform));
        bgG.transform.SetParent(box.transform, false);
        var br = bgG.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        var bgI = bgG.AddComponent<Image>();
        bgI.color = new Color(0.42f, 0.33f, 0.26f, 1f);
        var ck = new GameObject("C", typeof(RectTransform));
        ck.transform.SetParent(br, false);
        StretchFull(ck.GetComponent<RectTransform>());
        var ckI = ck.AddComponent<Image>();
        ckI.color = new Color(0.25f, 0.55f, 0.32f, 1f);
        t.graphic = ckI;
        t.targetGraphic = bgI;

        var lgo = new GameObject("L", typeof(RectTransform));
        lgo.transform.SetParent(row.transform, false);
        var ltmp = lgo.AddComponent<TextMeshProUGUI>();
        ltmp.text = amount;
        ltmp.fontSize = amountFont;
        ltmp.fontStyle = FontStyles.Bold;
        ltmp.alignment = TextAlignmentOptions.MidlineLeft;
        ltmp.color = TrucoUiTheme.CreateFormSubLabelCream;
        ltmp.raycastTarget = false;
        if (font != null) ltmp.font = font;
        var lLe = lgo.AddComponent<LayoutElement>();
        lLe.flexibleWidth = 0f;
        lLe.minWidth = 40f;
        return t;
    }

    static TMP_InputField CreateFormInputField(string name, Transform parent, TMP_FontAsset font, string ph, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        float h = Mathf.Max(96f, size + 48f);
        var rootLe = go.AddComponent<LayoutElement>();
        rootLe.minHeight = h;
        rootLe.preferredHeight = h;
        rootLe.flexibleHeight = 0f;
        rootLe.minWidth = 100f;
        var img = go.AddComponent<Image>();
        img.raycastTarget = true;
        img.sprite = null;
        img.color = TrucoUiTheme.InputBg;
        var input = go.AddComponent<TMP_InputField>();
        int padH = 22;
        int padV = 14;
        var textRt = new GameObject("Text", typeof(RectTransform));
        textRt.transform.SetParent(go.transform, false);
        var trt = textRt.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 0);
        trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(padH, padV);
        trt.offsetMax = new Vector2(-padH, -padV);
        var text = textRt.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        if (font != null) text.font = font;
        var phRt = new GameObject("Ph", typeof(RectTransform));
        phRt.transform.SetParent(go.transform, false);
        var prt = phRt.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0);
        prt.anchorMax = new Vector2(1, 1);
        prt.offsetMin = new Vector2(padH, padV);
        prt.offsetMax = new Vector2(-padH, -padV);
        var pht = phRt.AddComponent<TextMeshProUGUI>();
        pht.fontSize = size - 1;
        pht.text = ph;
        pht.fontStyle = FontStyles.Italic;
        pht.color = new Color(0.35f, 0.3f, 0.25f, 0.9f);
        if (font != null) pht.font = font;
        text.color = TrucoUiTheme.CreateFormSubLabelCream;
        text.enableWordWrapping = false;
        pht.enableWordWrapping = false;
        input.textViewport = trt;
        input.textComponent = text;
        input.placeholder = pht;
        return input;
    }

    static Button CreateFormButton(string name, Transform parent, TMP_FontAsset font, string label, bool primary, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 100f;
        le.preferredHeight = 100f;
        le.flexibleHeight = 0f;
        le.minWidth = 180f;
        le.flexibleWidth = 1f;
        var img = go.AddComponent<Image>();
        img.raycastTarget = true;
        img.sprite = null;
        img.color = primary ? TrucoUiTheme.CreateFormButtonPrimary : TrucoUiTheme.CreateFormButtonSecondary;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors;
        var baseCol = img.color;
        cb.normalColor = baseCol;
        cb.highlightedColor = baseCol * 1.08f;
        cb.pressedColor = baseCol * 0.9f;
        cb.selectedColor = baseCol;
        cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        b.colors = cb;
        var ch = new GameObject("Caption", typeof(RectTransform));
        ch.transform.SetParent(go.transform, false);
        var cr = ch.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero;
        cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(12, 8);
        cr.offsetMax = new Vector2(-12, -8);
        var t = ch.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = fontSize;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = TrucoUiTheme.CreateFormButtonText;
        t.enableAutoSizing = false;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        if (font != null) t.font = font;
        return b;
    }

    static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static bool HasNamedDescendant(Transform root, string name)
    {
        if (root == null) return false;
        var c = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < c.Length; i++)
            if (c[i] != null && c[i].name == name) return true;
        return false;
    }

    /// <summary>Remove old "RuntimeCreateForm" and its <see cref="OneVsOneCreateRoomPanel"/> on root so bindings are rebuilt.</summary>
    static void DestroyLegacyFormIfAny(Transform roomCreationRoot)
    {
        if (roomCreationRoot == null) return;
        var trs = roomCreationRoot.GetComponentsInChildren<Transform>(true);
        var removed = false;
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] == null || trs[i].name != "RuntimeCreateForm") continue;
            UnityEngine.Object.DestroyImmediate(trs[i].gameObject, true);
            removed = true;
        }
        if (!removed) return;
        var p = roomCreationRoot.GetComponent<OneVsOneCreateRoomPanel>();
        if (p != null) UnityEngine.Object.DestroyImmediate(p, true);
    }

    /// <summary>First create-form version (no cream inset); replaced by newer form roots.</summary>
    static void DestroyTruco1v1CreateFormV1IfAny(Transform roomCreationRoot)
    {
        if (roomCreationRoot == null) return;
        var trs = roomCreationRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] == null || trs[i].name != "Truco1v1CreateForm") continue;
            UnityEngine.Object.DestroyImmediate(trs[i].gameObject, true);
            var p = roomCreationRoot.GetComponent<OneVsOneCreateRoomPanel>();
            if (p != null) UnityEngine.Object.DestroyImmediate(p, true);
            return;
        }
    }

    static void DestroyTruco1v1CreateFormV2IfAny(Transform roomCreationRoot)
    {
        if (roomCreationRoot == null) return;
        var trs = roomCreationRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] == null || trs[i].name != "Truco1v1CreateFormV2") continue;
            UnityEngine.Object.DestroyImmediate(trs[i].gameObject, true);
            return;
        }
    }

    static void DestroyTruco1v1CreateFormV3IfAny(Transform roomCreationRoot)
    {
        if (roomCreationRoot == null) return;
        var trs = roomCreationRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] == null || trs[i].name != "Truco1v1CreateFormV3") continue;
            UnityEngine.Object.DestroyImmediate(trs[i].gameObject, true);
            return;
        }
    }

    static void DestroyTruco1v1CreateFormV4IfAny(Transform roomCreationRoot)
    {
        if (roomCreationRoot == null) return;
        var trs = roomCreationRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] == null || trs[i].name != "Truco1v1CreateFormV4") continue;
            UnityEngine.Object.DestroyImmediate(trs[i].gameObject, true);
            return;
        }
    }
}

/// <summary>Hooks MainMenu room list / room creation scene objects: layout, wood sprites, cream backgrounds.</summary>
public static class Truco1v1SceneUiWiring
{
    const string FormHostName = "1v1FormHost";
    public const string PrivateCodeListBarName = "PrivateCodeListBar";

    /// <summary>Removes legacy list-level code bar when rows carry per-room inputs.</summary>
    public static void RemoveGlobalPrivateCodeBarIfAny(Transform scrollContent)
    {
        if (scrollContent == null) return;
        for (int i = scrollContent.childCount - 1; i >= 0; i--)
        {
            var c = scrollContent.GetChild(i);
            if (c != null && c.name == PrivateCodeListBarName)
                UnityEngine.Object.Destroy(c.gameObject);
        }
    }

    public static void EnsureScrollContentLayout(Transform content)
    {
        if (content == null) return;
        var v = content.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.padding = new RectOffset(16, 16, 10, 10);
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        if (content.GetComponent<ContentSizeFitter>() == null)
        {
            var c = content.gameObject.AddComponent<ContentSizeFitter>();
            c.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            c.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }

    /// <summary>Wood from top bar + join CTA from create button on the same panel when present.</summary>
    public static void TryBorrowSpritesFromRoomList(GameObject roomList, out Sprite woodPanel, out Sprite joinButton)
    {
        woodPanel = null;
        joinButton = null;
        if (roomList == null) return;
        var top = FindDeep(roomList.transform, "Top Panel (1)");
        if (top != null)
        {
            var im = top.GetComponent<Image>();
            if (im != null) woodPanel = im.sprite;
        }
        var createT = FindDeep(roomList.transform, "CreateRoom Button");
        if (createT != null)
        {
            var im = createT.GetComponent<Image>();
            if (im != null) joinButton = im.sprite;
        }
    }

    /// <param name="topOffsetDown">Pixels reserved at top (header / wood bar).</param>
    /// <param name="bottomOffsetUp">Pixels reserved at bottom (nav bar) so action buttons stay visible.</param>
    public static RectTransform GetOrCreateFormHost(
        Transform roomCreationRoot,
        float topOffsetDown,
        float bottomOffsetUp = 160f,
        float horizontalInset = 20f)
    {
        if (roomCreationRoot == null) return null;
        var t = roomCreationRoot.Find(FormHostName);
        if (t == null)
        {
            var go = new GameObject(FormHostName, typeof(RectTransform));
            t = go.transform;
            t.SetParent(roomCreationRoot, false);
            t.SetAsFirstSibling();
        }
        var rt = (RectTransform)t;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(horizontalInset, bottomOffsetUp);
        rt.offsetMax = new Vector2(-horizontalInset, -topOffsetDown);
        return rt;
    }

    /// <summary>Legacy: min-6-chars field at list top; prefer per-row <see cref="TrucoRuntimeUiBuilders.CreateRoomRowTemplate"/> code strip.</summary>
    public static TMP_InputField EnsureRoomListPrivateCodeInput(Transform roomListRoot)
    {
        if (roomListRoot == null) return null;
        var existing = roomListRoot.Find(PrivateCodeListBarName);
        if (existing != null) return existing.GetComponentInChildren<TMP_InputField>(true);
        var scroll = roomListRoot.GetComponentInChildren<ScrollRect>(true);
        if (scroll == null || scroll.content == null) return null;
        var c = scroll.content;
        var bar = new GameObject(PrivateCodeListBarName, typeof(RectTransform));
        bar.transform.SetParent(c, false);
        bar.transform.SetAsFirstSibling();
        var leBar = bar.AddComponent<LayoutElement>();
        leBar.minHeight = 88f;
        leBar.preferredHeight = 92f;
        leBar.flexibleHeight = 0f;
        var h = bar.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 10, 10);
        h.spacing = 12;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        var font = TMP_Settings.defaultFontAsset;
        var lab = new GameObject("CodeLabel", typeof(RectTransform));
        lab.transform.SetParent(bar.transform, false);
        var ltmp = lab.AddComponent<TextMeshProUGUI>();
        ltmp.text = TrucoTextosClient.CodigoSala4;
        ltmp.fontSize = 24f;
        ltmp.color = new Color(0.15f, 0.12f, 0.1f, 1f);
        ltmp.raycastTarget = false;
        if (font != null) ltmp.font = font;
        var lLe = lab.AddComponent<LayoutElement>();
        lLe.minWidth = 100f;
        lLe.preferredWidth = 220f;
        lLe.flexibleWidth = 0f;
        lLe.minHeight = 36f;
        var inpRoot = new GameObject("CodeInput", typeof(RectTransform));
        inpRoot.transform.SetParent(bar.transform, false);
        var inpLe = inpRoot.AddComponent<LayoutElement>();
        inpLe.minWidth = 120f;
        inpLe.minHeight = 52f;
        inpLe.preferredWidth = 200f;
        inpLe.preferredHeight = 56f;
        inpLe.flexibleWidth = 1f;
        var img = inpRoot.AddComponent<Image>();
        img.raycastTarget = true;
        img.color = TrucoUiTheme.InputBg;
        var input = inpRoot.AddComponent<TMP_InputField>();
        input.characterLimit = OneVsOnePrivateRoomCode.MaxPasswordLength;
        var trt = new GameObject("Text", typeof(RectTransform));
        trt.transform.SetParent(inpRoot.transform, false);
        var tr = trt.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0);
        tr.anchorMax = new Vector2(1, 1);
        tr.offsetMin = new Vector2(10, 6);
        tr.offsetMax = new Vector2(-10, -6);
        var tcomp = trt.AddComponent<TextMeshProUGUI>();
        tcomp.fontSize = 28f;
        tcomp.fontStyle = FontStyles.Bold;
        if (font != null) tcomp.font = font;
        tcomp.color = TrucoUiTheme.CreateFormSubLabelCream;
        var ph = new GameObject("Ph", typeof(RectTransform));
        ph.transform.SetParent(inpRoot.transform, false);
        var prt = ph.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0);
        prt.anchorMax = new Vector2(1, 1);
        prt.offsetMin = new Vector2(10, 6);
        prt.offsetMax = new Vector2(-10, -6);
        var pht = ph.AddComponent<TextMeshProUGUI>();
        pht.text = "mín. 6";
        pht.fontSize = 22f;
        pht.color = new Color(0.45f, 0.4f, 0.36f, 0.8f);
        if (font != null) pht.font = font;
        input.textViewport = tr;
        input.textComponent = tcomp;
        input.placeholder = pht;
        return input;
    }

    /// <summary>Draw order + back chevron: top wood bar on top; back vertically centered in the bar.</summary>
    public static void PolishRoomCreationHeader(Transform roomCreationRoot, RectTransform formHost = null)
    {
        if (roomCreationRoot == null) return;
        if (formHost != null) formHost.SetAsFirstSibling();
        var top = roomCreationRoot.Find("Top Panel (1)");
        if (top == null) return;
        top.SetAsLastSibling();
        var back = top.Find("Back Button (1)");
        if (back == null) return;
        var br = back.GetComponent<RectTransform>();
        if (br == null) return;
        br.anchorMin = new Vector2(0f, 0.5f);
        br.anchorMax = new Vector2(0f, 0.5f);
        br.pivot = new Vector2(0.5f, 0.5f);
        br.anchoredPosition = new Vector2(80f, 0f);
        br.sizeDelta = new Vector2(112f, 112f);
        back.SetAsLastSibling();
    }

    public static void PolishRoomListShell(GameObject roomList, ScrollRect scroll)
    {
        if (roomList == null) return;
        var rootImg = roomList.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.enabled = true;
            rootImg.color = TrucoUiTheme.TournamentListScreenBg;
        }
        if (scroll == null) return;
        var bg = scroll.GetComponent<Image>();
        if (bg != null) bg.color = new Color(0.55f, 0.52f, 0.48f, 0.08f);
        if (scroll.viewport != null)
        {
            var vp = scroll.viewport.GetComponent<Image>();
            // Warm grey list area so wood-brown rows contrast
            if (vp != null) vp.color = new Color(0.75f, 0.71f, 0.66f, 0.92f);
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindDeep(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
