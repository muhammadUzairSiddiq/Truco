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

    public void DisplayNotification(string msgStr = "", Action onCloseAction = null)
    {
        _notificationText.text = msgStr;
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
}
