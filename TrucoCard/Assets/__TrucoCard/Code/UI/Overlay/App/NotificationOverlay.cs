using System;
using UnityEngine;

public class NotificationOverlay : MonoBehaviour
{
    [SerializeField] private GameObject _contentHolder;
    TrucoThemeRuntime.Prompt _prompt;
    string _customTitle;
    Action _onClose;

    void Awake() => TrucoLocalization.OnLanguageChanged += ReapplyTheme;
    void OnDestroy() => TrucoLocalization.OnLanguageChanged -= ReapplyTheme;

    public void DisplayNotification(string msgStr = "", Action onCloseAction = null, string title = null)
    {
        if (_contentHolder == null) return;
        _contentHolder.SetActive(true);
        _prompt ??= TrucoThemeRuntime.BuildPrompt((RectTransform)_contentHolder.transform, TrucoCanvasOrder.Notification);
        _customTitle = title;
        _onClose = onCloseAction;
        _prompt.SetBody(msgStr);
        ReapplyTheme();
    }

    public void HideNotification()
    {
        if (_contentHolder != null) _contentHolder.SetActive(false);
        _onClose = null;
    }

    public void ReapplyTheme()
    {
        if (_prompt == null) return;
        _prompt.Title.text = _customTitle ?? TrucoLocalization.T(TrucoLocalization.Key.ImportantTitle);
        TrucoThemeRuntime.SetAction(_prompt.Primary, TrucoTextosClient.Ok, () =>
        {
            var onClose = _onClose;
            HideNotification();
            onClose?.Invoke();
        });
    }
}
