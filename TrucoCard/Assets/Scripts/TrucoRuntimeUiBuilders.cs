using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds 1v1 room row + create-room form when the scene has empty Content / minimal create UI.</summary>
public static class TrucoRuntimeUiBuilders
{
    public static OneVsOneRoomRowView CreateRoomRowTemplate(Transform contentParent, TMP_FontAsset font = null)
    {
        if (contentParent == null) return null;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        var rowGo = new GameObject("RoomRow_Template", typeof(RectTransform));
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.SetParent(contentParent, false);
        rowGo.SetActive(false);
        var rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.minHeight = 120;
        rowLe.preferredHeight = 128;
        var rowBg = rowGo.AddComponent<Image>();
        rowBg.color = TrucoUiTheme.RoomRowPanel;
        rowBg.raycastTarget = false;
        var hl = rowGo.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(20, 20, 12, 12);
        hl.spacing = 12;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childForceExpandWidth = true;

        var col = new GameObject("Texts", typeof(RectTransform));
        col.transform.SetParent(rowRt, false);
        var colRt = col.GetComponent<RectTransform>();
        var v = col.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4;
        v.childAlignment = TextAnchor.UpperLeft;
        var colLe = col.AddComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;
        colLe.minWidth = 200;

        var t1 = TmpOn(col.transform, f, "Name", 24);
        var t2 = TmpOn(col.transform, f, "Meta", 20);
        var t3 = TmpOn(col.transform, f, "Players", 20);

        var joinGo = new GameObject("Join", typeof(RectTransform));
        joinGo.transform.SetParent(rowRt, false);
        var joinImg = joinGo.AddComponent<Image>();
        joinImg.color = TrucoUiTheme.RoomRowJoin;
        var jLe = joinGo.AddComponent<LayoutElement>();
        jLe.minWidth = 180;
        jLe.minHeight = 72;
        var btn = joinGo.AddComponent<Button>();
        var jl = new GameObject("L", typeof(RectTransform));
        jl.transform.SetParent(joinGo.transform, false);
        var jrt = jl.GetComponent<RectTransform>();
        jrt.anchorMin = Vector2.zero; jrt.anchorMax = Vector2.one;
        jrt.offsetMin = jrt.offsetMax = Vector2.zero;
        var jtmp = jl.AddComponent<TextMeshProUGUI>();
        jtmp.text = "Join";
        jtmp.alignment = TextAlignmentOptions.Center;
        jtmp.fontSize = 22;
        if (f != null) jtmp.font = f;

        var view = rowGo.AddComponent<OneVsOneRoomRowView>();
        view.SetRuntimeBinding(t1, t2, t3, btn, jtmp);
        return view;
    }

    static TextMeshProUGUI TmpOn(Transform parent, TMP_FontAsset font, string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = "—";
        t.fontSize = size;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.Left;
        t.color = TrucoUiTheme.TextPrimary;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = size * 1.25f;
        return t;
    }

    /// <summary>Builds a large center form; parent should be a full-screen panel. Uses scale 2 on form center.</summary>
    public static OneVsOneCreateRoomPanel EnsureCreateRoomForm(Transform roomCreationRoot, float formScale = 1.35f)
    {
        if (roomCreationRoot == null) return null;
        var existing = roomCreationRoot.GetComponent<OneVsOneCreateRoomPanel>();
        if (existing != null) return existing;

        var f = TMP_Settings.defaultFontAsset;
        var panel = roomCreationRoot.gameObject.AddComponent<OneVsOneCreateRoomPanel>();

        var center = new GameObject("RuntimeCreateForm", typeof(RectTransform));
        var cRt = center.GetComponent<RectTransform>();
        cRt.SetParent(roomCreationRoot, false);
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(520, 700);
        cRt.anchoredPosition = Vector2.zero;
        cRt.localScale = Vector3.one * formScale;
        var v = center.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 20, 20);
        v.spacing = 14;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;
        var bg = center.AddComponent<Image>();
        bg.color = TrucoUiTheme.CreateFormBg;
        var nameIn = TrucoOneVsOneMainMenuFactory_InputField("RoomName", center.transform, f, "Room name", 24);
        var pubT = TrucoOneVsOneMainMenuFactory_Toggle("Public", center.transform, f, "Public", true);
        var passG = new GameObject("PassGroup", typeof(RectTransform));
        passG.transform.SetParent(center.transform, false);
        var passGLe = passG.AddComponent<LayoutElement>();
        passGLe.minHeight = 56;
        passG.SetActive(false);
        var passIn = TrucoOneVsOneMainMenuFactory_InputField("Password", passG.transform, f, "Password", 20);
        StretchFull(passIn.GetComponent<RectTransform>());

        var feeRow = new GameObject("Fees", typeof(RectTransform));
        feeRow.transform.SetParent(center.transform, false);
        var feeH = feeRow.AddComponent<HorizontalLayoutGroup>();
        feeH.spacing = 12;
        var t10 = TrucoOneVsOneMainMenuFactory_Toggle("T10", feeRow.transform, f, "10", true);
        var t100 = TrucoOneVsOneMainMenuFactory_Toggle("T100", feeRow.transform, f, "100", false);
        var t500 = TrucoOneVsOneMainMenuFactory_Toggle("T500", feeRow.transform, f, "500", false);
        var btnRow = new GameObject("Btns", typeof(RectTransform));
        btnRow.transform.SetParent(center.transform, false);
        var btnH = btnRow.AddComponent<HorizontalLayoutGroup>();
        btnH.spacing = 20;
        var ok = TrucoOneVsOneMainMenuFactory_Btn("OK", btnRow.transform, f, "Create", 26);
        var cancel = TrucoOneVsOneMainMenuFactory_Btn("Cancel", btnRow.transform, f, "Back", 24);

        panel.SetRuntimeBinding(
            nameIn,
            pubT,
            passG,
            passIn,
            t10,
            t100,
            t500,
            ok,
            cancel);

        if (pubT != null)
        {
            pubT.onValueChanged.AddListener(_ =>
            {
                passG.SetActive(!pubT.isOn);
            });
        }

        return panel;
    }

    static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // Minimal copies of factory privates (same layout as TrucoOneVsOneMainMenuFactory) — project already compiles the factory; these stay local to avoid public API churn.
    static TMP_InputField TrucoOneVsOneMainMenuFactory_InputField(string name, Transform parent, TMP_FontAsset font, string ph, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 56;
        go.AddComponent<Image>().color = TrucoUiTheme.InputBg;
        var input = go.AddComponent<TMP_InputField>();
        var textRt = new GameObject("Text", typeof(RectTransform));
        textRt.transform.SetParent(go.transform, false);
        StretchFull(textRt.GetComponent<RectTransform>());
        var text = textRt.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        if (font != null) text.font = font;
        var phRt = new GameObject("Ph", typeof(RectTransform));
        phRt.transform.SetParent(go.transform, false);
        StretchFull(phRt.GetComponent<RectTransform>());
        var pht = phRt.AddComponent<TextMeshProUGUI>();
        pht.fontSize = size;
        pht.text = ph;
        pht.fontStyle = FontStyles.Italic;
        pht.color = new Color(TrucoUiTheme.TextSecondary.r, TrucoUiTheme.TextSecondary.g, TrucoUiTheme.TextSecondary.b, 0.5f);
        if (font != null) pht.font = font;
        text.color = TrucoUiTheme.TextPrimary;
        input.textViewport = textRt.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = pht;
        return input;
    }

    static Toggle TrucoOneVsOneMainMenuFactory_Toggle(string name, Transform parent, TMP_FontAsset font, string label, bool on)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Toggle>();
        t.isOn = on;
        var bg = new GameObject("Bg", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        var br = bg.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0.5f); br.anchorMax = new Vector2(0, 0.5f);
        br.sizeDelta = new Vector2(28, 28);
        var bgI = bg.AddComponent<Image>();
        bgI.color = new Color(0.5f, 0.45f, 0.4f, 0.9f);
        var ck = new GameObject("C", typeof(RectTransform));
        ck.transform.SetParent(br, false);
        StretchFull(ck.GetComponent<RectTransform>());
        var ckI = ck.AddComponent<Image>();
        ckI.color = TrucoUiTheme.AccentGreen;
        t.graphic = ckI;
        t.targetGraphic = bgI;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 36;
        le.minWidth = 120;
        return t;
    }

    static Button TrucoOneVsOneMainMenuFactory_Btn(string name, Transform parent, TMP_FontAsset font, string label, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = TrucoUiTheme.AccentGreen;
        var b = go.AddComponent<Button>();
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 200;
        le.minHeight = 56;
        var ch = new GameObject("L", typeof(RectTransform));
        ch.transform.SetParent(go.transform, false);
        StretchFull(ch.GetComponent<RectTransform>());
        var t = ch.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        if (font != null) t.font = font;
        t.color = new Color(0.98f, 0.97f, 0.95f, 1f);
        return b;
    }
}
