using TMPro;
using UnityEngine;

public class LoadingOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private GameObject _contentHolder;
    [SerializeField] private TextMeshProUGUI _loadingText;

    #endregion

    public void DisplayLoadingUI(string msgStr = "")
    {
        _loadingText.text = msgStr;

        _contentHolder.SetActive(true);
    }
    public void HideLoadingUI() 
    {
        _contentHolder.SetActive(false);
    }
}
