using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ENG / SPN toggle with flag icons on the main menu.</summary>
public static class TrucoLanguageToggleUi
{
    static GameObject _root;
    static Image _engHighlight;
    static Image _spnHighlight;

    public static void ResetForLeavingMainMenu()
    {
        TrucoLocalization.OnLanguageChanged -= OnLanguageChanged;
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _root = null;
        _engHighlight = null;
        _spnHighlight = null;
    }

    public static void EnsureOnMainMenu(Transform mainRoot)
    {
        ResetForLeavingMainMenu();
        TrucoLocalization.ForceSpanish();
    }

    static void Build(Transform menuPanel)
    {
        if (_root != null) UnityEngine.Object.Destroy(_root);

        _root = new GameObject("LanguageToggle", typeof(RectTransform));
        _root.transform.SetParent(menuPanel, false);
        var rt = _root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);
        rt.sizeDelta = new Vector2(220f, 52f);

        var h = _root.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        _engHighlight = CreateLangButton(h.transform, TrucoUiAssetLoader.UsFlag, TrucoLocalization.T(TrucoLocalization.Key.LangEng),
            () => TrucoLocalization.SetLanguage(TrucoLocalization.Lang.English));
        _spnHighlight = CreateLangButton(h.transform, TrucoUiAssetLoader.EsFlag, TrucoLocalization.T(TrucoLocalization.Key.LangSpn),
            () => TrucoLocalization.SetLanguage(TrucoLocalization.Lang.Spanish));

        TrucoLocalization.OnLanguageChanged -= OnLanguageChanged;
        TrucoLocalization.OnLanguageChanged += OnLanguageChanged;
    }

    static void OnLanguageChanged()
    {
        RefreshHighlight();
        TrucoLocalizedUiRefresh.ApplyAll();
    }

    static void RefreshHighlight()
    {
        bool en = TrucoLocalization.IsEnglish;
        Tint(_engHighlight, en);
        Tint(_spnHighlight, !en);
    }

    static void Tint(Image highlight, bool active)
    {
        if (highlight == null) return;
        highlight.color = active
            ? new Color(0.25f, 0.55f, 0.32f, 0.95f)
            : new Color(0.42f, 0.32f, 0.22f, 0.55f);
    }

    static Image CreateLangButton(Transform parent, Sprite flag, string label, Action onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 96f;
        le.preferredWidth = 104f;
        le.minHeight = 44f;
        le.preferredHeight = 44f;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.42f, 0.32f, 0.22f, 0.55f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.95f, 0.92f, 0.88f, 1f);
        cb.pressedColor = new Color(0.85f, 0.8f, 0.74f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(go.transform, false);
        Stretch(row.GetComponent<RectTransform>());
        var hr = row.AddComponent<HorizontalLayoutGroup>();
        hr.padding = new RectOffset(8, 8, 4, 4);
        hr.spacing = 6f;
        hr.childAlignment = TextAnchor.MiddleCenter;
        hr.childControlWidth = false;
        hr.childControlHeight = true;

        var flagGo = new GameObject("Flag", typeof(RectTransform));
        flagGo.transform.SetParent(row.transform, false);
        var fLe = flagGo.AddComponent<LayoutElement>();
        fLe.minWidth = fLe.preferredWidth = 34f;
        fLe.minHeight = fLe.preferredHeight = 24f;
        var fImg = flagGo.AddComponent<Image>();
        fImg.sprite = flag;
        fImg.preserveAspect = true;
        fImg.raycastTarget = false;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(row.transform, false);
        var t = txtGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 22f;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;

        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
