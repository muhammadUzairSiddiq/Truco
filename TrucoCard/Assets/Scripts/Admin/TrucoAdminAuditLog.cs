using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Local admin audit trail (Unity slot). Backend can replace this via empty API routes.</summary>
public static class TrucoAdminAuditLog
{
    const string PrefsKey = "truco_admin_audit_v1";
    const int MaxEntries = 80;

    [Serializable]
    public class Entry
    {
        public string at;
        public string admin;
        public string action;
        public string detail;
    }

    [Serializable]
    class Wire
    {
        public Entry[] items;
    }

    public static void Record(string action, string detail = null)
    {
        var list = Load();
        list.Insert(0, new Entry
        {
            at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
            admin = ApiController.GetSessionUser?.Data?.username ?? "admin",
            action = action ?? "",
            detail = detail ?? ""
        });
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        Save(list);
        Debug.Log("[AdminAudit] " + action + " | " + detail);
    }

    public static List<Entry> Load()
    {
        var result = new List<Entry>();
        string raw = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(raw)) return result;
        try
        {
            var wire = JsonUtility.FromJson<Wire>(raw);
            if (wire?.items != null) result.AddRange(wire.items);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AdminAudit] load: " + ex.Message);
        }
        return result;
    }

    static void Save(List<Entry> list)
    {
        var wire = new Wire { items = list != null ? list.ToArray() : Array.Empty<Entry>() };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(wire));
        PlayerPrefs.Save();
    }
}
