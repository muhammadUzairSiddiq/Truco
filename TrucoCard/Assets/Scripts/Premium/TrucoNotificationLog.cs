using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent in-app event log for the notifications tab.</summary>
public static class TrucoNotificationLog
{
    public enum Kind { Info, Success, Pending, Warning }

    [Serializable]
    public struct Entry
    {
        public string time;
        public string message;
        public Kind kind;
    }

    [Serializable]
    class Store
    {
        public List<Entry> items = new List<Entry>();
    }

    const string PrefKey = "truco_notification_log_v1";
    const int MaxEntries = 80;
    static readonly List<Entry> _entries = new List<Entry>(MaxEntries);
    static bool _loaded;

    public static event Action OnChanged;

    public static IReadOnlyList<Entry> Entries
    {
        get
        {
            EnsureLoaded();
            return _entries;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad() => EnsureLoaded();

    public static void Info(string message) => Add(message, Kind.Info);
    public static void Success(string message) => Add(message, Kind.Success);
    public static void Pending(string message) => Add(message, Kind.Pending);
    public static void Warning(string message) => Add(message, Kind.Warning);

    public static void Add(string message, Kind kind)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        EnsureLoaded();
        _entries.Insert(0, new Entry
        {
            time = DateTime.Now.ToString("HH:mm"),
            message = message.Trim(),
            kind = kind
        });
        while (_entries.Count > MaxEntries)
            _entries.RemoveAt(_entries.Count - 1);
        Save();
        OnChanged?.Invoke();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _entries.Clear();
        if (!PlayerPrefs.HasKey(PrefKey)) return;
        try
        {
            var json = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var store = JsonUtility.FromJson<Store>(json);
            if (store?.items != null) _entries.AddRange(store.items);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TrucoNotificationLog] load failed: " + ex.Message);
        }
    }

    static void Save()
    {
        var store = new Store { items = new List<Entry>(_entries) };
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(store));
        PlayerPrefs.Save();
    }
}
