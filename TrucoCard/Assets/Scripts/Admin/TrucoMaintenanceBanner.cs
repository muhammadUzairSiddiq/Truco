using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-width Spanish maintenance strip on MainMenu when the kill switch is on.</summary>
public class TrucoMaintenanceBanner : MonoBehaviour
{
    static TrucoMaintenanceBanner _instance;
    TextMeshProUGUI _label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        Refresh();
    }

    public static void Refresh()
    {
        if (!TrucoMaintenanceMode.IsActive)
        {
            if (_instance != null) _instance.gameObject.SetActive(false);
            return;
        }
        Ensure().Show(TrucoMaintenanceMode.Message);
    }

    static TrucoMaintenanceBanner Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("TrucoMaintenanceBanner");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TrucoMaintenanceBanner>();
        _instance.Build();
        return _instance;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 45000;
        gameObject.AddComponent<GraphicRaycaster>();
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(transform, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 88f);
        var img = bar.GetComponent<Image>();
        img.color = new Color(0.55f, 0.12f, 0.1f, 0.96f);
        if (TrucoGameplayUiSprites.BarGold != null)
        {
            img.sprite = TrucoGameplayUiSprites.BarGold;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.85f, 0.25f, 0.18f, 1f);
        }

        var textGo = new GameObject("Msg", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bar.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(24f, 8f);
        trt.offsetMax = new Vector2(-24f, -8f);
        _label = textGo.GetComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = 28;
        _label.color = Color.white;
        _label.enableWordWrapping = true;
        if (TMP_Settings.defaultFontAsset != null) _label.font = TMP_Settings.defaultFontAsset;
        gameObject.SetActive(false);
    }

    void Show(string message)
    {
        if (_label != null) _label.text = message;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }
}
