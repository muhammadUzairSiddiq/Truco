using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AppManager : SingeltonBase<AppManager>
{
    #region Setters/Private Variables

    [Header("References")]
    [SerializeField] private LoadingOverlay _loadingOverlay;
    [SerializeField] private NotificationOverlay _notificationOverlay;

    #endregion

    #region Loading Overlay

    public void DisplayLoadingUI(string loadingMsg)
    {
        _loadingOverlay.DisplayLoadingUI(loadingMsg);
    }
    public void HideLoadingUI()
    {
        _loadingOverlay.HideLoadingUI();
    }

    #endregion

    #region Notification Overlay

    public void DisplayNotification(string msgStr, Action onCloseAction = null)
    {
        HideLoadingUI();

        _notificationOverlay.DisplayNotification(msgStr, onCloseAction);
    }
    public void HideNotification()
    {
        _notificationOverlay.HideNotification();
    }

    #endregion

    #region Unity Methods

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == PostLoginSceneRouter.MainMenuSceneName) return;
        MainMenuViewCoordinator.DestroyBottomNavIfPresent();
    }
    
    private void Start()
    {
        Init();
    }

    #endregion

    private void Init()
    {
        //DisplayLodaingUI("Validating Server");
        SceneManager.LoadScene("LoginScreen");
    }
}
