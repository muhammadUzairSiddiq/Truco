using UnityEditor;
using UnityEngine;

public static class TrucoLobbyCleanupMenu
{
    const string PlayerMenu = "Truco/Cleanup/Delete All My Active Lobby Rooms (Play Mode)";
    const string AdminMenu = "Truco/Cleanup/Admin Force-Close ALL Lobby Rooms (Play Mode)";

    [MenuItem(PlayerMenu)]
    static void PurgeMyRooms()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Truco Cleanup",
                "Enter Play Mode, log in, then run this menu again.", "OK");
            return;
        }
        _ = PurgeMyRoomsAsync();
    }

    [MenuItem(AdminMenu)]
    static void AdminForceCloseAll()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Truco Cleanup",
                "Enter Play Mode, log in as admin, then run this menu again.", "OK");
            return;
        }
        _ = AdminForceCloseAllAsync();
    }

    [MenuItem(PlayerMenu, true)]
    [MenuItem(AdminMenu, true)]
    static bool PlayModeOnly() => Application.isPlaying;

    static async System.Threading.Tasks.Task PurgeMyRoomsAsync()
    {
        var result = await OneVsOneRoomListController.TryPurgeMyLobbyMatchesAsync(force: true);
        var list = Object.FindObjectOfType<OneVsOneRoomListController>(true);
        list?.Refresh(showLoading: true, forcePurge: true);
        Debug.Log("[Truco Cleanup] Purge attempted=" + result.attempted + " ok=" + result.succeeded +
                  " failed=" + result.failed + " mineStillVisible=" + result.mineStillVisible);
    }

    static async System.Threading.Tasks.Task AdminForceCloseAllAsync()
    {
        int closed = await OneVsOneMatchLifecycle.AdminPurgeAllLobbyMatchesAsync(
            err => Debug.LogWarning("[Truco Cleanup] " + err));
        var list = Object.FindObjectOfType<OneVsOneRoomListController>(true);
        list?.Refresh(showLoading: true, forcePurge: true);
        Debug.Log("[Truco Cleanup] Admin force-closed " + closed + " lobby matches.");
    }
}
