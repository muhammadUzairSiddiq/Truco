using UnityEngine;

/// <summary>Hides legacy full-screen matchmaking chrome while the 1v1 room list is the waiting UI.</summary>
public static class TrucoLobbyMatchmakingUi
{
    public static bool IsRoomListWaitingMode()
    {
        var list = Object.FindObjectOfType<OneVsOneRoomListController>(true);
        return list != null && list.IsLobbyVisible;
    }

    public static void HideWaitingOverlay()
    {
        foreach (var mm in Object.FindObjectsByType<MatchMakingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mm == null) continue;
            mm.ResetState();
            if (mm.gameObject != null) mm.gameObject.SetActive(false);
        }

        foreach (var ui in Object.FindObjectsByType<OneVsOnePhotonSessionUi>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ui?.ResetState();
    }

    public static void ShowMatchFoundOverlay()
    {
        var sessionUi = Object.FindObjectOfType<OneVsOnePhotonSessionUi>(true);
        if (sessionUi != null && sessionUi.gameObject != null)
        {
            sessionUi.gameObject.SetActive(true);
            return;
        }

        var mm = Object.FindObjectOfType<MatchMakingPanel>(true);
        if (mm != null && mm.gameObject != null)
            mm.gameObject.SetActive(true);
    }
}
