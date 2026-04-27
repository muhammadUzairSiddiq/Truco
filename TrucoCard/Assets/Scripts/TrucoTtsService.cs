using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mobile TTS for Truco callouts (no Asset Store). Uses <c>android.speech.tts.TextToSpeech</c> on device — Spanish (es-PY) when available.
/// Each client speaks locally when a challenge is raised (same Photon event for both = both hear their device’s voice).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public class TrucoTtsService : MonoBehaviour
{
    public static TrucoTtsService Instance { get; private set; }

    /// <summary>True when engine reported success and language is usable.</summary>
    public static bool IsReady { get; private set; }

    public static bool UseTtsOnThisBuild =>
#if UNITY_ANDROID && !UNITY_EDITOR
        true;
#else
        false;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject _activity;
    AndroidJavaObject _tts;
    TtsOnInitListener _listener;
    readonly List<string> _pending = new List<string>(8);
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                _activity = up.GetStatic<AndroidJavaObject>("currentActivity");
            _listener = new TtsOnInitListener(status =>
            {
                if (status == 0) // TextToSpeech.SUCCESS
                {
                    try
                    {
                        var locPy = new AndroidJavaObject("java.util.Locale", "es", "PY");
                        int r = _tts.Call<int>("setLanguage", locPy);
                        if (r == -1 || r == -2) // LANG_MISSING_DATA / LANG_NOT_SUPPORTED
                        {
                            var locEs = new AndroidJavaObject("java.util.Locale", "es", "ES");
                            _tts.Call<int>("setLanguage", locEs);
                        }
                        _tts.Call<float>("setSpeechRate", 0.92f);
                        _tts.Call<float>("setPitch", 1f);
                    }
                    catch (System.Exception e) { Debug.LogWarning("[TTS] setLanguage: " + e.Message); }
                    IsReady = true;
                    foreach (var s in _pending) SpeakInternal(s);
                    _pending.Clear();
                }
                else Debug.LogWarning("[TTS] onInit status=" + status);
            });
            if (_activity != null)
                _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", _activity, _listener);
        }
        catch (System.Exception e) { Debug.LogWarning("[TTS] init: " + e.Message); }
#endif
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_tts != null)
            {
                _tts.Call("stop");
                _tts.Call("shutdown");
            }
        }
        catch { /* ignore */ }
        _tts = null;
        _activity = null;
        _listener = null;
#endif
        IsReady = false;
    }

    /// <summary>Speak a challenge line (Spanish). Queues until engine ready (Android).</summary>
    public static void SpeakChallengePhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase)) return;
        if (Instance == null) return;
        if (!UseTtsOnThisBuild) return;
        Instance.SpeakOrQueue(phrase);
    }

    void SpeakOrQueue(string phrase)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsReady && _tts != null) SpeakInternal(phrase);
        else _pending.Add(phrase);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void SpeakInternal(string phrase)
    {
        if (_tts == null) return;
        try
        {
            int q = 0; // QUEUE_FLUSH
            _tts.Call<int>("speak", phrase, q, (AndroidJavaObject)null, "t" + phrase.GetHashCode());
        }
        catch (System.Exception e) { Debug.LogWarning("[TTS] speak: " + e.Message); }
    }

    class TtsOnInitListener : AndroidJavaProxy
    {
        readonly System.Action<int> _cb;
        public TtsOnInitListener(System.Action<int> cb) : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            _cb = cb;
        }
        public void onInit(int status) => _cb?.Invoke(status);
    }
#endif
}
