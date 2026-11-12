using System;
using UnityEngine;
using UnityEngine.UI;

public static class TrucoConfirmDialog
{
    const string RootName = "TrucoConfirmDialogCanvas";
    static GameObject _root;
    static TrucoThemeRuntime.Prompt _prompt;

    public static void Show(string title, string message, string noLabel, string yesLabel, Action onYes, Action onNo = null)
    {
        if (_root == null)
        {
            _root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0;
            _prompt = TrucoThemeRuntime.BuildPrompt((RectTransform)_root.transform, TrucoCanvasOrder.Confirm, twoActions: true);
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        _root.SetActive(true);
        _prompt.Title.text = title;
        _prompt.SetBody(message);
        TrucoThemeRuntime.SetAction(_prompt.Secondary, noLabel, () => { Hide(); onNo?.Invoke(); });
        TrucoThemeRuntime.SetAction(_prompt.Primary, yesLabel, () => { Hide(); onYes?.Invoke(); });
    }

    public static void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    public static bool IsVisible() => _root != null && _root.activeInHierarchy;
}
