using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Tagged client logs — Console + on-device file so APK issues are diagnosable after build.
/// File: Application.persistentDataPath/truco_debug.log (also mirrored to Reporter / logcat).
/// </summary>
public static class TrucoDebugLog
{
    public enum Category
    {
        OneVsOne,
        Api,
        Photon,
        Lobby,
        Settings,
        Localization,
        Gameplay,
        MainMenu,
        Rules
    }

    const string FileName = "truco_debug.log";
    const long MaxFileBytes = 512 * 1024;
    static readonly object _fileLock = new object();
    static string _filePath;
    static bool _bootLogged;

    public static string LogFilePath
    {
        get
        {
            EnsurePath();
            return _filePath;
        }
    }

    public static void Log(Category category, string message) =>
        Write(LogType.Log, category, message);

    public static void Warn(Category category, string message) =>
        Write(LogType.Warning, category, message);

    public static void Error(Category category, string message) =>
        Write(LogType.Error, category, message);

    /// <summary>Always written (even if debugLogsEnabled is off) — use for join/create/game-critical paths.</summary>
    public static void Always(Category category, string message) =>
        Write(LogType.Log, category, message, force: true);

    /// <summary>Green-tick rules/gameplay scenario (always on). Prefer <see cref="TrucoRulesScenarioLog"/> for score snapshots.</summary>
    public static void Tick(Category category, string scenario) =>
        Always(category, "✓ " + scenario);

    static void Write(LogType type, Category category, string message, bool force = false)
    {
        TrucoClientSettings.EnsureLoaded();
        bool allowInfo = force
            || TrucoClientSettings.DebugLogsEnabled
            || category == Category.Api
            || category == Category.Photon
            || category == Category.Lobby
            || category == Category.OneVsOne
            || category == Category.Gameplay
            || category == Category.Rules
            || category == Category.MainMenu;

        if (!allowInfo && type == LogType.Log) return;

        string line = $"[{DateTime.Now:HH:mm:ss.fff}][{Time.unscaledTime:F2}s][Truco/{category}] {message}";
        AppendToFile(line);

        switch (type)
        {
            case LogType.Warning: Debug.LogWarning(line); break;
            case LogType.Error: Debug.LogError(line); break;
            default: Debug.Log(line); break;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootBanner()
    {
        if (_bootLogged) return;
        _bootLogged = true;
        Always(Category.Settings,
            "Log file=" + LogFilePath
            + " debugLogs=" + TrucoClientSettings.DebugLogsEnabled
            + " platform=" + Application.platform
            + " version=" + Application.version);
    }

    static void EnsurePath()
    {
        if (!string.IsNullOrEmpty(_filePath)) return;
        try
        {
            _filePath = Path.Combine(Application.persistentDataPath, FileName);
        }
        catch
        {
            _filePath = FileName;
        }
    }

    static void AppendToFile(string line)
    {
        try
        {
            EnsurePath();
            lock (_fileLock)
            {
                if (File.Exists(_filePath))
                {
                    var info = new FileInfo(_filePath);
                    if (info.Length > MaxFileBytes)
                    {
                        string bak = _filePath + ".1";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(_filePath, bak);
                    }
                }
                File.AppendAllText(_filePath, line + "\n", Encoding.UTF8);
            }
        }
        catch
        {
            // Never break gameplay for logging.
        }
    }
}
