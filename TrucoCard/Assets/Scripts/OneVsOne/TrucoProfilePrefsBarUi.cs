using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Region + language controls on the profile avatar screen (bottom, above logout).</summary>
public static class TrucoProfilePrefsBarUi
{
    const int LayoutVersion = 5;
    const float GapAboveLogout = 44f;
    const int CanvasSortOrder = 32005;

    static readonly TrucoPhotonRegionSettingsSO.CloudRegion[] RegionOrder =
    {
        TrucoPhotonRegionSettingsSO.CloudRegion.SouthAmerica,
        TrucoPhotonRegionSettingsSO.CloudRegion.Asia,
        TrucoPhotonRegionSettingsSO.CloudRegion.Europe
    };

    static GameObject _root;
    static Image[] _regionButtons = new Image[3];
    static Image _engHighlight;
    static Image _spnHighlight;
    static TextMeshProUGUI _regionTitle;
    static TextMeshProUGUI _langTitle;
    static int _selectedRegionIndex;
    static int _builtLayoutVersion;

    public static void ResetForLeavingMainMenu()
    {
        TrucoLocalization.OnLanguageChanged -= OnLanguageChanged;
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _root = null;
        _engHighlight = null;
        _spnHighlight = null;
        _regionTitle = null;
        _langTitle = null;
        _regionButtons = new Image[3];
        _builtLayoutVersion = 0;
    }

    public static void EnsureOnProfilePanel(Transform mainRoot)
    {
        TrucoLocalization.EnsureSpanishDefaultUnlessUserPicked();
        TrucoLocalization.Load();
        TrucoPhotonRegionSettings.EnsureLoaded();
        if (!TrucoClientSettings.ShowProfilePrefsBar) return;

        var profile = FindDeep(mainRoot, "Profile Panel");
        if (profile == null)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Localization, "ProfilePrefsBar: Profile Panel not found.");
            return;
        }

        if (_root != null && _builtLayoutVersion == LayoutVersion && _root.transform.parent == profile)
        {
            RepositionBottomBar(profile);
            RefreshAll();
            return;
        }

        ResetForLeavingMainMenu();
        _builtLayoutVersion = LayoutVersion;
        Build(profile);
    }

    static void Build(Transform profilePanel)
    {
        _root = new GameObject("ProfilePrefsBar", typeof(RectTransform));
        _root.transform.SetParent(profilePanel, false);
        var rt = _root.GetComponent<RectTransform>();
        ApplyBarRect(rt, profilePanel);

        var canvas = _root.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasSortOrder;
        _root.AddComponent<GraphicRaycaster>();

        var bg = _root.AddComponent<Image>();
        ApplyPanelSprite(bg);
        bg.color = new Color(0.72f, 0.58f, 0.42f, 0.98f);
        bg.raycastTarget = false;

        var outer = _root.AddComponent<VerticalLayoutGroup>();
        outer.padding = new RectOffset(16, 16, 12, 12);
        outer.spacing = 8f;
        outer.childAlignment = TextAnchor.MiddleCenter;
        outer.childControlWidth = true;
        outer.childControlHeight = true;
        outer.childForceExpandWidth = true;
        outer.childForceExpandHeight = false;

        _regionTitle = CreateSectionTitle(_root.transform, TrucoLocalization.T(TrucoLocalization.Key.RegionLabel));

        var regionRow = CreateRow(_root.transform, 50f);
        var regionH = regionRow.GetComponent<HorizontalLayoutGroup>();
        regionH.spacing = 8f;
        regionH.childAlignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < RegionOrder.Length; i++)
        {
            int idx = i;
            _regionButtons[i] = CreateChipButton(regionRow.transform, RegionButtonLabel(idx), () => SelectRegion(idx));
        }

        var showLang = TrucoClientSettings.ShowLanguageToggleInMenu;
        if (showLang)
        {
            _langTitle = CreateSectionTitle(_root.transform, TrucoLocalization.T(TrucoLocalization.Key.LangSection));
            var langRow = CreateRow(_root.transform, 48f);
            var langH = langRow.GetComponent<HorizontalLayoutGroup>();
            langH.spacing = 12f;
            langH.childAlignment = TextAnchor.MiddleCenter;
            _engHighlight = CreateLangChip(langRow.transform, TrucoUiAssetLoader.UsFlag,
                TrucoLocalization.T(TrucoLocalization.Key.LangEng),
                () => TrucoLocalization.SetLanguage(TrucoLocalization.Lang.English));
            _spnHighlight = CreateLangChip(langRow.transform, TrucoUiAssetLoader.EsFlag,
                TrucoLocalization.T(TrucoLocalization.Key.LangSpn),
                () => TrucoLocalization.SetLanguage(TrucoLocalization.Lang.Spanish));
        }

        TrucoLocalization.OnLanguageChanged -= OnLanguageChanged;
        TrucoLocalization.OnLanguageChanged += OnLanguageChanged;

        SyncRegionFromSettings();
        RefreshHighlight();
        RefreshLabels();
        TrucoLocalizedUiRefresh.ApplyAll();
        _root.transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    static void ApplyBarRect(RectTransform rt, Transform profilePanel)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, ResolveBarBottomY(profilePanel));
        rt.sizeDelta = new Vector2(ResolveBarWidth(profilePanel), 0f);
        var fitter = rt.gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    static void RepositionBottomBar(Transform profilePanel)
    {
        if (_root == null) return;
        ApplyBarRect(_root.GetComponent<RectTransform>(), profilePanel);
        _root.transform.SetAsLastSibling();
    }

    static GameObject CreateRow(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        go.AddComponent<HorizontalLayoutGroup>();
        return go;
    }

    static TextMeshProUGUI CreateSectionTitle(Transform parent, string text)
    {
        var go = new GameObject("SectionTitle", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 26f;
        le.preferredHeight = 26f;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text.ToUpperInvariant();
        StyleText(t, 20f, FontStyles.Bold);
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.35f, 0.2f, 0.1f, 1f);
        return t;
    }

    static Image CreateChipButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject("Chip_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 90f;
        le.minHeight = 46f;
        le.preferredHeight = 46f;

        var img = go.AddComponent<Image>();
        ApplyPanelSprite(img);
        img.color = InactiveChip;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        SetButtonColors(btn);

        var tGo = new GameObject("Label", typeof(RectTransform));
        tGo.transform.SetParent(go.transform, false);
        Stretch(tGo.GetComponent<RectTransform>());
        var t = tGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        StyleText(t, 16f, FontStyles.Bold);
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;

        return img;
    }

    static Image CreateLangChip(Transform parent, Sprite flag, string label, Action onClick)
    {
        var go = new GameObject("Lang_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 148f;
        le.preferredWidth = 160f;
        le.minHeight = 46f;
        le.preferredHeight = 46f;

        var img = go.AddComponent<Image>();
        ApplyPanelSprite(img);
        img.color = InactiveChip;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        SetButtonColors(btn);

        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(go.transform, false);
        Stretch(row.GetComponent<RectTransform>());
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 6, 6);
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = true;

        var flagGo = new GameObject("Flag", typeof(RectTransform));
        flagGo.transform.SetParent(row.transform, false);
        var fLe = flagGo.AddComponent<LayoutElement>();
        fLe.minWidth = fLe.preferredWidth = 36f;
        fLe.minHeight = fLe.preferredHeight = 26f;
        var fImg = flagGo.AddComponent<Image>();
        fImg.sprite = flag;
        fImg.preserveAspect = true;
        fImg.raycastTarget = false;

        var tGo = new GameObject("Label", typeof(RectTransform));
        tGo.transform.SetParent(row.transform, false);
        var tLe = tGo.AddComponent<LayoutElement>();
        tLe.minWidth = 52f;
        var t = tGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        StyleText(t, 18f, FontStyles.Bold);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;

        return img;
    }

    static readonly Color InactiveChip = new Color(0.45f, 0.34f, 0.24f, 0.92f);
    static readonly Color ActiveChip = new Color(0.18f, 0.62f, 0.28f, 1f);

    static void ApplyPanelSprite(Image img)
    {
        var sprite = TrucoUiAssetLoader.Panel;
        if (sprite == null) return;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
    }

    static void SetButtonColors(Button btn)
    {
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.95f, 0.92f, 0.86f, 1f);
        cb.pressedColor = new Color(0.82f, 0.76f, 0.68f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;
    }

    static void SelectRegion(int index)
    {
        index = Mathf.Clamp(index, 0, RegionOrder.Length - 1);
        _selectedRegionIndex = index;
        TrucoPhotonRegionSettings.SetPlayerPrefsOverride(RegionOrder[index]);
        RefreshRegionHighlight();
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "Player selected region → " + TrucoPhotonRegionSettings.RegionCode);
    }

    static void RefreshAll()
    {
        RefreshRegionHighlight();
        RefreshHighlight();
        RefreshLabels();
    }

    static void RefreshLabels()
    {
        if (_regionTitle != null)
            _regionTitle.text = TrucoLocalization.T(TrucoLocalization.Key.RegionLabel).ToUpperInvariant();
        if (_langTitle != null)
            _langTitle.text = TrucoLocalization.T(TrucoLocalization.Key.LangSection).ToUpperInvariant();
        for (int i = 0; i < _regionButtons.Length; i++)
        {
            if (_regionButtons[i] == null) continue;
            var label = _regionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = RegionButtonLabel(i);
        }
    }

    static void RefreshRegionHighlight()
    {
        for (int i = 0; i < _regionButtons.Length; i++)
            TintChip(_regionButtons[i], i == _selectedRegionIndex);
    }

    static void OnLanguageChanged()
    {
        RefreshHighlight();
        RefreshLabels();
        TrucoLocalizedUiRefresh.ApplyAll();
    }

    static string RegionButtonLabel(int index)
    {
        index = Mathf.Clamp(index, 0, RegionOrder.Length - 1);
        if (TrucoLocalization.IsEnglish)
        {
            switch (RegionOrder[index])
            {
                case TrucoPhotonRegionSettingsSO.CloudRegion.Asia: return "Asia";
                case TrucoPhotonRegionSettingsSO.CloudRegion.Europe: return "Europe";
                default: return "S. America";
            }
        }
        switch (RegionOrder[index])
        {
            case TrucoPhotonRegionSettingsSO.CloudRegion.Asia: return "Asia";
            case TrucoPhotonRegionSettingsSO.CloudRegion.Europe: return "Europa";
            default: return "S. América";
        }
    }

    static void SyncRegionFromSettings()
    {
        var current = TrucoPhotonRegionSettings.CurrentRegion;
        _selectedRegionIndex = 0;
        for (int i = 0; i < RegionOrder.Length; i++)
        {
            if (RegionOrder[i] == current) { _selectedRegionIndex = i; break; }
        }
        RefreshRegionHighlight();
        RefreshLabels();
    }

    static void StyleText(TextMeshProUGUI t, float size, FontStyles style)
    {
        t.fontSize = size;
        t.fontStyle = style;
        t.enableAutoSizing = true;
        t.fontSizeMin = Mathf.Max(12f, size - 4f);
        t.fontSizeMax = size;
        t.color = new Color(0.98f, 0.97f, 0.94f, 1f);
        t.raycastTarget = false;
        ApplyTmpFont(t);
    }

    static void RefreshHighlight()
    {
        bool en = TrucoLocalization.IsEnglish;
        TintChip(_engHighlight, en);
        TintChip(_spnHighlight, !en);
    }

    static void TintChip(Image img, bool active)
    {
        if (img == null) return;
        var green = TrucoUiAssetLoader.GreenButton;
        if (active && green != null)
        {
            img.sprite = green;
            img.type = Image.Type.Sliced;
            img.color = ActiveChip;
        }
        else
        {
            ApplyPanelSprite(img);
            img.color = active ? ActiveChip : InactiveChip;
        }
    }

    static float ResolveBarBottomY(Transform profilePanel)
    {
        var logout = FindDeep(profilePanel, "logout Button") as RectTransform;
        if (logout != null && logout.anchorMin.y < 0.1f)
        {
            float h = logout.rect.height > 1f ? logout.rect.height : logout.sizeDelta.y;
            float logoutTop = logout.anchoredPosition.y + h * (1f - logout.pivot.y);
            return logoutTop + GapAboveLogout;
        }
        return 400f;
    }

    static float ResolveBarWidth(Transform profilePanel)
    {
        var pr = profilePanel as RectTransform;
        float w = pr != null ? pr.rect.width : 800f;
        if (w < 100f) w = 800f;
        return Mathf.Clamp(w - 36f, 360f, 740f);
    }

    static void ApplyTmpFont(TextMeshProUGUI t)
    {
        if (t == null) return;
        var f = TMP_Settings.defaultFontAsset;
        if (f != null) t.font = f;
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
