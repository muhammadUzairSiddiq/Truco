using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private GameObject _contentHolder;
    [SerializeField] private TextMeshProUGUI _notificationText;
    [SerializeField] private Button _closeNotificationBtn;

    #endregion

    void Awake() => ApplyTheme();

    public void DisplayNotification(string msgStr = "", Action onCloseAction = null)
    {
        _notificationText.text = msgStr;
        ApplyTheme();
        _contentHolder.SetActive(true);

        _closeNotificationBtn.onClick.RemoveAllListeners();
        _closeNotificationBtn.onClick.AddListener(() =>
        {
            onCloseAction?.Invoke();
            HideNotification();
        });
    }
    public void HideNotification()
    {
        _contentHolder.SetActive(false);
    }

    void ApplyTheme()
    {
        if (_contentHolder == null || _notificationText == null) return;
        TrucoThemeRuntime.ApplyToNotification(_contentHolder.transform, _notificationText, _closeNotificationBtn);
    }
}
