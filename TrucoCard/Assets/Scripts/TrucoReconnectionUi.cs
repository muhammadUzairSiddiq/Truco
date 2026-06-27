using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-screen dim + bold white countdown for network reconnection (Gameplay).</summary>
public class TrucoReconnectionUi : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Image _panelBg;
    RectTransform _panelRt;

    public static TrucoReconnectionUi Ensure(Transform parent)
    {
        if (parent == null) return null;
        var existing = parent.GetComponentInChildren<TrucoReconnectionUi>(true);
        if (existing != null) return existing;

        var host = new GameObject("TrucoReconnectionUi");
        host.transform.SetParent(parent, false);
        var ui = host.AddComponent<TrucoReconnectionUi>();
        ui.Build();
        return ui;
    }

    void Build()
    {
        if (_label != null) return;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 65000;
        gameObject.AddComponent<GraphicRaycaster>();

        var dim = new GameObject("Dim");
        dim.transform.SetParent(transform, false);
        var dimRt = dim.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget = true;

        var panel = new GameObject("Panel");
        panel.transform.SetParent(dim.transform, false);
        _panelRt = panel.AddComponent<RectTransform>();
        _panelRt.anchorMin = _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRt.pivot = new Vector2(0.5f, 0.5f);
        _panelRt.sizeDelta = new Vector2(920f, 420f);
        _panelBg = panel.AddComponent<Image>();
        _panelBg.color = TrucoGameplayTimerBanner.BgReconnect;
        _panelBg.raycastTarget = false;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);
        var tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(24f, 20f);
        tr.offsetMax = new Vector2(-24f, -20f);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.color = Color.white;
        _label.enableWordWrapping = true;
        _label.richText = true;
        _label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            _label.font = TMP_Settings.defaultFontAsset;
        gameObject.SetActive(false);
    }

    public void Show(string line1, int secondsRemaining)
    {
        if (_label == null) Build();
        _label.text = TrucoGameplayTimerBanner.OverlayTituloYSegundos(line1, secondsRemaining);
        if (_panelBg != null) _panelBg.color = TrucoGameplayTimerBanner.BgReconnect;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (gameObject != null) gameObject.SetActive(false);
    }
}
