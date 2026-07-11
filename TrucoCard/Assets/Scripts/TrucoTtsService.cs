#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
#endif
using UnityEngine;

/// <summary>
/// Mobile TTS for Truco callouts. Prefers a male Spanish voice on Android (es-PY / es-ES).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public class TrucoTtsService : MonoBehaviour
{
    public static TrucoTtsService Instance { get; private set; }

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
                if (status == 0)
                {
                    try
                    {
                        var locPy = new AndroidJavaObject("java.util.Locale", "es", "PY");
                        int r = _tts.Call<int>("setLanguage", locPy);
                        if (r == -1 || r == -2)
                        {
                            var locEs = new AndroidJavaObject("java.util.Locale", "es", "ES");
                            _tts.Call<int>("setLanguage", locEs);
                        }
                        _tts.Call<float>("setSpeechRate", 0.95f);
                        _tts.Call<float>("setPitch", 0.72f);
                        TrySelectMaleSpanishVoice();
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
            int q = 0;
            _tts.Call<int>("speak", phrase, q, (AndroidJavaObject)null, "t" + phrase.GetHashCode());
        }
        catch (System.Exception e) { Debug.LogWarning("[TTS] speak: " + e.Message); }
    }

    static bool IsFemaleVoiceName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        return name.Contains("female") || name.Contains("mujer") || name.Contains("woman")
               || name.Contains("-f-") || name.Contains("_f_") || name.Contains("fem");
    }

    static bool IsMaleVoiceName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        return name.Contains("male") || name.Contains("hombre") || name.Contains("-m-")
               || name.Contains("_m_") || name.Contains("man");
    }

    void TrySelectMaleSpanishVoice()
    {
        if (_tts == null) return;
        try
        {
            var voices = _tts.Call<AndroidJavaObject>("getVoices");
            if (voices == null) return;
            var iterator = voices.Call<AndroidJavaObject>("iterator");
            if (iterator == null) return;

            AndroidJavaObject maleVoice = null;
            AndroidJavaObject fallbackEs = null;
            const int genderMale = 500;

            while (iterator.Call<bool>("hasNext"))
            {
                var voice = iterator.Call<AndroidJavaObject>("next");
                if (voice == null) continue;
                var loc = voice.Call<AndroidJavaObject>("getLocale");
                if (loc == null) continue;
                if (loc.Call<string>("getLanguage") != "es") continue;

                string name = voice.Call<string>("getName") ?? string.Empty;
                if (IsFemaleVoiceName(name)) continue;

                int gender = voice.Call<int>("getGender");
                if (gender == genderMale || IsMaleVoiceName(name))
                {
                    maleVoice = voice;
                    break;
                }
                if (fallbackEs == null) fallbackEs = voice;
            }

            var chosen = maleVoice ?? fallbackEs;
            if (chosen != null)
            {
                _tts.Call<int>("setVoice", chosen);
                _tts.Call<float>("setPitch", 0.68f);
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[TTS] male voice: " + e.Message); }
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
