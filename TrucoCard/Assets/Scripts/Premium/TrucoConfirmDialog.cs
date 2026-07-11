using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Wood-panel confirm modal (green NO / red SÍ) matching in-game polish.</summary>
public static class TrucoConfirmDialog
{
    const string RootName = "TrucoConfirmDialog";
    static GameObject _root;
    static TextMeshProUGUI _title;
    static TextMeshProUGUI _body;
    static Button _btnNo;
    static Button _btnYes;

    public static void Show(string title, string message, string noLabel, string yesLabel, Action onYes, Action onNo = null)
    {
        EnsureBuilt();
        ReparentOnTop();
        if (_root == null)
        {
            Debug.LogWarning("[TrucoConfirmDialog] Could not build dialog.");
            AppManager.Instance?.DisplayNotification(message);
            return;
        }
        _title.text = title;
        _body.text = message;
        SetButtonLabel(_btnNo, noLabel);
        SetButtonLabel(_btnYes, yesLabel);
        _btnNo.onClick.RemoveAllListeners();
        _btnYes.onClick.RemoveAllListeners();
        _btnNo.onClick.AddListener(() =>
        {
            Hide();
            onNo?.Invoke();
        });
        _btnYes.onClick.AddListener(() =>
        {
            Hide();
            onYes?.Invoke();
        });
        _root.SetActive(true);
        var cv = _root.GetComponent<Canvas>();
        if (cv != null)
        {
            cv.overrideSorting = true;
            cv.sortingOrder = 40000;
        }
        _root.transform.SetAsLastSibling();
    }

    public static void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    public static bool IsVisible() => _root != null && _root.activeInHierarchy;

    static void ReparentOnTop()
    {
        if (_root == null) return;
        var overlayGo = GameObject.Find("TrucoConfirmDialogCanvas");
        if (overlayGo == null) return;
        var parent = overlayGo.transform;
        if (_root.transform.parent != parent)
        {
            _root.transform.SetParent(parent, false);
            var rt = _root.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }

    static void EnsureBuilt()
    {
        if (_root != null) return;
        Transform parent = null;
        var overlayGo = GameObject.Find("TrucoConfirmDialogCanvas");
        if (overlayGo == null)
        {
            overlayGo = new GameObject("TrucoConfirmDialogCanvas", typeof(RectTransform));
            var cv = overlayGo.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.overrideSorting = true;
            cv.sortingOrder = 40000;
            overlayGo.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(overlayGo);
        }
        parent = overlayGo.transform;
        if (parent == null) return;

        _root = new GameObject(RootName, typeof(RectTransform));
        _root.transform.SetParent(parent, false);
        var rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        var rootCv = _root.AddComponent<Canvas>();
        rootCv.overrideSorting = true;
        rootCv.sortingOrder = 40000;
        _root.AddComponent<GraphicRaycaster>();
        var scrim = _root.AddComponent<Image>();
        scrim.color = TrucoUiTheme.OverlayScrim;
        scrim.raycastTarget = true;

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(_root.transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(820f, 460f);
        var cardImg = card.GetComponent<Image>();
        var panel = TrucoUiAssetLoader.Panel;
        if (panel != null)
        {
            cardImg.sprite = panel;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = Color.white;
        }
        else cardImg.color = new Color(0.14f, 0.38f, 0.22f, 0.96f);

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(card.transform, false);
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.06f, 0.72f);
        trt.anchorMax = new Vector2(0.94f, 0.94f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        _title = titleGo.AddComponent<TextMeshProUGUI>();
        StyleText(_title, 34f);

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(card.transform, false);
        var brt = bodyGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.08f, 0.34f);
        brt.anchorMax = new Vector2(0.92f, 0.7f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        _body = bodyGo.AddComponent<TextMeshProUGUI>();
        StyleText(_body, 28f);
        _body.fontStyle = FontStyles.Normal;

        var row = new GameObject("Buttons", typeof(RectTransform));
        row.transform.SetParent(card.transform, false);
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.06f, 0.08f);
        rrt.anchorMax = new Vector2(0.94f, 0.28f);
        rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        _btnNo = CreateDialogButton(row.transform, "BtnNo", true);
        _btnYes = CreateDialogButton(row.transform, "BtnYes", false);

        _root.SetActive(false);
    }

    static Button CreateDialogButton(Transform parent, string name, bool green)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 88f;
        le.flexibleWidth = 1f;
        var img = go.AddComponent<Image>();
        if (green)
        {
            var g = TrucoUiAssetLoader.GreenButton;
            if (g != null) { img.sprite = g; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = TrucoUiTheme.CreateFormButtonPrimary;
        }
        else
        {
            img.sprite = null;
            img.color = new Color(0.78f, 0.22f, 0.18f, 1f);
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cap = new GameObject("Label", typeof(RectTransform));
        cap.transform.SetParent(go.transform, false);
        Stretch(cap.GetComponent<RectTransform>());
        var tmp = cap.AddComponent<TextMeshProUGUI>();
        StyleText(tmp, 30f);
        return btn;
    }

    static void SetButtonLabel(Button btn, string text)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) tmp.text = text;
    }

    static void StyleText(TextMeshProUGUI tmp, float size)
    {
        if (tmp == null) return;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = size;
        tmp.raycastTarget = false;
        var f = TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 8f);
        rt.offsetMax = new Vector2(-12f, -8f);
    }

    static Transform FindTopOverlayRoot()
    {
        Canvas best = null;
        var bestOrder = int.MinValue;
        var all = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (c.sortingOrder < bestOrder) continue;
            bestOrder = c.sortingOrder;
            best = c;
        }
        return best != null ? best.transform : null;
    }
}
