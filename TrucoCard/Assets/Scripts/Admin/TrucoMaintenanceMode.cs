using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Emergency kill switch / maintenance. Client-side flags plus empty backend slots
/// (<see cref="ApiConfig.AdminSetMaintenance"/> / <see cref="ApiConfig.GetSystemStatus"/>).
/// </summary>
public static class TrucoMaintenanceMode
{
    const string PrefsKey = "truco_maintenance_v1";

    [Serializable]
    public class State
    {
        public bool active;
        public bool blockGame = true;
        public bool blockTables = true;
        public bool blockTournaments = true;
        public bool blockTransactions = true;
        public string message = "El juego está en mantenimiento. Volvé más tarde.";
        public string updatedAt;
        public string updatedBy;
    }

    static State _cached;
    static bool _refreshing;

    public static State Current
    {
        get
        {
            if (_cached == null) _cached = LoadLocal();
            return _cached;
        }
    }

    public static bool IsActive => Current != null && Current.active;
    public static bool BlocksTables => IsActive && (Current.blockGame || Current.blockTables);
    public static bool BlocksTournaments => IsActive && (Current.blockGame || Current.blockTournaments);
    public static bool BlocksTransactions => IsActive && (Current.blockGame || Current.blockTransactions);
    public static string Message => string.IsNullOrWhiteSpace(Current.message)
        ? "El juego está en mantenimiento. Volvé más tarde."
        : Current.message;

    public static void ApplyLocal(State state, bool persist = true)
    {
        _cached = state ?? new State();
        if (persist)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_cached));
            PlayerPrefs.Save();
        }
        TrucoMaintenanceBanner.Refresh();
    }

    public static async Task RefreshFromServerAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            string raw = await HttpApiClient.GetAsync(ApiConfig.GetSystemStatus);
            if (string.IsNullOrEmpty(raw)) return;
            var parsed = TryParse(raw);
            if (parsed != null) ApplyLocal(parsed, persist: true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Maintenance] GET status: " + ex.Message);
        }
        finally { _refreshing = false; }
    }

    public static async Task<bool> PushToServerAsync(State state)
    {
        if (state == null) return false;
        try
        {
            string json = JsonUtility.ToJson(state);
            string response = await HttpApiClient.PostAsync(ApiConfig.AdminSetMaintenance, json);
            if (!TrucoServerAcknowledgement.Accepted(response)) return false;
            var confirmed = TryParse(response);
            if (confirmed == null) return false;
            ApplyLocal(confirmed, persist: true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Maintenance] POST: " + ex.Message);
            return false;
        }
    }

    static State LoadLocal()
    {
        string raw = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(raw)) return new State { active = false };
        var s = TryParse(raw);
        return s ?? new State { active = false };
    }

    public static State TryParse(string raw)
    {
        try
        {
            var root = JObject.Parse(raw);
            if (root["ok"]?.Type == JTokenType.Boolean && !(bool)root["ok"]) return null;
            if (root["success"]?.Type == JTokenType.Boolean && !(bool)root["success"]) return null;
            var payload = root["maintenance"] as JObject ?? root["data"] as JObject ?? root;
            payload = payload["maintenance"] as JObject ?? payload;
            var active = payload["active"];
            var enabled = payload["enabled"];
            if (active == null && enabled == null || active != null && active.Type != JTokenType.Boolean
                || enabled != null && enabled.Type != JTokenType.Boolean
                || active != null && enabled != null && (bool)active != (bool)enabled) return null;
            var state = payload.ToObject<State>();
            state.active = (bool)(active ?? enabled);
            return state;
        }
        catch { }
        return null;
    }

}
