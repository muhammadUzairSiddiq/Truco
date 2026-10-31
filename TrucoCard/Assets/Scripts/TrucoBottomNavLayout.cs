using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fits the original four navigation icons to a plain brown tab strip.</summary>
public sealed class TrucoBottomNavLayout : MonoBehaviour
{
    int _width, _height;
    Rect _safe;
    void OnEnable() => Refresh();
    float _canvasScale;
    void Update()
    {
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        if (_width != Screen.width || _height != Screen.height || _safe != Screen.safeArea
            || !Mathf.Approximately(_canvasScale, scale)) Refresh();
    }
    public void Refresh()
    {
        _width = Screen.width; _height = Screen.height; _safe = Screen.safeArea;
        var rt = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? Mathf.Max(.01f, canvas.rootCanvas.scaleFactor) : 1f;
        _canvasScale = scale;
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0);
        float leftInset = Screen.safeArea.xMin / scale;
        float rightInset = (Screen.width - Screen.safeArea.xMax) / scale;
        rt.sizeDelta = new Vector2(-32 - leftInset - rightInset, 192);
        rt.anchoredPosition = new Vector2((leftInset - rightInset) * .5f, 12 + Screen.safeArea.yMin / scale);
        rt.localScale = Vector3.one;
        var image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = TrucoMenuTheme.Brown;
        }
        foreach (var layout in GetComponents<LayoutGroup>()) layout.enabled = false;
        var buttons = GetComponentsInChildren<Button>(true);
        string[] captions = { "INICIO", "TORNEOS", "PERFIL", "AVISOS" };
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i]; var br = (RectTransform)button.transform;
            int index = button.name.Contains("Tournament") ? 1 : button.name.Contains("Profile") ? 2
                : button.name.Contains("Notification") ? 3 : 0;
            br.SetParent(transform, false);
            // Equal slots, with room for the wooden outer frame at either side.
            br.anchorMin = new Vector2(.035f + .93f * index / 4f, 0);
            br.anchorMax = new Vector2(.035f + .93f * (index + 1) / 4f, 1);
            br.offsetMin = new Vector2(5, 25); br.offsetMax = new Vector2(-5, -24);
            br.pivot = new Vector2(.5f, .5f);
            var tab = button.GetComponent<TrucoNavigationTab>();
            if (tab == null) tab = button.gameObject.AddComponent<TrucoNavigationTab>();
            tab.Configure(captions[index]);
        }
    }
}
