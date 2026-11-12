using UnityEngine;

public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private GameObject _contentHolder;
    TrucoThemeRuntime.Prompt _prompt;

    void Awake() => TrucoLocalization.OnLanguageChanged += ReapplyTheme;
    void OnDestroy() => TrucoLocalization.OnLanguageChanged -= ReapplyTheme;

    public void DisplayLoadingUI(string msgStr = "")
    {
        if (_contentHolder == null) return;
        _contentHolder.SetActive(true);
        _prompt ??= TrucoThemeRuntime.BuildPrompt((RectTransform)_contentHolder.transform, TrucoCanvasOrder.Loading, loading: true);
        ReapplyTheme();
        _prompt.SetBody(msgStr);
    }

    public void HideLoadingUI()
    {
        if (_contentHolder != null) _contentHolder.SetActive(false);
    }

    public void ReapplyTheme()
    {
        if (_prompt != null) _prompt.Title.text = TrucoLocalization.T(TrucoLocalization.Key.Espere);
    }
}
