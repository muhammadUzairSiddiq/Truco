#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
#endif
using UnityEngine;

/// <summary>
/// Mobile TTS for Truco callouts. Prefers a male Spanish voice on Android (es-PY / es-ES).
/// Device-dependent: if no male voice is installed, pitch is lowered and a gap log is emitted.
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
    string _chosenVoiceName = "?";
    bool _choseMale;
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
                Debug.Log("[TTS] onInit status=" + status + " (0=SUCCESS)");
                if (status == 0)
                {
                    try
                    {
                        var locPy = new AndroidJavaObject("java.util.Locale", "es", "PY");
                        int r = _tts.Call<int>("setLanguage", locPy);
                        Debug.Log("[TTS] setLanguage es-PY result=" + r);
                        if (r == -1 || r == -2)
                        {
                            var locEs = new AndroidJavaObject("java.util.Locale", "es", "ES");
                            int r2 = _tts.Call<int>("setLanguage", locEs);
                            Debug.Log("[TTS] setLanguage es-ES result=" + r2);
                        }
                        _tts.Call<float>("setSpeechRate", 0.92f);
                        _tts.Call<float>("setPitch", 0.65f);
                        TrySelectMaleSpanishVoice();
                    }
                    catch (System.Exception e) { Debug.LogWarning("[TTS] setLanguage: " + e.Message); }
                    IsReady = true;
                    foreach (var s in _pending) SpeakInternal(s);
                    _pending.Clear();
                }
                else Debug.LogWarning("[TTS] onInit FAILED status=" + status);
            });
            if (_activity != null)
                _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", _activity, _listener);
        }
        catch (System.Exception e) { Debug.LogWarning("[TTS] init: " + e.Message); }
#else
        Debug.Log("[TTS] disabled on this build (Editor/non-Android) — no spoken callouts");
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
        Debug.Log("[TTS] SpeakOrQueue phrase=\"" + phrase + "\" ready=" + IsReady
                  + " voice=" + _chosenVoiceName + " male=" + _choseMale
                  + " pending=" + _pending.Count);
        if (IsReady && _tts != null) SpeakInternal(phrase);
        else
        {
            if (_pending.Count >= 4) _pending.RemoveAt(0);
            _pending.Add(phrase);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void SpeakInternal(string phrase)
    {
        if (_tts == null) return;
        try
        {
            int q = 0;
            int code = _tts.Call<int>("speak", phrase, q, (AndroidJavaObject)null, "t" + phrase.GetHashCode());
            Debug.Log("[TTS] speak code=" + code + " (0=SUCCESS) phrase=\"" + phrase + "\" voice=" + _chosenVoiceName);
        }
        catch (System.Exception e) { Debug.LogWarning("[TTS] speak: " + e.Message); }
    }

    const int GenderNotSpecified = -1;
    const int GenderNeutral = 0;
    const int GenderMale = 1;
    const int GenderFemale = 2;

    static bool IsFemaleVoiceName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        return name.Contains("female") || name.Contains("mujer") || name.Contains("woman")
               || name.Contains("-f-") || name.Contains("_f_")
               || (name.Contains("fem") && !name.Contains("male"));
    }

    static bool IsMaleVoiceName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        if (IsFemaleVoiceName(name)) return false;
        return name.Contains("male") || name.Contains("hombre") || name.Contains("-m-")
               || name.Contains("_m_") || name.Contains("man") || name.Contains("masc");
    }

    void TrySelectMaleSpanishVoice()
    {
        if (_tts == null) return;
        try
        {
            var voices = _tts.Call<AndroidJavaObject>("getVoices");
            if (voices == null)
            {
                Debug.LogWarning("[TTS] GAP getVoices=null — cannot pick male");
                _tts.Call<float>("setPitch", 0.55f);
                return;
            }
            var iterator = voices.Call<AndroidJavaObject>("iterator");
            if (iterator == null) return;

            AndroidJavaObject malePy = null;
            AndroidJavaObject maleEs = null;
            AndroidJavaObject maleAny = null;
            AndroidJavaObject neutralEs = null;
            var dump = new StringBuilder(256);
            int scanned = 0;

            while (iterator.Call<bool>("hasNext"))
            {
                var voice = iterator.Call<AndroidJavaObject>("next");
                if (voice == null) continue;
                var loc = voice.Call<AndroidJavaObject>("getLocale");
                if (loc == null) continue;
                if (loc.Call<string>("getLanguage") != "es") continue;

                string name = voice.Call<string>("getName") ?? string.Empty;
                string country = loc.Call<string>("getCountry") ?? string.Empty;
                int gender = GenderNotSpecified;
                try { gender = voice.Call<int>("getGender"); }
                catch { /* older engines */ }
                scanned++;
                dump.Append('[').Append(name).Append("|").Append(country)
                    .Append("|g=").Append(gender).Append(']');

                if (gender == GenderFemale || IsFemaleVoiceName(name)) continue;

                bool isMale = gender == GenderMale || IsMaleVoiceName(name);
                bool isPy = country == "PY";
                bool isEs = country == "ES" || country == "MX" || country == "AR" || country == "UY";

                if (isMale)
                {
                    if (isPy && malePy == null) malePy = voice;
                    else if (isEs && maleEs == null) maleEs = voice;
                    else if (maleAny == null) maleAny = voice;
                }
                else if (neutralEs == null && gender != GenderFemale)
                    neutralEs = voice;
            }

            Debug.Log("[TTS] Spanish voices scanned=" + scanned + " " + dump);

            var chosen = malePy ?? maleEs ?? maleAny;
            _choseMale = chosen != null;
            if (chosen == null) chosen = neutralEs;

            if (chosen != null)
            {
                _tts.Call<int>("setVoice", chosen);
                _chosenVoiceName = chosen.Call<string>("getName") ?? "?";
                _tts.Call<float>("setPitch", _choseMale ? 0.62f : 0.52f);
                if (_choseMale)
                    Debug.Log("[TTS] MALE voice selected: " + _chosenVoiceName);
                else
                    Debug.LogWarning("[TTS] GAP no male Spanish voice — using neutral/fallback: "
                                     + _chosenVoiceName + " (lowered pitch). Install a male es TTS voice on device.");
            }
            else
            {
                _tts.Call<float>("setPitch", 0.5f);
                Debug.LogWarning("[TTS] GAP no usable Spanish voice — pitch-only fallback");
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
