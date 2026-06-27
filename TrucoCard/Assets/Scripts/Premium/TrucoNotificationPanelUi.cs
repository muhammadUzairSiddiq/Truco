using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Professional scrollable notification feed on the Notification Panel tab.</summary>
public static class TrucoNotificationPanelUi
{
    const string BuiltName = "TrucoNotificationFeed";
    static RectTransform _content;
    static TextMeshProUGUI _emptyLabel;
    static Transform _panelRoot;

    public static void Ensure(Transform notificationPanel)
    {
        if (notificationPanel == null) return;
        _panelRoot = notificationPanel;
        if (notificationPanel.Find(BuiltName) != null)
        {
            BindExisting(notificationPanel.Find(BuiltName));
            return;
        }

        for (int i = notificationPanel.childCount - 1; i >= 0; i--)
            notificationPanel.GetChild(i).gameObject.SetActive(false);

        var root = new GameObject(BuiltName, typeof(RectTransform));
        root.transform.SetParent(notificationPanel, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, 100f);
        rt.offsetMax = new Vector2(0f, -20f);

        var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(root.transform, false);
        var hrt = header.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 96f);
        var hImg = header.GetComponent<Image>();
        var wood = TrucoUiAssetLoader.Panel;
        if (wood != null) { hImg.sprite = wood; hImg.type = Image.Type.Sliced; hImg.color = Color.white; }
        else hImg.color = new Color(0.44f, 0.30f, 0.20f, 1f);
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(header.transform, false);
        Stretch(titleGo.GetComponent<RectTransform>());
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = TrucoTextosClient.NotificacionesTitulo.ToUpperInvariant();
        title.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        title.fontSize = 36f;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;

        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(root.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(16f, 12f);
        srt.offsetMax = new Vector2(-16f, -104f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        Stretch(vrt);
        viewport.AddComponent<Image>().color = new Color(0.75f, 0.71f, 0.66f, 0.92f);
        viewport.AddComponent<RectMask2D>();

        _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        _content.SetParent(viewport.transform, false);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = new Vector2(0f, 0f);
        var v = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 12f;
        v.padding = new RectOffset(8, 8, 8, 16);
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vrt;
        scroll.content = _content;

        var emptyGo = new GameObject("Empty", typeof(RectTransform));
        emptyGo.transform.SetParent(root.transform, false);
        var ert = emptyGo.GetComponent<RectTransform>();
        Stretch(ert);
        _emptyLabel = emptyGo.AddComponent<TextMeshProUGUI>();
        _emptyLabel.text = TrucoTextosClient.NotificacionesVacio;
        _emptyLabel.fontSize = 26f;
        _emptyLabel.color = new Color(0.35f, 0.32f, 0.28f, 1f);
        _emptyLabel.alignment = TextAlignmentOptions.Center;
        _emptyLabel.raycastTarget = false;

        TrucoNotificationLog.OnChanged -= Refresh;
        TrucoNotificationLog.OnChanged += Refresh;
        Refresh();
    }

    static void BindExisting(Transform built)
    {
        _content = built.Find("Scroll/Viewport/Content") as RectTransform;
        var empty = built.Find("Empty");
        _emptyLabel = empty != null ? empty.GetComponent<TextMeshProUGUI>() : null;
        TrucoNotificationLog.OnChanged -= Refresh;
        TrucoNotificationLog.OnChanged += Refresh;
        Refresh();
    }

    public static void Refresh()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Object.Destroy(_content.GetChild(i).gameObject);

        var entries = TrucoNotificationLog.Entries;
        if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(entries.Count == 0);
        var f = TMP_Settings.defaultFontAsset;
        for (int i = 0; i < entries.Count; i++)
            CreateRow(_content, entries[i], f);
    }

    static void CreateRow(Transform parent, TrucoNotificationLog.Entry e, TMP_FontAsset font)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 96f;
        le.preferredHeight = 104f;
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.44f, 0.30f, 0.20f, 0.95f);

        var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
        stripe.transform.SetParent(go.transform, false);
        var srt = stripe.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot = new Vector2(0f, 0.5f);
        srt.sizeDelta = new Vector2(8f, 0f);
        stripe.GetComponent<Image>().color = KindColor(e.kind);

        var chipGo = new GameObject("Chip", typeof(RectTransform), typeof(Image));
        chipGo.transform.SetParent(go.transform, false);
        var crt = chipGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(1f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-12f, -10f);
        crt.sizeDelta = new Vector2(120f, 32f);
        chipGo.GetComponent<Image>().color = KindColor(e.kind) * new Color(1f, 1f, 1f, 0.35f);
        var chipTxtGo = new GameObject("ChipText", typeof(RectTransform));
        chipTxtGo.transform.SetParent(chipGo.transform, false);
        Stretch(chipTxtGo.GetComponent<RectTransform>());
        var chip = chipTxtGo.AddComponent<TextMeshProUGUI>();
        chip.text = KindLabel(e.kind);
        chip.fontSize = 18f;
        chip.fontStyle = FontStyles.Bold;
        chip.color = Color.white;
        chip.alignment = TextAlignmentOptions.Center;

        var timeGo = new GameObject("Time", typeof(RectTransform));
        timeGo.transform.SetParent(go.transform, false);
        var trt = timeGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(0f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(18f, -10f);
        trt.sizeDelta = new Vector2(80f, 28f);
        var time = timeGo.AddComponent<TextMeshProUGUI>();
        time.text = e.time;
        time.fontSize = 20f;
        time.color = new Color(0.9f, 0.85f, 0.7f, 1f);
        if (font != null) time.font = font;

        var msgGo = new GameObject("Message", typeof(RectTransform));
        msgGo.transform.SetParent(go.transform, false);
        var mrt = msgGo.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0f, 0f);
        mrt.anchorMax = new Vector2(1f, 1f);
        mrt.offsetMin = new Vector2(18f, 12f);
        mrt.offsetMax = new Vector2(-140f, -36f);
        var msg = msgGo.AddComponent<TextMeshProUGUI>();
        msg.text = e.message;
        msg.fontSize = 24f;
        msg.fontStyle = FontStyles.Bold;
        msg.color = Color.white;
        msg.enableWordWrapping = true;
        if (font != null) msg.font = font;
    }

    static Color KindColor(TrucoNotificationLog.Kind k) => k switch
    {
        TrucoNotificationLog.Kind.Success => new Color(0.22f, 0.62f, 0.34f, 1f),
        TrucoNotificationLog.Kind.Pending => new Color(0.85f, 0.62f, 0.18f, 1f),
        TrucoNotificationLog.Kind.Warning => new Color(0.78f, 0.28f, 0.22f, 1f),
        _ => new Color(0.35f, 0.48f, 0.62f, 1f)
    };

    static string KindLabel(TrucoNotificationLog.Kind k) => k switch
    {
        TrucoNotificationLog.Kind.Success => TrucoTextosClient.LogExito,
        TrucoNotificationLog.Kind.Pending => TrucoTextosClient.LogPendiente,
        TrucoNotificationLog.Kind.Warning => TrucoTextosClient.LogAviso,
        _ => TrucoTextosClient.LogInfo
    };

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
