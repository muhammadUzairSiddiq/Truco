using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns icon/label presentation without changing the tab's clickable rectangle.</summary>
[DisallowMultipleComponent]
public sealed class TrucoNavigationTab : MonoBehaviour
{
    Image _icon;
    TextMeshProUGUI _label;
    bool _selected;
    bool _configured;
    const float IconSize = 86f;
    const float SelectedScale = 1.12f;
    const float RestScale = .9f;

    public void Configure(string caption)
    {
        // The original scene uses Icon, Icon (1), Icon (2), Icon (3).
        foreach (Transform child in transform)
            if (child.name == "Icon" || child.name.StartsWith("Icon ("))
            { _icon = child.GetComponent<Image>(); break; }
        if (_icon != null)
        {
            var rt = _icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .66f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.one * IconSize;
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
        }
        var existing = transform.Find("NavigationCaption");
        if (existing == null)
        {
            var go = new GameObject("NavigationCaption", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMeshProUGUI>();
            _label.font = TMP_Settings.defaultFontAsset;
        }
        else _label = existing.GetComponent<TextMeshProUGUI>();

        // Remove captions created by the previous generic layout, retaining the original icon.
        foreach (var other in GetComponentsInChildren<TextMeshProUGUI>(true))
            if (other != _label) other.gameObject.SetActive(false);
        var labelRect = _label.rectTransform;
        labelRect.anchorMin = new Vector2(0, .035f);
        labelRect.anchorMax = new Vector2(1, .28f);
        labelRect.offsetMin = new Vector2(4, 0); labelRect.offsetMax = new Vector2(-4, 0);
        _label.text = caption;
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.fontSize = 26;
        _label.enableAutoSizing = true; _label.fontSizeMin = 21; _label.fontSizeMax = 26;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.characterSpacing = .8f;
        _label.raycastTarget = false;
        var background = GetComponent<Image>();
        if (background != null)
        {
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            GetComponent<Button>().targetGraphic = background;
        }
        _configured = true;
        SetSelected(_selected, true);
    }

    public void SetSelected(bool selected, bool immediate = false)
    {
        _selected = selected;
        if (!_configured) return;
        var button = GetComponent<Button>();
        var colors = button.colors;
        var baseColor = selected ? TrucoUiTheme.NavSelected : TrucoUiTheme.NavUnselected;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, .10f);
        colors.selectedColor = baseColor;
        colors.pressedColor = Color.Lerp(baseColor, Color.black, .14f);
        colors.disabledColor = new Color(.4f, .3f, .2f, .35f);
        colors.colorMultiplier = 1; colors.fadeDuration = .16f;
        button.colors = colors;
        if (_icon != null) _icon.color = selected ? Color.white : TrucoUiTheme.NavIconOffTab;
        _label.color = selected ? new Color32(66, 38, 14, 255) : new Color32(247, 224, 176, 255);
        if (_icon != null && (immediate || !Application.isPlaying))
            _icon.transform.localScale = Vector3.one * (selected ? SelectedScale : RestScale);
    }

    void LateUpdate()
    {
        if (_icon == null) return;
        var target = Vector3.one * (_selected ? SelectedScale : RestScale);
        _icon.transform.localScale = Vector3.Lerp(_icon.transform.localScale, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
    }
}
