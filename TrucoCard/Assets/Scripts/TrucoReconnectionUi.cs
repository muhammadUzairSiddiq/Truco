using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-screen dim + centered white text for network reconnection (Gameplay).</summary>
public class TrucoReconnectionUi : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;
    RectTransform _rootRt;

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
        canvas.sortingOrder = 60000;
        gameObject.AddComponent<GraphicRaycaster>();

        var dim = new GameObject("Dim");
        dim.transform.SetParent(transform, false);
        _rootRt = dim.AddComponent<RectTransform>();
        _rootRt.anchorMin = Vector2.zero;
        _rootRt.anchorMax = Vector2.one;
        _rootRt.offsetMin = Vector2.zero;
        _rootRt.offsetMax = Vector2.zero;
        var img = dim.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.72f);
        img.raycastTarget = true;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(dim.transform, false);
        var tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 0.5f);
        tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = Vector2.zero;
        tr.sizeDelta = new Vector2(1080f, 400f);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = 36;
        _label.color = Color.white;
        _label.enableWordWrapping = true;
        if (TMP_Settings.defaultFontAsset != null)
            _label.font = TMP_Settings.defaultFontAsset;
        gameObject.SetActive(false);
    }

    public void Show(string line1, int secondsRemaining)
    {
        if (_label == null) Build();
        string t = secondsRemaining >= 0 ? $"\n{secondsRemaining}" : string.Empty;
        _label.text = line1 + t;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject != null) gameObject.SetActive(false);
    }
}
