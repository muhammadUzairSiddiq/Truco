using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UIMANAGER : MonoBehaviour
{
    // Event Names to Call on Network
    public static byte TRUCO_CHALLENGE = 10;
    public static byte RETRUCO_CHALLENGE = 11;
    public static byte VALE4_CHALLENGE = 12;
    public static byte ENVIDO_CHALLENGE = 13;
    public static byte REALENVIDO_CHALLENGE = 14;
    public static byte FALTAENVIDO_CHALLENGE = 15;
    public static byte QUEIRO_CHALLENGE = 16;
    public static byte NOQUEIRO_CHALLENGE = 17;
    public static byte FLOR_CHALLENGE = 18;
    public static byte CON_FLOR_QUIERO_CHALLENGE = 19;
    public static byte CONTRA_FLOR_CHALLENGE = 20;
    public static byte FLOR_CHICA_CHALLENGE = 21;
    public static byte MAZO_CHALLENGE = 22;
    /// <summary>CustomData flag for FLOR when opponent cannot reject (auto +3).</summary>
    public const byte FlorAutoAwardFlag = 1;
    
    // Contains a list off challenges 
    public List<ChallengeType> invokedChallenges = new List<ChallengeType>();
    
    public static UIMANAGER Instance { get; private set; }
    [SerializeField] private List<GameObject> allUiButtons;
    [SerializeField] private GameObject turnText;
    [Tooltip("Creada en runtime si no existe: panel detrás del texto de turno (no bloquea clics).")]
    [SerializeField] private RectTransform turnTextBackground;
    static Sprite _cachedUiWhiteSprite;
    static readonly Color TurnBannerBgNormal = TrucoGameplayTimerBanner.BgNormal;
    static readonly Color TurnBannerBgUrgent = TrucoGameplayTimerBanner.BgUrgent;
    static readonly Color TurnBannerBgReconnect = TrucoGameplayTimerBanner.BgReconnect;
    public List<GameObject> myPlayerCards;
    [SerializeField] private List<GameObject> otherPlayersCards;
    [SerializeField] private List<Transform> myDisplayCardsPosition;
    [SerializeField] private List<Transform> otherPlayersDisplayCardsPosition;
    [SerializeField] private List<Vector3> myCardsInitialPositions;
    [SerializeField] private List<Vector3> otherPlayersCardsInitialPositions;
    
    // UI Buttons
    [SerializeField] private GameObject truco;
    [SerializeField] private GameObject retruco;
    [SerializeField] private GameObject vale4;
    [SerializeField] private GameObject envido;
    [SerializeField] private GameObject realEnvido;
    [SerializeField] private GameObject faltaEnvido;
    [SerializeField] private GameObject queiro;
    [SerializeField] private GameObject noQueiro;
    [SerializeField] private GameObject flor;
    [SerializeField] private GameObject conFlorQuiero;
    [SerializeField] private GameObject contraFlor;
    [SerializeField] private GameObject florChica;
    [SerializeField] private GameObject mazo;
    
    public bool _envidoPlayed = false;
    public bool trucoPlayed = false;
    
    public Dictionary<ChallengeType,int> unAnsweredChallenges = new Dictionary<ChallengeType, int>();
    private bool _cantChallenge = false;
    private int _myDisplayCardIndex = 0;
    private int _otherPlayersDisplayCardIndex = 0;
    private bool _challengeAccepted = false;
    private bool _isDoubleEnvido = false;
    private bool _isDoubleRealEnvido = false;
    public bool _isChallengepPending = false;

    /// <summary>True only on the player who currently must answer a canto (truco/envido/flor).</summary>
    public bool _iOweChallengeResponse = false;
    private Coroutine _challengeResponseRoutine;
    const float ChallengeResponseSeconds = 30f;
    float _challengeSentAt = -1f;

    public void MarkChallengeSentNow() => _challengeSentAt = Time.time;

    public int GetChallengeResponseSecondsRemaining()
    {
        if (_challengeSentAt < 0f) return Mathf.CeilToInt(ChallengeResponseSeconds);
        return Mathf.Max(0, Mathf.CeilToInt(ChallengeResponseSeconds - (Time.time - _challengeSentAt)));
    }

    /// <summary>Clears canto state when a hand ends so the next deal starts clean.</summary>
    public void ResetHandChallengeState()
    {
        _challengeSentAt = -1f;
        _isChallengepPending = false;
        _iOweChallengeResponse = false;
        _cantChallenge = false;
        _challengeAccepted = false;
        trucoPlayed = false;
        _envidoPlayed = false;
        unAnsweredChallenges.Clear();
        invokedChallenges.Clear();
        CancelChallengeResponseCountdown();
    }

    public void ClearTurnBanner() => DisableTurnText();

    /// <summary>Start the 30 s response countdown on the player who received a canto. Auto-declines on timeout (anti-freeze).</summary>
    public void BeginChallengeResponseCountdown()
    {
        if (SpectatorContext.IsSpectator) return;
        _iOweChallengeResponse = true;
        if (_challengeResponseRoutine != null) StopCoroutine(_challengeResponseRoutine);
        TrucoRulesScenarioLog.Ok("ChallengeResponseTimer START",
            "window=" + ChallengeResponseSeconds + "s last=" + (GameManager.Instance != null
                ? GameManager.Instance.lastChallengeType.ToString() : "?"));
        _challengeResponseRoutine = StartCoroutine(ChallengeResponseCountdown());
    }

    /// <summary>Cancel the response countdown (called whenever this player takes any action / buttons disabled).</summary>
    public void CancelChallengeResponseCountdown()
    {
        _iOweChallengeResponse = false;
        if (_challengeResponseRoutine != null) { StopCoroutine(_challengeResponseRoutine); _challengeResponseRoutine = null; }
    }

    System.Collections.IEnumerator ChallengeResponseCountdown()
    {
        float d = ChallengeResponseSeconds;
        while (d > 0f)
        {
            if (!_iOweChallengeResponse) { _challengeResponseRoutine = null; yield break; }
            if (GameManager.Instance != null && GameManager.Instance._gameEnded) { _challengeResponseRoutine = null; yield break; }
            d -= Time.deltaTime;
            int sec = Mathf.CeilToInt(d);
            if (sec < 0) sec = 0;
            bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
            UpdateTurnText(TrucoTextosClient.FormatoBannerResponderConSegundos(sec), -1f, urgent);
            yield return null;
        }
        _challengeResponseRoutine = null;
        if (!_iOweChallengeResponse) yield break;
        if (GameManager.Instance != null && GameManager.Instance._gameEnded) yield break;
        _iOweChallengeResponse = false;
        TrucoRulesScenarioLog.Ok("ChallengeResponseTimeout → auto NoQuiero",
            "window=" + ChallengeResponseSeconds + "s last=" + (GameManager.Instance != null
                ? GameManager.Instance.lastChallengeType.ToString() : "?"));
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.TiempoRespuestaAgotado);
        // Safe auto-resolution: decline the canto so the hand never freezes.
        ChallengeNoQueiro();
    }

    // ───────── Visual callout banner: shows which canto each player declared (truco/envido/flor/…) ─────────
    private GameObject _calloutGo;
    private TMPro.TMP_Text _calloutTmp;

    /// <summary>Show a short banner with the canto/declaration so BOTH players can see it (not only hear it).</summary>
    public void ShowChallengeCallout(string phrase, bool mine)
    {
        if (string.IsNullOrEmpty(phrase)) return;
        EnsureCalloutLabel();
        if (_calloutTmp == null || _calloutGo == null) return;
        string who = mine ? "Vos" : "Rival";
        string color = mine ? "#7CFC9B" : "#FFC24A";
        _calloutTmp.text = $"<color={color}><b>{who}:</b></color> {phrase}";
        _calloutGo.SetActive(true);
        CancelInvoke(nameof(HideCallout));
        Invoke(nameof(HideCallout), 2.4f);
    }

    /// <summary>Show a free-form declaration (e.g. "Tengo 31", "Son buenas", "Flor: 38").</summary>
    public void ShowDeclarationCallout(string text, bool mine) => ShowChallengeCallout(text, mine);

    void HideCallout()
    {
        if (_calloutGo != null) _calloutGo.SetActive(false);
    }

    void EnsureCalloutLabel()
    {
        if (_calloutTmp != null) return;
        Canvas canvas = turnText != null ? turnText.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
        if (canvas == null) return;
        _calloutGo = new GameObject("ChallengeCallout", typeof(RectTransform));
        _calloutGo.transform.SetParent(canvas.transform, false);
        var rt = _calloutGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -120f);
        rt.sizeDelta = new Vector2(680f, 80f);
        var bg = _calloutGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.58f);
        bg.raycastTarget = false;
        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(_calloutGo.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(18f, 8f);
        trt.offsetMax = new Vector2(-18f, -8f);
        _calloutTmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        _calloutTmp.alignment = TMPro.TextAlignmentOptions.Center;
        _calloutTmp.raycastTarget = false;
        _calloutTmp.enableAutoSizing = true;
        _calloutTmp.fontSizeMin = 26f;
        _calloutTmp.fontSizeMax = 50f;
        _calloutGo.SetActive(false);
    }

    private void Awake()
    {
        Instance = this;
        // store Initial Positions of Cards
        for (int i = 0; i < otherPlayersCards.Count; i++)
        {
            otherPlayersCardsInitialPositions.Add(otherPlayersCards[i].transform.position);
        }
        if (turnText != null)
        {
            var tmp = turnText.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) tmp.raycastTarget = false;
        }
        EnsureTurnBannerBackground();
        if (!SpectatorContext.IsSpectator) EnsureMuteButton();
    }

    void EnsureMuteButton()
    {
        Canvas canvas = turnText != null ? turnText.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
        if (canvas == null || canvas.transform.Find("MuteAudioBtn") != null) return;
        var go = new GameObject("MuteAudioBtn", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(120f, 48f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);
        var btn = go.AddComponent<Button>();
        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize = 22f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = Color.white;
        tmp.text = TrucoTextosClient.SilenciarAudio;
        tmp.raycastTarget = false;
        btn.onClick.AddListener(() =>
        {
            TrucoGameplayAudio.ToggleMute();
            tmp.text = TrucoGameplayAudio.IsMuted ? "🔇" : TrucoTextosClient.SilenciarAudio;
            AppManager.Instance?.DisplayNotification(
                TrucoGameplayAudio.IsMuted
                    ? TrucoLocalization.T(TrucoLocalization.Key.AudioSilenciado)
                    : TrucoLocalization.T(TrucoLocalization.Key.AudioActivado));
        });
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(DisableTurnText));
    }

    void EnsureTurnBannerBackground()
    {
        if (turnText == null) return;
        if (turnTextBackground != null) return;
        var textRt = turnText.GetComponent<RectTransform>();
        if (textRt == null) return;
        var t = textRt.parent;
        if (t == null) return;
        if (t.Find("TurnTimerBannerBg") != null) return;
        var go = new GameObject("TurnTimerBannerBg");
        go.transform.SetParent(t, false);
        if (_cachedUiWhiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            _cachedUiWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
        var im = go.AddComponent<Image>();
        im.sprite = _cachedUiWhiteSprite;
        im.type = Image.Type.Simple;
        im.raycastTarget = false;
        im.color = TurnBannerBgNormal;
        go.transform.SetSiblingIndex(textRt.GetSiblingIndex());
        var bg = go.GetComponent<RectTransform>();
        bg.anchorMin = textRt.anchorMin;
        bg.anchorMax = textRt.anchorMax;
        bg.pivot = textRt.pivot;
        bg.anchoredPosition = textRt.anchoredPosition + new Vector2(0f, -8f);
        bg.sizeDelta = new Vector2(Mathf.Max(textRt.sizeDelta.x + 64f, 380f), Mathf.Max(textRt.sizeDelta.y + 48f, 120f));
        turnTextBackground = bg;
        turnText.transform.SetAsLastSibling();
    }

    void ApplyTurnBannerSize(bool largeCountdown)
    {
        if (turnTextBackground == null || turnText == null) return;
        var textRt = turnText.GetComponent<RectTransform>();
        if (textRt == null) return;
        turnTextBackground.sizeDelta = largeCountdown
            ? new Vector2(Mathf.Max(textRt.sizeDelta.x + 80f, 420f), Mathf.Max(textRt.sizeDelta.y + 56f, 140f))
            : new Vector2(Mathf.Max(textRt.sizeDelta.x + 64f, 380f), Mathf.Max(textRt.sizeDelta.y + 48f, 120f));
        if (turnTextBackground != null && textRt != null)
            turnTextBackground.anchoredPosition = textRt.anchoredPosition + new Vector2(0f, -8f);
    }

    /// <param name="autoHideSeconds">Si es &lt; 0, el banner queda visible hasta el próximo <see cref="UpdateTurnText"/> (p. ej. durante la cuenta de 30 s).</param>
    /// <param name="turnCountdownUrgent">Panel del cronómetro ligeramente más cálido cuando quedan pocos segundos.</param>
    /// <param name="reconnectCountdown">Rival desconectado — panel negro más marcado para el timer de 60 s.</param>
    public void UpdateTurnText(string message, float autoHideSeconds = 2.25f, bool turnCountdownUrgent = false, bool reconnectCountdown = false)
    {
        if (turnText == null) return;
        CancelInvoke(nameof(DisableTurnText));
        bool persistentTimer = autoHideSeconds < 0f;
        EnsureTurnBannerBackground();
        ApplyTurnBannerSize(persistentTimer);
        turnText.SetActive(true);
        if (turnTextBackground != null) turnTextBackground.gameObject.SetActive(true);
        ApplyTurnBannerUrgency(turnCountdownUrgent, reconnectCountdown);
        var tmp = turnText.GetComponent<TMPro.TMP_Text>();
        if (tmp != null)
        {
            tmp.text = message;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }
        turnText.transform.SetAsLastSibling();
        if (autoHideSeconds >= 0f)
            Invoke(nameof(DisableTurnText), autoHideSeconds);
    }

    void ApplyTurnBannerUrgency(bool urgent, bool reconnect = false)
    {
        if (turnTextBackground == null) return;
        var img = turnTextBackground.GetComponent<Image>();
        if (img == null) return;
        if (reconnect) img.color = TurnBannerBgReconnect;
        else if (urgent) img.color = TurnBannerBgUrgent;
        else img.color = TurnBannerBgNormal;
    }

    // Go Back to Main Menu
    public void GoToHome()
    {
        bool forfeit = GameManager.Instance != null
            && !GameManager.Instance._gameEnded
            && OneVsOneMatchSession.GameStarted
            && !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId);
        if (forfeit)
            _ = OneVsOneMatchLifecycle.ForfeitActiveMatchAsync(OneVsOneMatchSession.CurrentMatchId);
        TrucoReturnFromGameplayCleanup.MarkLeavingGameplay(
            forfeit || (GameManager.Instance != null && GameManager.Instance._gameEnded));
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        PhotonNetwork.Disconnect();
        TrucoSceneTransition.Go("MainMenu");
    }
    
    void DisableTurnText()
    {
        if (turnText != null) turnText.SetActive(false);
        if (turnTextBackground != null) turnTextBackground.gameObject.SetActive(false);
    }
    
    // This enables all the buttons in the UI Depending on the game state
    public void EnableButtons()
    {
        if (GameManager.Instance == null || GameManager.Instance._gameEnded || GameManager.Instance.HandResolved)
        {
            DisableButtons();
            return;
        }
        if (!GameManager.Instance.IsMyTurn())
        {
            DisableButtons();
            return;
        }
        // Challenge response UI (Quiero/No quiero) is configured by TrucoChallenged et al.
        if (_isChallengepPending && unAnsweredChallenges.Count > 0)
            return;

        foreach (GameObject button in allUiButtons)
        {
            if (button != null && !GameManager.Instance._gameEnded)
                button.SetActive(true);
        }

        if (!invokedChallenges.Contains(ChallengeType.Truco))
            truco.SetActive(true);
        else if (!invokedChallenges.Contains(ChallengeType.Retruco))
            retruco.SetActive(true);
        else if (!invokedChallenges.Contains(ChallengeType.Vale4))
            vale4.SetActive(true);
        
        if (GameManager.Instance.cardPlayed)
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
        }
        else
        {
            if (!_challengeAccepted)
            {
                Debug.LogWarning("It is my turn, enabling buttons");
                if (!invokedChallenges.Contains(ChallengeType.Envido) && !invokedChallenges.Contains(ChallengeType.Flor) 
                                                                      && !invokedChallenges.Contains(ChallengeType.ContraFlor)
                                                                      && !invokedChallenges.Contains(ChallengeType.ConFlorQuiero)
                                                                      && !invokedChallenges.Contains(ChallengeType.FaltaEnvido)
                                                                      && !invokedChallenges.Contains(ChallengeType.RealEnvido))
                {
                    envido.SetActive(true);
                    realEnvido.SetActive(true);
                    faltaEnvido.SetActive(true);
                }
                if (OneVsOneMatchSession.WithFlor
                                                         && GameManager.Instance.PlayerHasFlor() && !invokedChallenges.Contains(ChallengeType.Flor) 
                                                         && !invokedChallenges.Contains(ChallengeType.Envido)
                                                         && !invokedChallenges.Contains(ChallengeType.FaltaEnvido)
                                                         && !invokedChallenges.Contains(ChallengeType.RealEnvido)
                                                         && !invokedChallenges.Contains(ChallengeType.Truco))
                {
                    flor.SetActive(true);
                }
            }
        }

        // Mazo is always available on your turn (concede the hand).
        if (mazo != null)
            mazo.SetActive(true);
    }
    
    // This disables all the buttons in the UI
    public void DisableButtons()
    {
        // Any local challenge action / button-disable means this player no longer "owes" a response.
        // Safe failure mode: if we ever cancel too early we simply lose the auto-decline net (never wrong-decline).
        CancelChallengeResponseCountdown();
        foreach (GameObject button in allUiButtons)
        {
            if (button != null)
            {
                button.SetActive(false);
            }
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            queiro.SetActive(false);
            noQueiro.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            conFlorQuiero.SetActive(false);
            contraFlor.SetActive(false);
            florChica.SetActive(false);
            mazo.SetActive(false);
        }
    }

    // This moves my card to center of screen 
    public void ShowMyCard(Transform cardTransform,CardSuit suit, int value)
    {
        if (cardTransform == null || myDisplayCardsPosition == null || myDisplayCardsPosition.Count == 0) return;
        int idx = Mathf.Min(_myDisplayCardIndex, myDisplayCardsPosition.Count - 1);
        PrepareTrickLayout(cardTransform, true);
        cardTransform.LeanMove(myDisplayCardsPosition[idx].position, 0.5f).setEaseInOutCubic();
        _myDisplayCardIndex++;
    }
    
    // This moves other player's Card to center of screen
    public void ShowOtherPlayersCard(CardSuit suit, int value)
    {
        if (otherPlayersCards == null || _otherPlayersDisplayCardIndex < 0 || _otherPlayersDisplayCardIndex >= otherPlayersCards.Count) return;
        if (otherPlayersDisplayCardsPosition == null || _otherPlayersDisplayCardIndex >= otherPlayersDisplayCardsPosition.Count) return;
        var o = otherPlayersCards[_otherPlayersDisplayCardIndex].GetComponent<Card>();
        if (o == null) return;
        o.SetupCard(suit, value);
        var t = o.transform;
        PrepareTrickLayout(t, true);
        t.LeanMove(otherPlayersDisplayCardsPosition[_otherPlayersDisplayCardIndex].position, 0.5f).setEaseInOutCubic();
        _otherPlayersDisplayCardIndex++;
        if (envido != null) envido.SetActive(false);
    }

    /// <summary>Shows cards revealed for envido/flor scoring (visible to both players in the trick area).</summary>
    public void TriggerMazoOnTimeout()
    {
        if (SpectatorContext.IsSpectator || GameManager.Instance == null || GameManager.Instance._gameEnded) return;
        if (GameManager.Instance.HandResolved || !GameManager.Instance.IsMyTurn()) return;
        TrucoRulesScenarioLog.Ok("TriggerMazoOnTimeout → ChallengeMazo");
        GameManager.Instance.SetCanPlayCard(false);
        DisableButtons();
        ChallengeMazo();
    }

    public void ShowRevealedScoringCard(CardSuit suit, int value, bool isMine)
    {
        if (isMine)
        {
            foreach (var card in myPlayerCards)
            {
                if (card == null) continue;
                var c = card.GetComponent<Card>();
                if (c != null && c.suit == suit && c.value == value)
                {
                    ShowMyCard(card.transform, suit, value);
                    return;
                }
            }
        }
        else
        {
            ShowOtherPlayersCard(suit, value);
        }
    }

    void PrepareTrickLayout(Transform t, bool lastSibling = true)
    {
        if (t == null) return;
        var le = t.GetComponent<LayoutElement>();
        if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        if (lastSibling) t.SetAsLastSibling();
        var cg = t.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
    }
    
    // This is called from UI Button To Call Truco Challenge on other player
    public void ChallengeTruco()
    {
        TrucoGameplayAudio.PlayLocalRaise(TRUCO_CHALLENGE);
        MarkChallengeSentNow();
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Truco);
        _cantChallenge = true;
        DisableButtons();
        truco.SetActive(false);
        retruco.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        unAnsweredChallenges.Add(ChallengeType.Truco,PhotonNetwork.LocalPlayer.ActorNumber);
        GameManager.Instance.SetCanPlayCard(false);
        GameManager.Instance.deniedFlor = true;
        GameManager.Instance.challengePoints = 1;
        GameManager.Instance.noQuieroPoints = 1;
        GameManager.Instance.lastChallengeType = ChallengeType.Truco;
        TrucoRulesScenarioLog.Ok("Local RAISE Truco", "challengePts=1 noQuieroPts=1");
        PhotonNetwork.RaiseEvent(TRUCO_CHALLENGE,null, RaiseEventOptions.Default, SendOptions.SendReliable);
    }
    
    // This is called from UI Button To Call Retruco Challenge on other player
    public void ChallengeRetruco()
    {
        TrucoGameplayAudio.PlayLocalRaise(RETRUCO_CHALLENGE);
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Retruco);
        _cantChallenge = true;
        DisableButtons();
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        unAnsweredChallenges.Add(ChallengeType.Retruco,PhotonNetwork.LocalPlayer.ActorNumber);
        GameManager.Instance.deniedFlor = true;
        GameManager.Instance.challengePoints = 2;
        GameManager.Instance.noQuieroPoints = 2;
        GameManager.Instance.SetCanPlayCard(false);
        GameManager.Instance.lastChallengeType = ChallengeType.Retruco;
        GameManager.Instance.cardPlayed = true;
        TrucoRulesScenarioLog.Ok("Local RAISE Retruco", "challengePts=2");
        PhotonNetwork.RaiseEvent(RETRUCO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);

    }
    
    // This is called from UI Button To Call Vale4 Challenge on other player
    public void ChallengeVale4()
    {
        TrucoGameplayAudio.PlayLocalRaise(VALE4_CHALLENGE);
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Vale4);
        _cantChallenge = true;
        DisableButtons();
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        unAnsweredChallenges.Add(ChallengeType.Vale4,PhotonNetwork.LocalPlayer.ActorNumber);
        GameManager.Instance.deniedFlor = true;
        GameManager.Instance.challengePoints = 3;
        GameManager.Instance.noQuieroPoints = 3;
        GameManager.Instance.SetCanPlayCard(false);
        GameManager.Instance.lastChallengeType = ChallengeType.Vale4;
        GameManager.Instance.cardPlayed = true;
        TrucoRulesScenarioLog.Ok("Local RAISE Vale4", "challengePts=3");
        PhotonNetwork.RaiseEvent(VALE4_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);

    }
    
    // This is called from UI Button To Call Envido Challenge on other player
    public void ChallengeEnvido()
    {
        TrucoGameplayAudio.PlayLocalRaise(ENVIDO_CHALLENGE);
        // This checks if other player had invoked Truco Challenge and resets the Points
        if (GameManager.Instance.lastChallengeType != ChallengeType.Envido)
        {
            GameManager.Instance.challengePoints = 0;
        }
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Envido);
        DisableButtons();
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        GameManager.Instance.ActiveChallenges.Add(ChallengeType.Envido);
        GameManager.Instance.deniedFlor = true;
        GameManager.Instance.lastChallengeType = ChallengeType.Envido;
        GameManager.Instance.challengePoints += 2;
        TrucoRulesScenarioLog.Ok("Local RAISE Envido", "challengePts+=" + GameManager.Instance.challengePoints);
        PhotonNetwork.RaiseEvent(ENVIDO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
    }
    
    // This is called from UI Button To Call RealEnvido Challenge on other player
    public void ChallengeRealEnvido()
    {
        TrucoGameplayAudio.PlayLocalRaise(REALENVIDO_CHALLENGE);
        // This checks if other player had invoked Truco Challenge and resets the Points
        if (GameManager.Instance.lastChallengeType == ChallengeType.Truco)
        {
            GameManager.Instance.challengePoints = 0;
        }
        
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Envido);
        DisableButtons();
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        GameManager.Instance.ActiveChallenges.Add(ChallengeType.RealEnvido);
        GameManager.Instance.deniedFlor = true;
        GameManager.Instance.lastChallengeType = ChallengeType.RealEnvido;
        GameManager.Instance.challengePoints += 3;
        TrucoRulesScenarioLog.Ok("Local RAISE RealEnvido", "challengePts=" + GameManager.Instance.challengePoints);
        PhotonNetwork.RaiseEvent(REALENVIDO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
    }
    
    // This is called from UI Button To Call Falta Envido Challenge on other player

    public void ChallengeFaltaEnvido()
    {
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Envido);
        invokedChallenges.Add(ChallengeType.FaltaEnvido);
        DisableButtons();
        // This checks if falta envido has been already invoked by other player and accepts the challenge
        if (GameManager.Instance.lastChallengeType == ChallengeType.FaltaEnvido)
        {
            TrucoGameplayAudio.PlayLocalRaise(QUEIRO_CHALLENGE);
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            conFlorQuiero.SetActive(false);
            contraFlor.SetActive(false);
            florChica.SetActive(false);
            queiro.SetActive(false);
            noQueiro.SetActive(false);
            GameManager.Instance.deniedFlor = true;
            GameManager.Instance.lastChallengeType = ChallengeType.FaltaEnvido;
            PhotonNetwork.RaiseEvent(QUEIRO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
        }
        else
        {
            TrucoGameplayAudio.PlayLocalRaise(FALTAENVIDO_CHALLENGE);
            // This block of code invokes the Falta Envido Challenge for other player
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            conFlorQuiero.SetActive(false);
            contraFlor.SetActive(false);
            florChica.SetActive(false);
            queiro.SetActive(false);
            noQueiro.SetActive(false);
            GameManager.Instance.deniedFlor = true;
            GameManager.Instance.lastChallengeType = ChallengeType.FaltaEnvido;
            // GameManager.Instance.challengePoints += 3;
            TrucoRulesScenarioLog.Ok("Local RAISE FaltaEnvido");
            PhotonNetwork.RaiseEvent(FALTAENVIDO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
        }
    }
    
    // This is called from UI Button To Call Quiero Challenge on other player
    public void ChallengeQueiro()
    {
        TrucoGameplayAudio.PlayLocalRaise(QUEIRO_CHALLENGE);
        _challengeAccepted = true;
        DisableButtons();
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Truco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            invokedChallenges.Add(ChallengeType.Truco);
            GameManager.Instance.mazoPoints = 2;
            trucoPlayed = true;
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Retruco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            invokedChallenges.Add(ChallengeType.Retruco);
            GameManager.Instance.mazoPoints = 3;
            trucoPlayed = true;
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Vale4))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            invokedChallenges.Add(ChallengeType.Vale4);
            GameManager.Instance.mazoPoints = 4;
            trucoPlayed = true;
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
            GameManager.Instance.ActiveChallenges.Clear();
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
            GameManager.Instance.ActiveChallenges.Clear();
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Flor))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.ContraFlor))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            GameManager.Instance.ShowAllCards();
        }
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        _isChallengepPending = false;
        TrucoRulesScenarioLog.Ok("Local RAISE Quiero (accept)",
            "accepting=" + GameManager.Instance.lastChallengeType + " mazoPts=" + GameManager.Instance.mazoPoints);
        PhotonNetwork.RaiseEvent(QUEIRO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
        // This block of code checks if there are any unanswered challenges
        if (unAnsweredChallenges.Count > 0)
        {
            StartCoroutine(CheckForUnansweredChallenges());
        }
        else if (GameManager.Instance.IsMyTurn())
        {
            Debug.LogWarning("Setting My turn Again");
            GameManager.Instance.SetCanPlayCard(true);
        }
    }
    
    // This is called from UI Button To Call NoQuiero Challenge on other player
    public void ChallengeNoQueiro()
    {
        TrucoGameplayAudio.PlayLocalRaise(NOQUEIRO_CHALLENGE);
        ChallengeType decliningType = GameManager.Instance.lastChallengeType;
        DisableButtons();
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Truco))
        {
            GameManager.Instance.challengePoints = 1;
            _cantChallenge = true;
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            trucoPlayed = true;
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Retruco))
        {
            GameManager.Instance.challengePoints = 2;
            _cantChallenge = true;
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            trucoPlayed = true;
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Vale4))
        {
            GameManager.Instance.challengePoints = 3;
            _cantChallenge = true;
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            trucoPlayed = true;
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
            GameManager.Instance.ActiveChallenges.Clear();
            _envidoPlayed = true; 
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
            GameManager.Instance.ActiveChallenges.Clear();
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
            GameManager.Instance.ActiveChallenges.Clear();
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Flor))
        {
            bool hasPendingTruco = unAnsweredChallenges.ContainsKey(ChallengeType.Truco);
            bool hasPendingRetruco = unAnsweredChallenges.ContainsKey(ChallengeType.Retruco);
            bool hasPendingVale4 = unAnsweredChallenges.ContainsKey(ChallengeType.Vale4);

            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);

            if (hasPendingTruco)
            {
                GameManager.Instance.lastChallengeType = ChallengeType.Truco;
                TrucoChallenged();
                BeginChallengeResponseCountdown();
            }
            else if (hasPendingRetruco)
            {
                GameManager.Instance.lastChallengeType = ChallengeType.Retruco;
                RetrucoChallenged();
                BeginChallengeResponseCountdown();
            }
            else if (hasPendingVale4)
            {
                GameManager.Instance.lastChallengeType = ChallengeType.Vale4;
                Vale4Challenged();
                BeginChallengeResponseCountdown();
            }
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.ContraFlor))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
        }
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        GameManager.Instance.noQuieroPoints = 0;
        _isChallengepPending = false;
        byte typePayload = (byte)decliningType;
        TrucoRulesScenarioLog.Ok("Local RAISE NoQuiero (decline)",
            "declining=" + decliningType + " awardViaMaster=" + TrucoRulePoints.NoQuieroAward(decliningType)
            + " endsHand=" + TrucoRulePoints.NoQuieroEndsHand(decliningType));
        TrucoPunChallenges.RaiseToAll(NOQUEIRO_CHALLENGE, typePayload);
        if (unAnsweredChallenges.Count > 0)
        {
            StartCoroutine(CheckForUnansweredChallenges());
        }
        else if (GameManager.Instance.IsMyTurn())
        {
            if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido) ||
                GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido) ||
                GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
            {
                if (!invokedChallenges.Contains(ChallengeType.Truco))
                {
                    truco.SetActive(true);
                    mazo.SetActive(true);
                }
            }
            Debug.LogWarning("Setting My turn Again");
            GameManager.Instance.SetCanPlayCard(true);
        }
        else
        {
            DisableButtons();
        }
        GameManager.Instance.challengePoints = 0;

    }
    
    // This is called from UI Button To Call Flor Challenge on other player

    public void ChallengeFlor()
    {
         _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Flor);
        // This block of code checks if other player has already denied FLor and gives local player points
        if (GameManager.Instance.otherPlayerDeniedFlor)
        {
            TrucoGameplayAudio.PlayLocalRaise(FLOR_CHALLENGE);
            flor.SetActive(false);
            conFlorQuiero.SetActive(false);
            contraFlor.SetActive(false);
            florChica.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            
            // Check if there's a pending TRUCO challenge before awarding FLOR points
            bool hasPendingTruco = unAnsweredChallenges.ContainsKey(ChallengeType.Truco);
            bool hasPendingRetruco = unAnsweredChallenges.ContainsKey(ChallengeType.Retruco);
            bool hasPendingVale4 = unAnsweredChallenges.ContainsKey(ChallengeType.Vale4);

            TrucoPunChallenges.RaiseToAll(FLOR_CHALLENGE, FlorAutoAwardFlag);
            TrucoRulesScenarioLog.Ok("Local RAISE Flor AUTO-AWARD (+3)", "rivalDeniedFlor=true");
            if (hasPendingTruco)
            {
                GameManager.Instance.lastChallengeType = ChallengeType.Truco;
                TrucoChallenged();
                BeginChallengeResponseCountdown();
            }
            else if (hasPendingRetruco)
            {
                GameManager.Instance.lastChallengeType = ChallengeType.Retruco;
                RetrucoChallenged();
                BeginChallengeResponseCountdown();
            }
            else if (hasPendingVale4)
            {
                GameManager.Instance.lastChallengeType = ChallengeType.Vale4;
                Vale4Challenged();
                BeginChallengeResponseCountdown();
            }
            else if (GameManager.Instance.IsMyTurn())
            {
                GameManager.Instance.SetCanPlayCard(true);
            }
            else
            {
                DisableButtons();
            }
            return;
        }
        TrucoGameplayAudio.PlayLocalRaise(FLOR_CHALLENGE);
        GameManager.Instance.noQuieroPoints = 1;
        DisableButtons();
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        GameManager.Instance.lastChallengeType = ChallengeType.Flor;
        TrucoRulesScenarioLog.Ok("Local RAISE Flor (challenge rival)");
        TrucoPunChallenges.RaiseToOthers(FLOR_CHALLENGE, null);
    }

    public void ChallengeFlorChica()
    {
        TrucoGameplayAudio.PlayLocalRaise(FLOR_CHICA_CHALLENGE);
        _isChallengepPending = false;
        invokedChallenges.Add(ChallengeType.Flor);
        DisableButtons();
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        GameManager.Instance.lastChallengeType = ChallengeType.FlorChica;
        TrucoRulesScenarioLog.Ok("Local RAISE FlorChica (rival +4)");
        TrucoPunChallenges.RaiseToAll(FLOR_CHICA_CHALLENGE, null);
        if (GameManager.Instance.IsMyTurn())
        {
            GameManager.Instance.SetCanPlayCard(true);
        }
    }
    
    // This is called from UI Button To Call Con FLor Quiero Challenge on other player

    public void ChallengeConFlorQuiero()
    {
        TrucoGameplayAudio.PlayLocalRaise(CON_FLOR_QUIERO_CHALLENGE);
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Flor);
        invokedChallenges.Add(ChallengeType.ConFlorQuiero);
        DisableButtons();
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        GameManager.Instance.lastChallengeType = ChallengeType.ConFlorQuiero;
        GameManager.Instance.challengePoints = 5;
        TrucoRulesScenarioLog.Ok("Local RAISE ConFlorQuiero", "challengePts=5");
        PhotonNetwork.RaiseEvent(CON_FLOR_QUIERO_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
        if (GameManager.Instance.IsMyTurn())
        {
            GameManager.Instance.SetCanPlayCard(true);
        }
    }
    
    // This is called from UI Button To Call Contra Flor Challenge on other player

    public void ChallengeContraFlor()
    {
        TrucoGameplayAudio.PlayLocalRaise(CONTRA_FLOR_CHALLENGE);
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Flor);
        DisableButtons();
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        queiro.SetActive(false);
        GameManager.Instance.noQuieroPoints = 1;
        noQueiro.SetActive(false);
        GameManager.Instance.lastChallengeType = ChallengeType.ContraFlor;
        TrucoRulesScenarioLog.Ok("Local RAISE ContraFlor");
        PhotonNetwork.RaiseEvent(CONTRA_FLOR_CHALLENGE, null, RaiseEventOptions.Default, SendOptions.SendReliable);
        // if (GameManager.Instance.IsMyTurn())
        // {
            GameManager.Instance.SetCanPlayCard(false);
        // }
    }
    
    // This is called from UI Button To Call Mazo Challenge on other player

    public void ChallengeMazo()
    {
        if (GameManager.Instance == null || GameManager.Instance._gameEnded) return;
        if (!GameManager.Instance.IsMyTurn()) return;

        GameManager.Instance.SetCanPlayCard(false);
        DisableButtons();

        if (GameManager.Instance.HandResolved)
        {
            TrucoRulesScenarioLog.Ok("ChallengeMazo skipped (hand already resolved) → sync only");
            GameManager.Instance.RequestStateSyncAfterReconnect();
            return;
        }

        TrucoGameplayAudio.PlayLocalRaise(MAZO_CHALLENGE);
        TrucoRulesScenarioLog.Ok("Local RAISE Mazo (fold hand)",
            "cardPlayed=" + GameManager.Instance.cardPlayed
            + " trucoPlayed=" + trucoPlayed);
        TrucoPunChallenges.RaiseToAll(MAZO_CHALLENGE, null);
    }

    // This is called When Other player Challenges us with this Challenge

    public void TrucoChallenged()
    {
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Truco);
        _cantChallenge = false;
        if (PhotonNetwork.PlayerListOthers.Length > 0)
            unAnsweredChallenges[ChallengeType.Truco] = PhotonNetwork.PlayerListOthers[0].ActorNumber;
        truco.SetActive(false);
        queiro.SetActive(true);
        noQueiro.SetActive(true);
        retruco.SetActive(true);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        GameManager.Instance.mazoPoints = 1;
        GameManager.Instance.noQuieroPoints = 1;
        GameManager.Instance.challengePoints = 1;
        if (!invokedChallenges.Contains(ChallengeType.Envido) && !invokedChallenges.Contains(ChallengeType.Flor) && 
            !GameManager.Instance.cardPlayed)
        {
            envido.SetActive(true);
            realEnvido.SetActive(true);
            faltaEnvido.SetActive(true);
            if (GameManager.Instance.PlayerHasFlor())
            {
                flor.SetActive(true);
            }
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void RetrucoChallenged()
    {
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Retruco);
        _cantChallenge = false;
        truco.SetActive(false);
        retruco.SetActive(false);
        queiro.SetActive(true);
        noQueiro.SetActive(true);
        vale4.SetActive(true);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        unAnsweredChallenges.Add(ChallengeType.Retruco,PhotonNetwork.PlayerListOthers[0].ActorNumber);
        GameManager.Instance.mazoPoints = 2;
        GameManager.Instance.noQuieroPoints = 2;
        GameManager.Instance.cardPlayed = true;
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void Vale4Challenged()
    {
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Vale4);
        _cantChallenge = false;
        truco.SetActive(false);
        retruco.SetActive(false);
        queiro.SetActive(true);
        noQueiro.SetActive(true);
        vale4.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        unAnsweredChallenges.Add(ChallengeType.Vale4,PhotonNetwork.PlayerListOthers[0].ActorNumber);
        GameManager.Instance.mazoPoints = 3;
        GameManager.Instance.noQuieroPoints = 3;
        GameManager.Instance.cardPlayed = true;
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void EnvidoChallenged()
    {
        _isChallengepPending = true;
        GameManager.Instance.ActiveChallenges.Add(ChallengeType.Envido);
        // This block of code checks if there is a double envido is in play
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            _isDoubleEnvido = true;
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(true);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            conFlorQuiero.SetActive(false);
            florChica.SetActive(false);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            mazo.SetActive(false);
        }else 
        {
            envido.SetActive(true);
            realEnvido.SetActive(true);
            faltaEnvido.SetActive(true);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            conFlorQuiero.SetActive(false);
            florChica.SetActive(false);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            if (GameManager.Instance.lastChallengeType == ChallengeType.Truco)
            {
                mazo.SetActive(false);
            }
            else
            {
                mazo.SetActive(true);
            }

            if (GameManager.Instance.PlayerHasFlor())
            {
                flor.SetActive(true);
            }
            
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void RealEnvidoChallenged()
    {
        _isChallengepPending = true;
        GameManager.Instance.ActiveChallenges.Add(ChallengeType.RealEnvido);
        // This block of code checks if there is Double Real Envido in play
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido))
        {
            _isDoubleEnvido = false;
            _isDoubleRealEnvido = true;
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            conFlorQuiero.SetActive(false);
            florChica.SetActive(false);
        }
        else if(GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            conFlorQuiero.SetActive(false);
            florChica.SetActive(false);
        }
        else
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            realEnvido.SetActive(true);
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            conFlorQuiero.SetActive(false);
            florChica.SetActive(false);
            if (GameManager.Instance.lastChallengeType == ChallengeType.Truco)
            {
                mazo.SetActive(false);
            }
            else
            {
                mazo.SetActive(true);
            }
            if (GameManager.Instance.PlayerHasFlor())
            {
                flor.SetActive(true);
            }
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void FaltaEnvidoChallenged()
    {
        _isChallengepPending = true;
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        queiro.SetActive(true);
        noQueiro.SetActive(true);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        if (GameManager.Instance.PlayerHasFlor() && !invokedChallenges.Contains(ChallengeType.Flor) 
                                                 && !invokedChallenges.Contains(ChallengeType.Envido)
                                                 && !invokedChallenges.Contains(ChallengeType.Truco))
        {
            flor.SetActive(true);
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void FlorChallenged()
    {
        _isChallengepPending = true;
        envido.SetActive(false);
        realEnvido.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(true);
        florChica.SetActive(true);
        contraFlor.SetActive(true);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
    }

    // This is called When Other player Challenges us with this Challenge
    public void FlorChicaChallenged()
    {
        _isChallengepPending = false;
        envido.SetActive(false);
        realEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void ConFlorQuieroChallenged()
    {
        _isChallengepPending = false;
        invokedChallenges.Add(ChallengeType.ConFlorQuiero);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void ContraFlorChallenged()
    {
        _isChallengepPending = true;
        envido.SetActive(false);
        realEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(true);
        queiro.SetActive(true);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        // GameManager.Instance.ShowAllCards();
    }
    
    // This block of code is called when the player accepts the challenge
    public void QueiroChallenged()
    {
        _isChallengepPending = false;
        _cantChallenge = false;
        _challengeAccepted = true;
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Truco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            GameManager.Instance.mazoPoints = 2;
            unAnsweredChallenges.Clear();
            invokedChallenges.Add(ChallengeType.Truco);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Retruco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            GameManager.Instance.mazoPoints = 3;
            unAnsweredChallenges.Clear();
            invokedChallenges.Add(ChallengeType.Retruco);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Vale4))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            GameManager.Instance.mazoPoints = 4;
            unAnsweredChallenges.Clear();
            invokedChallenges.Add(ChallengeType.Vale4);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            _envidoPlayed = true;
            GameManager.Instance.ActiveChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            _envidoPlayed = true;
            GameManager.Instance.ActiveChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Flor))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.ContraFlor))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
        }

        // if (GameManager.Instance.IsMyTurn())
        // {
        //     GameManager.Instance.SetCanPlayCard(true);
        // }
        _cantChallenge = false;
        CancelChallengeResponseCountdown();
        TurnManager.Instance?.RestartTurnTimersIfActive();
        if (GameManager.Instance.IsMyTurn())
            GameManager.Instance.SetCanPlayCard(true);
    }

    // This block of code is called when the player does not accept the challenge
    public void NoQueiroChallenged()
    {
        _isChallengepPending = false;
        _cantChallenge = false;
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Truco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            DisableButtons();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Retruco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            DisableButtons();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
            _envidoPlayed = true;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Flor))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.ContraFlor))
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
            flor.SetActive(false);
            contraFlor.SetActive(false);
        }

        GameManager.Instance.noQuieroPoints = 0;
        if (unAnsweredChallenges.Count > 0)
        {
            StartCoroutine(CheckForUnansweredChallenges());
        }
        else if (GameManager.Instance.IsMyTurn())
        {
            GameManager.Instance.SetCanPlayCard(true);
        }
        else
        {
            DisableButtons();
        }

        GameManager.Instance.challengePoints = 0;
        CancelChallengeResponseCountdown();
        TurnManager.Instance?.RestartTurnTimersIfActive();
    }

    
    // This block of code checks for any unanswered challenges after a delay
    public IEnumerator CheckForUnansweredChallenges()
    {
        yield return new WaitForSeconds(3);
        ChallengeType lastChallenge = unAnsweredChallenges.Last().Key;
        int playerActorNumber = unAnsweredChallenges.Last().Value;

        switch (lastChallenge)
        {
            case ChallengeType.Truco:
            {
                unAnsweredChallenges.Clear();
                GameManager.Instance.lastChallengeType = ChallengeType.Truco;
                if (playerActorNumber.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
                {
                    PhotonNetwork.RaiseEvent(TRUCO_CHALLENGE,null, RaiseEventOptions.Default, SendOptions.SendReliable);
                }
                else
                {
                    TrucoChallenged();
                    BeginChallengeResponseCountdown();
                }
                break;
            }
        }
    }
    
}