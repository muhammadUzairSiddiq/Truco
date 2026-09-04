using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Durable ledger of 1v1 rooms whose entry Trucoins are still owed back to this player.
/// An entry is written the moment the backend deducts the fee for a room that has not started, and is
/// only dropped once the match actually began or a refund was confirmed. It therefore survives the app
/// being killed mid-cancel, a failed reconnect, or the backend deleting the abandoned row on its own —
/// every launch and every lobby refresh retries whatever is still listed here.
/// </summary>
public static class TrucoPendingRefundStore
{
    const string Key = "truco_pending_entry_refunds";
    const char EntrySeparator = '|';
    const char FieldSeparator = '#';

    public struct PendingRefund
    {
        public string matchId;
        public int entryFee;
    }

    public static void Remember(string matchId, int entryFee)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        var all = All();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].matchId != matchId) continue;
            if (entryFee <= 0 || all[i].entryFee == entryFee) return;
            all[i] = new PendingRefund { matchId = matchId, entryFee = entryFee };
            Save(all);
            return;
        }
        all.Add(new PendingRefund { matchId = matchId, entryFee = Mathf.Max(0, entryFee) });
        Save(all);
        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
            "PendingRefund remember match=" + matchId + " fee=" + entryFee);
    }

    public static void Forget(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        var all = All();
        int removed = all.RemoveAll(e => e.matchId == matchId);
        if (removed <= 0) return;
        Save(all);
        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby, "PendingRefund settled match=" + matchId);
    }

    /// <summary>True while this room's entry fee has not been confirmed back in the wallet.</summary>
    public static bool IsPending(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        var all = All();
        for (int i = 0; i < all.Count; i++)
            if (all[i].matchId == matchId) return true;
        return false;
    }

    public static int GetFee(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return 0;
        var all = All();
        for (int i = 0; i < all.Count; i++)
            if (all[i].matchId == matchId) return all[i].entryFee;
        return 0;
    }

    public static List<PendingRefund> All()
    {
        var result = new List<PendingRefund>();
        string raw = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (string entry in raw.Split(EntrySeparator))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            string[] parts = entry.Split(FieldSeparator);
            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0])) continue;
            int fee = 0;
            if (parts.Length > 1) int.TryParse(parts[1], out fee);
            result.Add(new PendingRefund { matchId = parts[0], entryFee = fee });
        }
        return result;
    }

    public static void Clear()
    {
        if (!PlayerPrefs.HasKey(Key)) return;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    static void Save(List<PendingRefund> entries)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.IsNullOrEmpty(entries[i].matchId)) continue;
            if (sb.Length > 0) sb.Append(EntrySeparator);
            sb.Append(entries[i].matchId).Append(FieldSeparator).Append(entries[i].entryFee);
        }
        PlayerPrefs.SetString(Key, sb.ToString());
        PlayerPrefs.Save();
    }
}
