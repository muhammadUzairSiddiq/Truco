using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Premium form widgets for create-room and similar runtime panels.</summary>
public static class TrucoFormUiPolish
{
    public static void ApplyWoodCard(Image bg, bool inner = false)
    {
        if (bg == null) return;
        var panel = TrucoUiAssetLoader.Panel;
        if (panel != null)
        {
            bg.sprite = panel;
            bg.type = Image.Type.Sliced;
            bg.color = inner ? TrucoUiTheme.CreateFormPanelInner : Color.white;
        }
        else
        {
            bg.color = inner ? TrucoUiTheme.CreateFormPanelInner : TrucoUiTheme.CreateFormPanelCard;
        }
    }

    public static TextMeshProUGUI CreateSectionHeader(Transform parent, TMP_FontAsset font, string text, int fontSize = 34)
    {
        var go = new GameObject("Section_" + text, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 44f;
        le.flexibleHeight = 0f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        ApplySectionHeaderStyle(tmp);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    /// <summary>Restyles runtime-built section headers (bold white caps) on existing forms.</summary>
    public static void PolishFormSectionHeaders(Transform root)
    {
        if (root == null) return;
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null) continue;
            if (!tmp.gameObject.name.StartsWith("Section_")) continue;
            ApplySectionHeaderStyle(tmp);
        }
    }

    static void ApplySectionHeaderStyle(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.color = Color.white;
    }

    public static void CreateDivider(Transform parent)
    {
        var go = new GameObject("Divider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 2f;
        le.preferredHeight = 2f;
        le.flexibleHeight = 0f;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.32f, 0.24f, 0.16f, 0.22f);
        img.raycastTarget = false;
    }

    /// <summary>Equal-width pill chip; label is centered inside the chip.</summary>
    public static Toggle CreatePillToggle(Transform parent, TMP_FontAsset font, string id, string label, bool isOn, int fontSize = 30)
    {
        var go = new GameObject("Pill_" + id, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 0f;
        le.minHeight = 76f;
        le.preferredHeight = 80f;

        var img = go.AddComponent<Image>();
        StylePillImage(img, isOn);

        var t = go.AddComponent<Toggle>();
        t.isOn = isOn;
        t.transition = Selectable.Transition.ColorTint;
        var c = t.colors;
        c.fadeDuration = 0.1f;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.96f, 0.98f, 0.95f, 1f);
        c.pressedColor = new Color(0.88f, 0.92f, 0.88f, 1f);
        c.selectedColor = Color.white;
        t.colors = c;
        t.targetGraphic = img;
        t.graphic = null;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var rt = txtGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 6f);
        rt.offsetMax = new Vector2(-10f, -6f);

        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16f;
        tmp.fontSizeMax = fontSize;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        StylePillLabel(tmp, isOn);

        t.onValueChanged.AddListener(on =>
        {
            StylePillImage(img, on);
            StylePillLabel(tmp, on);
        });

        return t;
    }

    public static (Toggle a, Toggle b) CreateSegmentedPair(
        Transform parent, TMP_FontAsset font, string id, string labelA, string labelB, bool aSelected, int fontSize = 30)
    {
        var row = new GameObject("Seg_" + id, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rel = row.AddComponent<LayoutElement>();
        rel.minHeight = 84f;
        rel.preferredHeight = 88f;
        rel.flexibleHeight = 0f;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 0, 0);

        var a = CreatePillToggle(h.transform, font, id + "A", labelA, aSelected, fontSize);
        var b = CreatePillToggle(h.transform, font, id + "B", labelB, !aSelected, fontSize);
        WireExclusivePair(a, b);
        return (a, b);
    }

    public static (Toggle t5, Toggle t10, Toggle t15) CreateFeePillRow(Transform parent, TMP_FontAsset font, int fontSize = 34)
        => CreateFeeScrollSelector(parent, font, OneVsOneCreateRoomPanel.EntryFeeOptions, fontSize, out _);

    /// <summary>Scrollable grid of entry-fee pills (white bold label when selected).</summary>
    public static (Toggle t5, Toggle t10, Toggle t15) CreateFeeScrollSelector(
        Transform parent, TMP_FontAsset font, int[] amounts, int fontSize, out List<Toggle> allToggles)
    {
        allToggles = new List<Toggle>();
        var scroll = WrapInScrollView(parent, out RectTransform content);
        var scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
        scrollLe.minHeight = 160f;
        scrollLe.preferredHeight = 180f;
        scrollLe.flexibleHeight = 0f;

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(118f, 72f);
        grid.spacing = new Vector2(12f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;

        Toggle first = null, second = null, third = null;
        for (int i = 0; i < amounts.Length; i++)
        {
            bool on = amounts[i] == 5;
            var t = CreatePillToggle(content, font, amounts[i].ToString(), amounts[i].ToString(), on, fontSize);
            t.name = "Fee_" + amounts[i];
            allToggles.Add(t);
            if (i == 0) first = t;
            else if (i == 1) second = t;
            else if (i == 2) third = t;
        }
        WireExclusiveGroup(allToggles);
        return (first, second, third);
    }

    public static void WireExclusiveGroup(IList<Toggle> toggles)
    {
        if (toggles == null) return;
        foreach (var t in toggles)
        {
            if (t == null) continue;
            t.onValueChanged.AddListener(on =>
            {
                if (!on) return;
                foreach (var other in toggles)
                {
                    if (other == null || other == t) continue;
                    other.SetIsOnWithoutNotify(false);
                    RefreshPillVisual(other, false);
                }
                RefreshPillVisual(t, true);
            });
        }
    }

    public static void RefreshPillVisual(Toggle t, bool on)
    {
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) StylePillImage(img, on);
        var tmp = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) StylePillLabel(tmp, on);
    }

    public static void WireExclusivePair(Toggle a, Toggle b)
    {
        if (a == null || b == null) return;
        a.onValueChanged.AddListener(on =>
        {
            if (!on) { if (!b.isOn) a.SetIsOnWithoutNotify(true); return; }
            b.SetIsOnWithoutNotify(false);
            RefreshPillVisual(b, false);
            RefreshPillVisual(a, true);
        });
        b.onValueChanged.AddListener(on =>
        {
            if (!on) { if (!a.isOn) b.SetIsOnWithoutNotify(true); return; }
            a.SetIsOnWithoutNotify(false);
            RefreshPillVisual(a, false);
            RefreshPillVisual(b, true);
        });
    }

    public static void WireExclusiveTriple(Toggle a, Toggle b, Toggle c)
    {
        void Select(Toggle winner)
        {
            if (a != null && a != winner && a.isOn) { a.SetIsOnWithoutNotify(false); RefreshPillVisual(a, false); }
            if (b != null && b != winner && b.isOn) { b.SetIsOnWithoutNotify(false); RefreshPillVisual(b, false); }
            if (c != null && c != winner && c.isOn) { c.SetIsOnWithoutNotify(false); RefreshPillVisual(c, false); }
            if (winner != null) { winner.SetIsOnWithoutNotify(true); RefreshPillVisual(winner, true); }
        }
        if (a != null) a.onValueChanged.AddListener(on => { if (on) Select(a); else if (!b.isOn && !c.isOn) Select(a); });
        if (b != null) b.onValueChanged.AddListener(on => { if (on) Select(b); else if (!a.isOn && !c.isOn) Select(b); });
        if (c != null) c.onValueChanged.AddListener(on => { if (on) Select(c); else if (!a.isOn && !b.isOn) Select(c); });
    }

    public static ScrollRect WrapInScrollView(Transform cardRoot, out RectTransform content)
    {
        var scrollGo = new GameObject("FormScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(cardRoot, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(8f, 8f);
        srt.offsetMax = new Vector2(-8f, -8f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        var mask = viewport.AddComponent<RectMask2D>();

        content = new GameObject("FormInner", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport.transform, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vrt;
        scroll.content = content;
        return scroll;
    }

    static void StylePillImage(Image img, bool on)
    {
        var green = TrucoUiAssetLoader.GreenButton;
        if (green != null)
        {
            img.sprite = green;
            img.type = Image.Type.Sliced;
            img.color = on ? Color.white : new Color(0.78f, 0.72f, 0.64f, 1f);
        }
        else
        {
            img.sprite = null;
            img.color = on ? TrucoUiTheme.CreateFormButtonPrimary : new Color(0.62f, 0.52f, 0.40f, 1f);
        }
    }

    static void StylePillLabel(TextMeshProUGUI tmp, bool on)
    {
        if (tmp == null) return;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = on ? Color.white : TrucoUiTheme.CreateFormSubLabelCream;
    }
}
