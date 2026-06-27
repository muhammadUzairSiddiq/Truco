using TMPro;
using UnityEngine;

/// <summary>Pushes <see cref="ApiController.GetSessionUser"/> wallet balance into all HUD coin labels.</summary>
public static class TrucoWalletHudRefresh
{
    public static void Apply()
    {
        int balance = ApiController.GetSessionUser?.Data?.wallet?.balance ?? 0;
        foreach (var cg in Object.FindObjectsByType<CoinGetter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            cg.RefreshNow();
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (t.gameObject.name == "coins" || t.transform.name == "coins")
                t.text = balance.ToString();
        }
    }
}
