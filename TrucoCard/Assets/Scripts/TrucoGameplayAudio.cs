using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// Gameplay SFX for Truco retos. Local: <see cref="PlayLocalRaise"/> when you press a challenge (same moment as <see cref="Photon.Pun.PhotonNetwork.RaiseEvent"/> to Others).
/// Remote: <see cref="PlayFromPhotonEvent"/> from <see cref="GameManager.OnEvent"/> — same Photon event that drives UI/text, so audio follows the network flow.
/// Place optional clips under Resources/TrucoSFX/ (truco, envido, …) or assign in the inspector on the child created by <see cref="EnsureUnder"/>.
/// </summary>
[DisallowMultipleComponent]
public class TrucoGameplayAudio : MonoBehaviour
{
    public static TrucoGameplayAudio Instance { get; private set; }

    /// <summary>Reto / callout clips (truco, envido, quiero…).</summary>
    [SerializeField] private AudioSource _calloutSource;
    /// <summary>Card table SFX only; keeps callouts clear when both fire close together.</summary>
    [SerializeField] private AudioSource _cardSource;

    [Header("Retos (opcional)")]
    [SerializeField] private AudioClip _truco;
    [SerializeField] private AudioClip _retruco;
    [SerializeField] private AudioClip _vale4;
    [SerializeField] private AudioClip _envido;
    [SerializeField] private AudioClip _realEnvido;
    [SerializeField] private AudioClip _faltaEnvido;
    [SerializeField] private AudioClip _quiero;
    [SerializeField] private AudioClip _noQuiero;
    [SerializeField] private AudioClip _flor;
    [SerializeField] private AudioClip _florChica;
    [SerializeField] private AudioClip _conFlorQuiero;
    [SerializeField] private AudioClip _contraFlor;
    [SerializeField] private AudioClip _mazo;
    [SerializeField] private AudioClip _cardPlaced;

    void Awake()
    {
        Instance = this;
        EnsureAudioSources();
        _calloutSource.playOnAwake = false;
        _cardSource.playOnAwake = false;
        _calloutSource.spatialBlend = 0f;
        _cardSource.spatialBlend = 0f;
        TryLoadClipsFromResources();
#if !UNITY_ANDROID || UNITY_EDITOR
        EnsureProceduralFallbacks();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        if (GetComponent<TrucoTtsService>() == null) gameObject.AddComponent<TrucoTtsService>();
#endif
    }

    void EnsureAudioSources()
    {
        var list = GetComponents<AudioSource>();
        if (list.Length == 0)
        {
            _calloutSource = gameObject.AddComponent<AudioSource>();
            _cardSource = gameObject.AddComponent<AudioSource>();
        }
        else if (list.Length == 1)
        {
            _calloutSource = list[0];
            _cardSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            _calloutSource = list[0];
            _cardSource = list[1];
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void PlayLocalRaise(byte eventCode)
    {
        // Visual callout (shown regardless of audio/TTS path) so both players can read the canto.
        if (UIMANAGER.Instance != null)
            UIMANAGER.Instance.ShowChallengeCallout(TrucoCalloutPhrases.ForEventCode(eventCode), true);
        if (Instance == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        var phrase = TrucoCalloutPhrases.ForEventCode(eventCode);
        if (!string.IsNullOrEmpty(phrase) && TrucoTtsService.UseTtsOnThisBuild && TrucoTtsService.Instance != null)
        {
            TrucoTtsService.SpeakChallengePhrase(phrase);
            return;
        }
#endif
        var c = Instance.ResolveClip(eventCode);
        if (c != null && Instance._calloutSource != null)
            Instance._calloutSource.PlayOneShot(c);
    }

    /// <summary>Incoming <see cref="RaiseEvent"/> from another player — call from <see cref="GameManager.OnEvent"/> only.</summary>
    public static void PlayFromPhotonEvent(EventData photonEvent)
    {
        if (photonEvent == null) return;
        PlayIncomingEvent(photonEvent.Code, photonEvent.Sender);
    }

    /// <summary>Same as network event: other player’s raise. Skips if this client is the sender (local already used <see cref="PlayLocalRaise"/>).</summary>
    public static void PlayIncomingEvent(byte eventCode, int senderActorNumber)
    {
        if (Photon.Pun.PhotonNetwork.LocalPlayer != null
            && senderActorNumber != 0
            && senderActorNumber == Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return;
        }
        // Visual callout for the opponent's canto (shown regardless of audio/TTS path).
        if (UIMANAGER.Instance != null)
            UIMANAGER.Instance.ShowChallengeCallout(TrucoCalloutPhrases.ForEventCode(eventCode), false);
        if (Instance == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        var phrase = TrucoCalloutPhrases.ForEventCode(eventCode);
        if (!string.IsNullOrEmpty(phrase) && TrucoTtsService.UseTtsOnThisBuild && TrucoTtsService.Instance != null)
        {
            TrucoTtsService.SpeakChallengePhrase(phrase);
            return;
        }
#endif
        var c = Instance.ResolveClip(eventCode);
        if (c != null && Instance._calloutSource != null)
            Instance._calloutSource.PlayOneShot(c);
    }

    public static void PlayCardPlaced()
    {
        if (Instance == null || Instance._cardSource == null || Instance._cardPlaced == null) return;
        Instance._cardSource.PlayOneShot(Instance._cardPlaced);
    }

    AudioClip ResolveClip(byte code)
    {
        if (code == UIMANAGER.TRUCO_CHALLENGE) return _truco;
        if (code == UIMANAGER.RETRUCO_CHALLENGE) return _retruco;
        if (code == UIMANAGER.VALE4_CHALLENGE) return _vale4;
        if (code == UIMANAGER.ENVIDO_CHALLENGE) return _envido;
        if (code == UIMANAGER.REALENVIDO_CHALLENGE) return _realEnvido;
        if (code == UIMANAGER.FALTAENVIDO_CHALLENGE) return _faltaEnvido;
        if (code == UIMANAGER.QUEIRO_CHALLENGE) return _quiero;
        if (code == UIMANAGER.NOQUEIRO_CHALLENGE) return _noQuiero;
        if (code == UIMANAGER.FLOR_CHALLENGE) return _flor;
        if (code == UIMANAGER.FLOR_CHICA_CHALLENGE) return _florChica;
        if (code == UIMANAGER.CON_FLOR_QUIERO_CHALLENGE) return _conFlorQuiero;
        if (code == UIMANAGER.CONTRA_FLOR_CHALLENGE) return _contraFlor;
        if (code == UIMANAGER.MAZO_CHALLENGE) return _mazo;
        return null;
    }

    void TryLoadClipsFromResources()
    {
        const string p = "TrucoSFX/";
        if (_truco == null) _truco = Resources.Load<AudioClip>(p + "truco");
        if (_retruco == null) _retruco = Resources.Load<AudioClip>(p + "retruco");
        if (_vale4 == null) _vale4 = Resources.Load<AudioClip>(p + "vale4");
        if (_envido == null) _envido = Resources.Load<AudioClip>(p + "envido");
        if (_realEnvido == null) _realEnvido = Resources.Load<AudioClip>(p + "realEnvido");
        if (_faltaEnvido == null) _faltaEnvido = Resources.Load<AudioClip>(p + "faltaEnvido");
        if (_quiero == null) _quiero = Resources.Load<AudioClip>(p + "quiero");
        if (_noQuiero == null) _noQuiero = Resources.Load<AudioClip>(p + "noQuiero");
        if (_flor == null) _flor = Resources.Load<AudioClip>(p + "flor");
        if (_florChica == null) _florChica = Resources.Load<AudioClip>(p + "florChica");
        if (_conFlorQuiero == null) _conFlorQuiero = Resources.Load<AudioClip>(p + "conFlorQuiero");
        if (_contraFlor == null) _contraFlor = Resources.Load<AudioClip>(p + "contraFlor");
        if (_mazo == null) _mazo = Resources.Load<AudioClip>(p + "mazo");
        if (_cardPlaced == null) _cardPlaced = Resources.Load<AudioClip>(p + "cardPlaced");
    }

    /// <summary>Si no hay WAV en Resources/TrucoSFX/, genera tonos cortos para que local y red tengan feedback audible (sustituir por clips reales).</summary>
    void EnsureProceduralFallbacks()
    {
        if (_truco == null) _truco = CreateToneClip("truco", 329.63f, 0.11f);
        if (_retruco == null) _retruco = CreateToneClip("retruco", 392f, 0.11f);
        if (_vale4 == null) _vale4 = CreateToneClip("vale4", 523.25f, 0.12f);
        if (_envido == null) _envido = CreateToneClip("envido", 277.18f, 0.1f);
        if (_realEnvido == null) _realEnvido = CreateToneClip("realEnvido", 311.13f, 0.1f);
        if (_faltaEnvido == null) _faltaEnvido = CreateToneClip("faltaEnvido", 349.23f, 0.12f);
        if (_quiero == null) _quiero = CreateToneClip("quiero", 440f, 0.08f);
        if (_noQuiero == null) _noQuiero = CreateToneClip("noQuiero", 196f, 0.1f);
        if (_flor == null) _flor = CreateToneClip("flor", 587.33f, 0.1f);
        if (_florChica == null) _florChica = CreateToneClip("florChica", 554.37f, 0.09f);
        if (_conFlorQuiero == null) _conFlorQuiero = CreateToneClip("conFlorQuiero", 659.26f, 0.09f);
        if (_contraFlor == null) _contraFlor = CreateToneClip("contraFlor", 493.88f, 0.11f);
        if (_mazo == null) _mazo = CreateToneClip("mazo", 146.83f, 0.14f);
        if (_cardPlaced == null) _cardPlaced = CreateToneClip("cardPlaced", 880f, 0.05f);
    }

    static AudioClip CreateToneClip(string name, float freqHz, float durationSec)
    {
        int rate = 44100;
        int n = Mathf.Max(256, Mathf.RoundToInt(rate * durationSec));
        var samples = new float[n];
        float vol = 0.22f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = 1f - (float)i / n;
            samples[i] = Mathf.Sin(2f * Mathf.PI * freqHz * t) * vol * env;
        }
        var c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    public static void EnsureUnder(Transform parent)
    {
        if (Instance != null) return;
        if (parent == null) return;
        var existing = parent.GetComponentInChildren<TrucoGameplayAudio>(true);
        if (existing != null) return;
        var go = new GameObject("TrucoGameplayAudio");
        go.transform.SetParent(parent, false);
        go.AddComponent<TrucoGameplayAudio>();
    }
}
