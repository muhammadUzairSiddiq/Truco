using UnityEditor;
using UnityEngine;

/// <summary>Editor assembly + Editor asset folder: never serialized into an Android player.</summary>
[CreateAssetMenu(menuName = "Truco/Editor Tournament Practice", fileName = "TournamentPracticeSettings")]
public sealed class TrucoTournamentPracticeSettings : ScriptableObject
{
    public bool enabledInEditor;
    [Tooltip("Drive your seat as well as the AI opponents.")]
    public bool autoPlayYourSeat;
    [Range(.05f, 2)] public float aiActionDelay = .45f;
    public bool withFlor = true;
    public int targetScore = 15;
    public int seed = 1731;
    public const string AssetPath = "Assets/Editor/TrucoTournamentPractice/TournamentPracticeSettings.asset";
    public static TrucoTournamentPracticeSettings Load() => AssetDatabase.LoadAssetAtPath<TrucoTournamentPracticeSettings>(AssetPath);
}

[InitializeOnLoad]
public static class TrucoTournamentPracticeBootstrap
{
    static TrucoTournamentPracticeBootstrap()
    {
        BindHooks();
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.ExitingPlayMode) TrucoTournamentPracticeUI.Close();
            if (state == PlayModeStateChange.EnteredPlayMode) BindHooks();
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("TrucoOpenPracticeOnPlay", false))
            { SessionState.SetBool("TrucoOpenPracticeOnPlay", false); EditorApplication.delayCall += TrucoTournamentPracticeUI.OpenList; }
        };
    }
    [InitializeOnEnterPlayMode]
    static void OnEnterPlayMode(EnterPlayModeOptions options) => BindHooks();
    static void BindHooks()
    {
        TrucoEditorTournamentHooks.Enabled = () => LoadEnabled();
        TrucoEditorTournamentHooks.Open = TrucoTournamentPracticeUI.OpenList;
    }
    static bool LoadEnabled() => TrucoTournamentPracticeSettings.Load()?.enabledInEditor == true;
    [MenuItem("Truco/Editor Tournament Practice/Settings")]
    public static void SelectSettings()
    {
        var settings = TrucoTournamentPracticeSettings.Load();
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TrucoTournamentPracticeSettings>();
            AssetDatabase.CreateAsset(settings, TrucoTournamentPracticeSettings.AssetPath); AssetDatabase.SaveAssets();
        }
        Selection.activeObject = settings; EditorGUIUtility.PingObject(settings);
    }
    [MenuItem("Truco/Editor Tournament Practice/Open AI Tournament")]
    public static void Open()
    {
        SelectSettings(); var settings = TrucoTournamentPracticeSettings.Load();
        settings.enabledInEditor = true; EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        if (EditorApplication.isPlaying) TrucoTournamentPracticeUI.OpenList();
        else { SessionState.SetBool("TrucoOpenPracticeOnPlay", true); EditorApplication.EnterPlaymode(); }
    }
}
