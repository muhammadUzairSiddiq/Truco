using TMPro;
using UnityEngine;

public class LoadingOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private GameObject _contentHolder;
    [SerializeField] private TextMeshProUGUI _loadingText;

    #endregion

    void Awake()
    {
        ApplyTheme();
    }

    public void DisplayLoadingUI(string msgStr = "")
    {
        if (_loadingText != null) _loadingText.text = msgStr;
        ApplyTheme();
        if (_contentHolder != null) _contentHolder.SetActive(true);
    }

    public void HideLoadingUI()
    {
        if (_contentHolder != null) _contentHolder.SetActive(false);
    }

    void ApplyTheme()
    {
        if (_contentHolder == null || _loadingText == null) return;
        TrucoThemeRuntime.ApplyToLoading(_contentHolder.transform, _loadingText);
    }
}
