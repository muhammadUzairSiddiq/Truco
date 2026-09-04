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
    [Tooltip("Creada en runtime si no existe: panel detrÃ¡s del texto de turno (no bloquea clics).")]
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
    /// <summary>Flor already called this hand â€” button stays off until next deal.</summary>
    public bool _florPlayed = false;
    public bool trucoPlayed = false;
    /// <summary>Actor who last raised Truco/Retruco/Vale4 â€” opponent alone may raise the next level.</summary>
    public int LastTrucoRaiseActor { get; private set; }

    public Dictionary<ChallengeType,int> unAnsweredChallenges = new Dictionary<ChallengeType, int>();
    private bool _cantChallenge = false;
    private int _myDisplayCardIndex = 0;
    private int _otherPlayersDisplayCardIndex = 0;
    readonly List<Vector3> _myCardsInitialPositions = new List<Vector3>();
    private bool _challengeAccepted = false;
    private bool _isDoubleEnvido = false;
    private bool _isDoubleRealEnvido = false;
    public bool _isChallengepPending = false;
    private bool _autoFlorQueued = false;

    /// <summary>True only on the player who currently must answer a canto (truco/envido/flor).</summary>
    public bool _iOweChallengeResponse = false;
    private Coroutine _challengeResponseRoutine;
    const float ChallengeResponseSeconds = 30f;
    float _challengeSentAt = -1f;

    double _challengeDeadlinePhoton = -1;

    public double MarkChallengeSentNow()
    {
        _challengeSentAt = Time.time;
        _challengeDeadlinePhoton = PhotonNetwork.Time + ChallengeResponseSeconds;
        return _challengeDeadlinePhoton;
    }

    public void ApplySharedChallengeDeadline(double deadlinePhoton)
    {
        if (deadlinePhoton > 0)
            _challengeDeadlinePhoton = deadlinePhoton;
        else
            MarkChallengeSentNow();
        _challengeSentAt = Time.time;
    }

    public double GetChallengeDeadlinePhoton() => _challengeDeadlinePhoton;

    public int GetChallengeResponseSecondsRemaining()
    {
        if (_challengeDeadlinePhoton > 0)
            return Mathf.Max(0, Mathf.CeilToInt((float)(_challengeDeadlinePhoton - PhotonNetwork.Time)));
        if (_challengeSentAt < 0f) return Mathf.CeilToInt(ChallengeResponseSeconds);
        return Mathf.Max(0, Mathf.CeilToInt(ChallengeResponseSeconds - (Time.time - _challengeSentAt)));
    }

    /// <summary>Clears canto state when a hand ends so the next deal starts clean.</summary>
    public void ResetHandChallengeState()
    {
        _challengeSentAt = -1f;
        _challengeDeadlinePhoton = -1;
        _scoringRevealMyCount = 0;
        _scoringRevealOtherCount = 0;
        _isChallengepPending = false;
        _iOweChallengeResponse = false;
        _cantChallenge = false;
        _challengeAccepted = false;
        trucoPlayed = false;
        _envidoPlayed = false;
        _florPlayed = false;
        _autoFlorQueued = false;
        LastTrucoRaiseActor = 0;
        unAnsweredChallenges.Clear();
        invokedChallenges.Clear();
        CancelChallengeResponseCountdown();
    }

    public void MarkTrucoRaiseByLocal()
    {
        if (PhotonNetwork.LocalPlayer != null)
            LastTrucoRaiseActor = PhotonNetwork.LocalPlayer.ActorNumber;
    }

    public void MarkTrucoRaiseByActor(int actorNumber)
    {
        if (actorNumber > 0)
            LastTrucoRaiseActor = actorNumber;
    }

    /// <summary>True if local may press Retruco/Vale4 (must not be the player who made the last raise).</summary>
    bool CanRaiseNextTrucoLevel()
    {
        if (PhotonNetwork.LocalPlayer == null) return false;
        if (LastTrucoRaiseActor <= 0) return true;
        return PhotonNetwork.LocalPlayer.ActorNumber != LastTrucoRaiseActor;
    }

    /// <summary>
    /// Rebuild canto UI after reconnect from peer snapshot. Does not change scores.
    /// </summary>
    public void RestoreChallengeStateFromSync(ChallengeType type, bool pending, int raiserActor,
        int responderActor, int trucoLevel, bool envidoPlayed, int lastTrucoRaiseActor = 0)
    {
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "RestoreChallengeStateFromSync type=" + type + " pending=" + pending
            + " raiser=" + raiserActor + " responder=" + responderActor
            + " trucoLv=" + trucoLevel + " envido=" + envidoPlayed
            + " lastRaise=" + lastTrucoRaiseActor
            + " local=" + PhotonNetwork.LocalPlayer.ActorNumber);
        _envidoPlayed = envidoPlayed;
        trucoPlayed = trucoLevel > 0;
        if (lastTrucoRaiseActor > 0)
            LastTrucoRaiseActor = lastTrucoRaiseActor;
        else if (pending && raiserActor > 0
                 && (type == ChallengeType.Truco || type == ChallengeType.Retruco || type == ChallengeType.Vale4))
            LastTrucoRaiseActor = raiserActor;
        else if (trucoLevel == 0)
            LastTrucoRaiseActor = 0;
        invokedChallenges.Clear();
        if (trucoLevel >= 1) invokedChallenges.Add(ChallengeType.Truco);
        if (trucoLevel >= 2) invokedChallenges.Add(ChallengeType.Retruco);
        if (trucoLevel >= 3) invokedChallenges.Add(ChallengeType.Vale4);
        if (envidoPlayed)
        {
            invokedChallenges.Add(ChallengeType.Envido);
            invokedChallenges.Add(ChallengeType.RealEnvido);
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
        }
        // Flor locks Envido for the hand after reconnect too.
        if (type == ChallengeType.Flor || type == ChallengeType.ContraFlor
            || type == ChallengeType.ConFlorQuiero || type == ChallengeType.FlorChica)
            invokedChallenges.Add(ChallengeType.Flor);
        unAnsweredChallenges.Clear();
        _isChallengepPending = pending;
        if (!pending || type == ChallengeType.None || responderActor <= 0)
        {
            CancelChallengeResponseCountdown();
            return;
        }
        bool florFamily = type == ChallengeType.Flor || type == ChallengeType.ContraFlor
                          || type == ChallengeType.ConFlorQuiero || type == ChallengeType.FlorChica;
        if (florFamily && GameManager.Instance != null
            && (GameManager.Instance.deniedFlor || _florPlayed || GameManager.Instance.florResolvedThisHand))
        {
            // Snapshot of the rival's in-flight Flor canto: this seat has no Flor (or already closed the
            // Flor phase), so the live FLOR event resolves it as auto +3 — never rebuild Flor response buttons.
            TrucoRulesScenarioLog.Ok("RestoreChallenge Flor skipped",
                "type=" + type + " deniedFlor=" + GameManager.Instance.deniedFlor + " florPlayed=" + _florPlayed);
            _isChallengepPending = false;
            CancelChallengeResponseCountdown();
            if (!GameManager.Instance.IsMyTurn()) DisableButtons();
            return;
        }
        if (raiserActor > 0)
            unAnsweredChallenges[type] = raiserActor;
        if (GameManager.Instance != null)
            GameManager.Instance.lastChallengeType = type;
        bool iRespond = responderActor == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!iRespond)
        {
            CancelChallengeResponseCountdown();
            DisableButtons();
            UpdateTurnText(TrucoTextosClient.FormatoBannerEsperandoRival(TrucoTextosClient.EsperandoRespuestaRival), -1f);
            return;
        }
        switch (type)
        {
            case ChallengeType.Truco: TrucoChallenged(); break;
            case ChallengeType.Retruco: RetrucoChallenged(); break;
            case ChallengeType.Vale4: Vale4Challenged(); break;
            case ChallengeType.Envido: EnvidoChallenged(); break;
            case ChallengeType.RealEnvido: RealEnvidoChallenged(); break;
            case ChallengeType.FaltaEnvido: FaltaEnvidoChallenged(); break;
            case ChallengeType.Flor: FlorChallenged(); break;
            case ChallengeType.ContraFlor: ContraFlorChallenged(); break;
            case ChallengeType.FlorChica: FlorChicaChallenged(); break;
            case ChallengeType.ConFlorQuiero: ConFlorQuieroChallenged(); break;
            default:
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon, "RestoreChallenge unknown type=" + type);
                break;
        }
        BeginChallengeResponseCountdown();
        GameManager.Instance?.SetCanPlayCard(false);
    }

    public void ClearTurnBanner() => DisableTurnText();

    /// <summary>Start the 30 s response countdown on the player who received a canto. Auto-declines on timeout (anti-freeze).</summary>
    public void BeginChallengeResponseCountdown(double sharedDeadlinePhoton = -1)
    {
        if (SpectatorContext.IsSpectator) return;
        ApplySharedChallengeDeadline(sharedDeadlinePhoton);
        _iOweChallengeResponse = true;
        if (_challengeResponseRoutine != null) StopCoroutine(_challengeResponseRoutine);
        TrucoRulesScenarioLog.Ok("ChallengeResponseTimer START",
            "window=" + ChallengeResponseSeconds + "s photonDeadline=" + _challengeDeadlinePhoton
            + " last=" + (GameManager.Instance != null
                ? GameManager.Instance.lastChallengeType.ToString() : "?"));
        _challengeResponseRoutine = StartCoroutine(ChallengeResponseCountdown());
    }

    /// <summary>Cancel the response countdown (called whenever this player takes any action / buttons disabled).</summary>
    public void CancelChallengeResponseCountdown()
    {
        _iOweChallengeResponse = false;
        if (_challengeResponseRoutine != null) { StopCoroutine(_challengeResponseRoutine); _challengeResponseRoutine = null; }
    }

    /// <summary>After Quiero/NoQuiero closes a canto â€” clear pending flags and restart turn clocks on BOTH seats.</summary>
    void FinishChallengeUiAndRestartTimers(string reason, double syncStartedAt = -1)
    {
        if (GameManager.Instance != null
            && (GameManager.Instance.HandResolved || GameManager.Instance.IsMatchEndPending()
                || GameManager.Instance._gameEnded))
        {
            DisableButtons();
            CancelChallengeResponseCountdown();
            return;
        }
        // Keep pending when a stacked canto still waits (e.g. Truco after Envido No quiero).
        if (unAnsweredChallenges.Count == 0)
            _isChallengepPending = false;
        CancelChallengeResponseCountdown();
        TrucoRulesScenarioLog.Ok("ChallengeClosed â†’ restart timers",
            "reason=" + reason
            + " unanswered=" + unAnsweredChallenges.Count
            + " pending=" + _isChallengepPending
            + " syncAt=" + syncStartedAt
            + " myTurn=" + (GameManager.Instance != null && GameManager.Instance.IsMyTurn()));
        // Do not restart turn clocks while a stacked Truco still needs a response.
        if (PendingTrucoChainType() != ChallengeType.None)
            return;
        TurnManager.Instance?.RestartTurnTimersIfActive(force: true, startedAt: syncStartedAt);
    }

    /// <summary>True when any canto is still unresolved on this client.</summary>
    bool HasUnresolvedChallengeState()
    {
        return _isChallengepPending || unAnsweredChallenges.Count > 0;
    }

    System.Collections.IEnumerator ChallengeResponseCountdown()
    {
        while (true)
        {
            if (!_iOweChallengeResponse) { _challengeResponseRoutine = null; yield break; }
            if (GameManager.Instance != null && GameManager.Instance._gameEnded) { _challengeResponseRoutine = null; yield break; }
            int sec = GetChallengeResponseSecondsRemaining();
            bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
            UpdateTurnText(TrucoTextosClient.FormatoBannerResponderConSegundos(sec), -1f, urgent);
            if (sec <= 0) break;
            yield return null;
        }
        _challengeResponseRoutine = null;
        if (!_iOweChallengeResponse) yield break;
        if (GameManager.Instance != null && GameManager.Instance._gameEnded) yield break;
        _iOweChallengeResponse = false;
        TrucoRulesScenarioLog.Ok("ChallengeResponseTimeout â†’ auto NoQuiero",
            "window=" + ChallengeResponseSeconds + "s last=" + (GameManager.Instance != null
                ? GameManager.Instance.lastChallengeType.ToString() : "?"));
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.TiempoRespuestaAgotado);
        // Safe auto-resolution: decline the canto so the hand never freezes.
        ChallengeNoQueiro();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€ Visual callout banner: canto/declaration â€” sits BELOW the turn status lane â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private GameObject _calloutGo;
    private TMPro.TMP_Text _calloutTmp;
    private RectTransform _calloutRt;

    /// <summary>Show a short banner with the canto/declaration so BOTH players can see it (not only hear it).</summary>
    public void ShowChallengeCallout(string phrase, bool mine)
    {
        if (string.IsNullOrEmpty(phrase)) return;
        EnsureCalloutLabel();
        if (_calloutTmp == null || _calloutGo == null) return;
        LayoutCalloutForSpeaker(mine);
        string who = mine
            ? (TrucoLocalization.IsEnglish ? "You" : "Vos")
            : (TrucoLocalization.IsEnglish ? "Rival" : "Rival");
        string color = mine ? "#7CFC9B" : "#FFC24A";
        _calloutTmp.text = $"<color={color}><b>{who}</b></color>  Â·  {phrase}";
        _calloutGo.SetActive(true);
        _calloutGo.transform.SetAsLastSibling();
        CancelInvoke(nameof(HideCallout));
        Invoke(nameof(HideCallout), 2.2f);
    }

    /// <summary>Local announcements near your hand (bottom); rival near their cards (top).</summary>
    void LayoutCalloutForSpeaker(bool mine)
    {
        if (_calloutRt == null) return;
        if (mine)
        {
            _calloutRt.anchorMin = new Vector2(0.5f, 0f);
            _calloutRt.anchorMax = new Vector2(0.5f, 0f);
            _calloutRt.pivot = new Vector2(0.5f, 0f);
            _calloutRt.anchoredPosition = new Vector2(0f, 210f);
        }
        else
        {
            _calloutRt.anchorMin = new Vector2(0.5f, 1f);
            _calloutRt.anchorMax = new Vector2(0.5f, 1f);
            _calloutRt.pivot = new Vector2(0.5f, 1f);
            _calloutRt.anchoredPosition = new Vector2(0f, -168f);
        }
        _calloutRt.sizeDelta = new Vector2(620f, 56f);
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
        _calloutRt = _calloutGo.GetComponent<RectTransform>();
        _calloutRt.anchorMin = new Vector2(0.5f, 1f);
        _calloutRt.anchorMax = new Vector2(0.5f, 1f);
        _calloutRt.pivot = new Vector2(0.5f, 1f);
        _calloutRt.anchoredPosition = new Vector2(0f, -210f);
        _calloutRt.sizeDelta = new Vector2(620f, 56f);
        var bg = _calloutGo.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.08f, 0.82f);
        bg.raycastTarget = false;
        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(_calloutGo.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 6f);
        trt.offsetMax = new Vector2(-16f, -6f);
        _calloutTmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        _calloutTmp.alignment = TMPro.TextAlignmentOptions.Center;
        _calloutTmp.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
        _calloutTmp.raycastTarget = false;
        _calloutTmp.enableAutoSizing = true;
        _calloutTmp.fontSizeMin = 16f;
        _calloutTmp.fontSizeMax = 26f;
        _calloutTmp.enableWordWrapping = true;
        _calloutTmp.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        _calloutTmp.color = Color.white;
        _calloutGo.SetActive(false);
    }

    /// <summary>Timer beside the acting seat: local hand (bottom) vs rival (top).</summary>
    void LayoutStatusBanners(bool? localMustAct = null)
    {
        bool mine = localMustAct ?? (GameManager.Instance != null &&
                                     (GameManager.Instance.IsMyTurn() || _iOweChallengeResponse));
        if (turnText != null)
        {
            var textRt = turnText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                if (mine)
                {
                    textRt.anchorMin = new Vector2(0.5f, 0f);
                    textRt.anchorMax = new Vector2(0.5f, 0f);
                    textRt.pivot = new Vector2(0.5f, 0f);
                    textRt.anchoredPosition = new Vector2(0f, 280f);
                }
                else
                {
                    textRt.anchorMin = new Vector2(0.5f, 1f);
                    textRt.anchorMax = new Vector2(0.5f, 1f);
                    textRt.pivot = new Vector2(0.5f, 1f);
                    textRt.anchoredPosition = new Vector2(0f, -56f);
                }
                textRt.sizeDelta = new Vector2(640f, 96f);
            }
        }
        if (turnTextBackground != null)
        {
            turnTextBackground.anchorMin = turnText != null
                ? turnText.GetComponent<RectTransform>().anchorMin
                : new Vector2(0.5f, 1f);
            turnTextBackground.anchorMax = turnText != null
                ? turnText.GetComponent<RectTransform>().anchorMax
                : new Vector2(0.5f, 1f);
            turnTextBackground.pivot = turnText != null
                ? turnText.GetComponent<RectTransform>().pivot
                : new Vector2(0.5f, 1f);
            turnTextBackground.anchoredPosition = turnText != null
                ? turnText.GetComponent<RectTransform>().anchoredPosition
                : new Vector2(0f, -56f);
        }
    }

    /// <summary>Re-apply face sprites on local hand (disconnect overlay / layout must not leave blank white cards).</summary>
    public void EnsureLocalHandSpritesVisible()
    {
        if (myPlayerCards == null) return;
        for (int i = 0; i < myPlayerCards.Count; i++)
        {
            var go = myPlayerCards[i];
            if (go == null) continue;
            var card = go.GetComponent<Card>();
            if (card == null || card.value <= 0) continue;
            card.SetupCard(card.suit, card.value);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = Color.white;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            go.SetActive(true);
        }
    }

    /// <summary>Re-stamp opponent reveal/dummy card faces after Flor/Envido show.</summary>
    public void EnsureOpponentRevealSpritesVisible()
    {
        if (otherPlayersCards == null) return;
        for (int i = 0; i < otherPlayersCards.Count; i++)
        {
            var go = otherPlayersCards[i];
            if (go == null) continue;
            var card = go.GetComponent<Card>();
            if (card == null || card.value <= 0) continue;
            card.SetupCard(card.suit, card.value);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = Color.white;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            go.SetActive(true);
        }
    }

    /// <summary>Destination of the last LeanMove per card, so a cancelled tween can snap to its seat.</summary>
    readonly Dictionary<GameObject, Vector3> _cardMoveTargets = new Dictionary<GameObject, Vector3>();

    void MoveCardTo(Transform cardTf, Vector3 target, float seconds)
    {
        if (cardTf == null) return;
        LeanTween.cancel(cardTf.gameObject);
        _cardMoveTargets[cardTf.gameObject] = target;
        cardTf.LeanMove(target, seconds).setEaseInOutCubic();
    }

    /// <summary>Stop LeanMoves and keep faces painted so NuevaMano never flashes blank whites.</summary>
    public void FreezeTableCardsForHandTransition()
    {
        CancelLeanOnCards(myPlayerCards);
        CancelLeanOnCards(otherPlayersCards);
        EnsureLocalHandSpritesVisible();
        EnsureOpponentRevealSpritesVisible();
    }

    /// <summary>Cancel in-flight moves but land each card on its table seat — never leave it half-way out of the hand.</summary>
    void CancelLeanOnCards(List<GameObject> cards)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            var go = cards[i];
            if (go == null) continue;
            bool moving = LeanTween.isTweening(go);
            LeanTween.cancel(go);
            if (moving && _cardMoveTargets.TryGetValue(go, out Vector3 target))
                go.transform.position = target;
        }
    }

    /// <summary>
    /// Rebuild hand + table from master authority after reconnect.
    /// Prevents re-dealt 3-card hands and duplicate plays.
    /// </summary>
    public void RebuildHandAndTricksFromSync(
        List<DeckCards> localDeal,
        List<ScoreClass> myPlayed,
        List<ScoreClass> oppPlayed)
    {
        if (myPlayerCards == null) return;
        _myDisplayCardIndex = 0;
        _otherPlayersDisplayCardIndex = 0;
        CancelLeanOnCards(myPlayerCards);
        CancelLeanOnCards(otherPlayersCards);
        _cardMoveTargets.Clear();

        // Reset opponent dummy cards to hand seats.
        if (otherPlayersCards != null && otherPlayersCardsInitialPositions != null)
        {
            for (int i = 0; i < otherPlayersCards.Count; i++)
            {
                if (otherPlayersCards[i] == null) continue;
                if (i < otherPlayersCardsInitialPositions.Count)
                    otherPlayersCards[i].transform.position = otherPlayersCardsInitialPositions[i];
                var oppCard = otherPlayersCards[i].GetComponent<Card>();
                if (oppCard != null) oppCard.SetSelectable(false);
            }
        }

        // Reset local hand seats, then place remaining vs played.
        var remaining = new List<DeckCards>();
        if (localDeal != null)
        {
            var playedMask = new bool[localDeal.Count];
            if (myPlayed != null)
            {
                for (int p = 0; p < myPlayed.Count; p++)
                {
                    for (int d = 0; d < localDeal.Count; d++)
                    {
                        if (playedMask[d]) continue;
                        if (localDeal[d].suit == myPlayed[p].suit && localDeal[d].rank == myPlayed[p].value)
                        {
                            playedMask[d] = true;
                            break;
                        }
                    }
                }
            }
            for (int d = 0; d < localDeal.Count; d++)
            {
                if (!playedMask[d]) remaining.Add(localDeal[d]);
            }
        }

        for (int i = 0; i < myPlayerCards.Count; i++)
        {
            var go = myPlayerCards[i];
            if (go == null) continue;
            if (i < _myCardsInitialPositions.Count)
                go.transform.position = _myCardsInitialPositions[i];
            var card = go.GetComponent<Card>();
            if (card == null) continue;
            if (i < remaining.Count)
            {
                card.SetupCard(remaining[i].suit, remaining[i].rank);
                card.SetSelectable(true);
                go.SetActive(true);
            }
            else
            {
                card.SetSelectable(false);
                // Slot will be reused for a played card placement below, or stay empty.
                go.SetActive(false);
            }
        }

        // Place own played cards onto the table (instant).
        int slot = remaining.Count;
        if (myPlayed != null)
        {
            for (int p = 0; p < myPlayed.Count; p++)
            {
                GameObject go = null;
                if (slot < myPlayerCards.Count) go = myPlayerCards[slot++];
                if (go == null && myPlayerCards.Count > 0)
                    go = myPlayerCards[Mathf.Min(p, myPlayerCards.Count - 1)];
                if (go == null) continue;
                go.SetActive(true);
                var card = go.GetComponent<Card>();
                if (card != null)
                {
                    card.SetupCard(myPlayed[p].suit, myPlayed[p].value);
                    card.SetSelectable(false);
                }
                if (myDisplayCardsPosition != null && myDisplayCardsPosition.Count > 0)
                {
                    int idx = Mathf.Min(p, myDisplayCardsPosition.Count - 1);
                    PrepareTrickLayout(go.transform, true);
                    go.transform.position = myDisplayCardsPosition[idx].position;
                }
                _myDisplayCardIndex = p + 1;
            }
        }

        // Place opponent played faces.
        if (oppPlayed != null)
        {
            for (int p = 0; p < oppPlayed.Count; p++)
                SnapOtherPlayersCard(oppPlayed[p].suit, oppPlayed[p].value);
        }

        EnsureLocalHandSpritesVisible();
        TrucoRulesScenarioLog.Ok("RebuildHandAndTricksFromSync",
            "remain=" + remaining.Count
            + " myPlayed=" + (myPlayed != null ? myPlayed.Count : 0)
            + " oppPlayed=" + (oppPlayed != null ? oppPlayed.Count : 0));
    }

    void SnapOtherPlayersCard(CardSuit suit, int value)
    {
        if (otherPlayersCards == null || _otherPlayersDisplayCardIndex < 0
            || _otherPlayersDisplayCardIndex >= otherPlayersCards.Count) return;
        if (otherPlayersDisplayCardsPosition == null
            || _otherPlayersDisplayCardIndex >= otherPlayersDisplayCardsPosition.Count) return;
        var o = otherPlayersCards[_otherPlayersDisplayCardIndex].GetComponent<Card>();
        if (o == null) return;
        o.SetupCard(suit, value);
        o.SetSelectable(false);
        var t = o.transform;
        PrepareTrickLayout(t, true);
        t.position = otherPlayersDisplayCardsPosition[_otherPlayersDisplayCardIndex].position;
        _otherPlayersDisplayCardIndex++;
    }

    private void Awake()
    {
        Instance = this;
        // store Initial Positions of Cards
        for (int i = 0; i < otherPlayersCards.Count; i++)
        {
            otherPlayersCardsInitialPositions.Add(otherPlayersCards[i].transform.position);
        }
        if (myPlayerCards != null)
        {
            for (int i = 0; i < myPlayerCards.Count; i++)
            {
                if (myPlayerCards[i] != null)
                    _myCardsInitialPositions.Add(myPlayerCards[i].transform.position);
            }
        }
        if (turnText != null)
        {
            var tmp = turnText.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.raycastTarget = false;
                ConfigureTurnTextStyle(tmp);
            }
        }
        LayoutStatusBanners();
        EnsureTurnBannerBackground();
        if (!SpectatorContext.IsSpectator) EnsureMuteButton();
        // Scene buttons start active â€” force off until Turn RPC assigns mano (Pie must not flash).
        DisableButtons();
        // Hide undealt faces so NuevaMano scene load never flashes blank white Images.
        HideUndealtHandCards();
    }

    void HideUndealtHandCards()
    {
        SetCardListVisible(myPlayerCards, false);
        SetCardListVisible(otherPlayersCards, false);
    }

    public void ShowDealtHandCards()
    {
        SetCardListVisible(myPlayerCards, true);
        SetCardListVisible(otherPlayersCards, true);
        EnsureLocalHandSpritesVisible();
        TrucoSceneTransition.ReleaseCover();
    }

    static void SetCardListVisible(List<GameObject> cards, bool visible)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null) cards[i].SetActive(visible);
        }
    }

    /// <summary>True when local may raise a canto: own turn, or answering a received challenge (Envido over Truco, etc.).</summary>
    bool CanRaiseChallengeNow()
    {
        if (SpectatorContext.IsSpectator) return false;
        if (GameManager.Instance == null || GameManager.Instance._gameEnded || GameManager.Instance.HandResolved
            || GameManager.Instance.IsMatchEndPending())
            return false;
        return GameManager.Instance.IsMyTurn() || _iOweChallengeResponse;
    }

    bool ShouldAutoCallFlorNow()
    {
        if (!OneVsOneMatchSession.WithFlor || _autoFlorQueued) return false;
        if (GameManager.Instance == null || !CanRaiseChallengeNow()) return false;
        if (_florPlayed || invokedChallenges.Contains(ChallengeType.Flor)
                        || invokedChallenges.Contains(ChallengeType.ContraFlor)
                        || invokedChallenges.Contains(ChallengeType.ConFlorQuiero)
                        || invokedChallenges.Contains(ChallengeType.FlorChica))
            return false;
        if (_iOweChallengeResponse && (GameManager.Instance.lastChallengeType == ChallengeType.Flor
                                      || GameManager.Instance.lastChallengeType == ChallengeType.ContraFlor
                                      || GameManager.Instance.lastChallengeType == ChallengeType.ConFlorQuiero
                                      || GameManager.Instance.lastChallengeType == ChallengeType.FlorChica))
            return false;
        return GameManager.Instance.PlayerHasFlor();
    }

    void QueueAutoFlor(string reason)
    {
        if (!ShouldAutoCallFlorNow()) return;
        _autoFlorQueued = true;
        StartCoroutine(CoAutoFlor(reason));
    }

    IEnumerator CoAutoFlor(string reason)
    {
        // Give the peer's NoFlor RPC a short moment so +3 auto-award can resolve immediately.
        // Buttons stay disabled for this wait — never flash illegal actions.
        yield return new WaitForSeconds(0.15f);
        if (_florPlayed)
        {
            _autoFlorQueued = false;
            yield break;
        }
        if (GameManager.Instance == null || !GameManager.Instance.PlayerHasFlor())
        {
            _autoFlorQueued = false;
            yield break;
        }
        // Clear BEFORE ChallengeFlor — CompleteFlorAutoAwardUi → EnableButtons must not see
        // _autoFlorQueued still true (that left TRUCO/MAZO off while cards stayed playable).
        _autoFlorQueued = false;
        _iOweChallengeResponse = true;
        TrucoRulesScenarioLog.Ok("Auto Flor", "reason=" + reason);
        ChallengeFlor();
    }

    /// <summary>
    /// Announce local Flor before showing any response buttons (Truco/Envido).
    /// Returns true when Flor was queued — caller must not enable Quiero/Envido/etc.
    /// </summary>
    bool TryQueueResponseFlor(string reason)
    {
        if (!OneVsOneMatchSession.WithFlor || _florPlayed || _autoFlorQueued) return false;
        if (GameManager.Instance == null || !GameManager.Instance.PlayerHasFlor()) return false;
        if (invokedChallenges.Contains(ChallengeType.Flor)
            || invokedChallenges.Contains(ChallengeType.ContraFlor)
            || invokedChallenges.Contains(ChallengeType.ConFlorQuiero)
            || invokedChallenges.Contains(ChallengeType.FlorChica))
            return false;
        DisableButtons();
        _iOweChallengeResponse = true;
        _isChallengepPending = true;
        _autoFlorQueued = true;
        StartCoroutine(CoAutoFlor(reason));
        return true;
    }

    ChallengeType PendingTrucoChainType()
    {
        if (unAnsweredChallenges.ContainsKey(ChallengeType.Vale4)) return ChallengeType.Vale4;
        if (unAnsweredChallenges.ContainsKey(ChallengeType.Retruco)) return ChallengeType.Retruco;
        if (unAnsweredChallenges.ContainsKey(ChallengeType.Truco)) return ChallengeType.Truco;
        return ChallengeType.None;
    }

    void ClearEnvidoFlorPendingAfterFlor()
    {
        unAnsweredChallenges.Remove(ChallengeType.Envido);
        unAnsweredChallenges.Remove(ChallengeType.RealEnvido);
        unAnsweredChallenges.Remove(ChallengeType.FaltaEnvido);
        unAnsweredChallenges.Remove(ChallengeType.Flor);
        unAnsweredChallenges.Remove(ChallengeType.ContraFlor);
        unAnsweredChallenges.Remove(ChallengeType.ConFlorQuiero);
        unAnsweredChallenges.Remove(ChallengeType.FlorChica);
        if (GameManager.Instance != null)
            GameManager.Instance.ActiveChallenges.Clear();
    }

    bool ResumePendingTrucoChainAfterFlor() => ResumePendingTrucoChain("flor");

    /// <summary>Public wrapper for GameManager Flor response paths after Flor Chica / Con Flor Quiero.</summary>
    public bool ResumePendingTrucoChainAfterFlorPublic()
    {
        _florPlayed = true;
        if (!invokedChallenges.Contains(ChallengeType.Flor))
            invokedChallenges.Add(ChallengeType.Flor);
        ClearEnvidoFlorPendingAfterFlor();
        _isChallengepPending = false;
        CancelChallengeResponseCountdown();
        if (ResumePendingTrucoChain("flor-public"))
            return true;
        FinishChallengeUiAndRestartTimers("flor phase closed");
        return false;
    }

    bool ResumePendingTrucoChain(string reason)
    {
        ChallengeType pendingTruco = PendingTrucoChainType();
        if (pendingTruco == ChallengeType.None) return false;
        int raiser = 0;
        unAnsweredChallenges.TryGetValue(pendingTruco, out raiser);
        ClearEnvidoFlorPendingAfterFlor();
        // Re-store raiser after clear of envido/flor keys (truco keys preserved by ClearEnvidoFlorâ€¦? 
        // ClearEnvidoFlorPendingAfterFlor does NOT remove Truco â€” good. Re-apply if wiped.)
        if (raiser > 0) unAnsweredChallenges[pendingTruco] = raiser;
        _isChallengepPending = true;
        if (GameManager.Instance != null)
            GameManager.Instance.lastChallengeType = pendingTruco;
        bool iMustRespond = raiser > 0 && PhotonNetwork.LocalPlayer != null
                            && raiser != PhotonNetwork.LocalPlayer.ActorNumber;
        TrucoRulesScenarioLog.Ok("ResumePendingTruco",
            "reason=" + reason + " type=" + pendingTruco + " raiser=" + raiser
            + " iMustRespond=" + iMustRespond);
        if (iMustRespond)
        {
            switch (pendingTruco)
            {
                case ChallengeType.Truco:
                    TrucoChallenged();
                    break;
                case ChallengeType.Retruco:
                    RetrucoChallenged();
                    break;
                case ChallengeType.Vale4:
                    Vale4Challenged();
                    break;
            }
            BeginChallengeResponseCountdown();
        }
        else
        {
            // Raiser waits for Quiero / Retruco / NoQuiero â€” MAZO must stay off.
            DisableButtons();
            CancelChallengeResponseCountdown();
        }
        if (mazo != null) mazo.SetActive(false);
        return true;
    }

    public void CompleteFlorAutoAwardUi(string reason)
    {
        _florPlayed = true;
        _autoFlorQueued = false;
        if (!invokedChallenges.Contains(ChallengeType.Flor))
            invokedChallenges.Add(ChallengeType.Flor);
        flor.SetActive(false);
        conFlorQuiero.SetActive(false);
        contraFlor.SetActive(false);
        florChica.SetActive(false);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);

        if (ResumePendingTrucoChainAfterFlor())
            return;

        ClearEnvidoFlorPendingAfterFlor();
        _isChallengepPending = false;
        _iOweChallengeResponse = false;
        CancelChallengeResponseCountdown();
        TrucoRulesScenarioLog.Ok("Flor auto-award closed", "reason=" + reason
            + " myTurn=" + (GameManager.Instance != null && GameManager.Instance.IsMyTurn()));
        if (GameManager.Instance != null && GameManager.Instance.IsMyTurn())
        {
            GameManager.Instance.SetCanPlayCard(true);
            EnableButtons();
            TurnManager.Instance?.RestartTurnTimersIfActive(force: true);
        }
        else
        {
            GameManager.Instance?.SetCanPlayCard(false);
            DisableButtons();
            TurnManager.Instance?.RestartTurnTimersIfActive(force: true);
        }
    }

    /// <summary>Remove Envido/Flor entries only â€” keep pending Truco/Retruco/Vale4 for later response.</summary>
    void ClearEnvidoFamilyFromUnanswered()
    {
        unAnsweredChallenges.Remove(ChallengeType.Envido);
        unAnsweredChallenges.Remove(ChallengeType.RealEnvido);
        unAnsweredChallenges.Remove(ChallengeType.FaltaEnvido);
    }

    void LockEnvidoFamilyForHand()
    {
        _envidoPlayed = true;
        if (!invokedChallenges.Contains(ChallengeType.Envido))
            invokedChallenges.Add(ChallengeType.Envido);
        if (!invokedChallenges.Contains(ChallengeType.RealEnvido))
            invokedChallenges.Add(ChallengeType.RealEnvido);
        if (!invokedChallenges.Contains(ChallengeType.FaltaEnvido))
            invokedChallenges.Add(ChallengeType.FaltaEnvido);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
    }

    bool ResumePendingTrucoChainAfterEnvido()
    {
        ClearEnvidoFamilyFromUnanswered();
        return ResumePendingTrucoChain("envido");
    }

    static void ConfigureTurnTextStyle(TMPro.TMP_Text tmp)
    {
        if (tmp == null) return;
        // Rich-text sizes are authored in TrucoGameplayTimerBanner â€” disable auto-size so lines don't fight each other.
        tmp.enableAutoSizing = false;
        tmp.fontSize = 26f;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
        tmp.margin = new Vector4(12f, 6f, 12f, 6f);
        tmp.color = Color.white;
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
            tmp.text = TrucoGameplayAudio.IsMuted ? "ðŸ”‡" : TrucoTextosClient.SilenciarAudio;
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
        LayoutStatusBanners();
        var textRt = turnText.GetComponent<RectTransform>();
        if (textRt == null) return;
        if (turnTextBackground == null)
        {
            var t = textRt.parent;
            if (t == null) return;
            var existing = t.Find("TurnTimerBannerBg");
            if (existing != null)
                turnTextBackground = existing.GetComponent<RectTransform>();
            else
            {
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
                turnTextBackground = go.GetComponent<RectTransform>();
            }
        }
        SyncTurnBannerBackgroundRect(textRt);
        turnText.transform.SetAsLastSibling();
    }

    void SyncTurnBannerBackgroundRect(RectTransform textRt)
    {
        if (turnTextBackground == null || textRt == null) return;
        turnTextBackground.anchorMin = textRt.anchorMin;
        turnTextBackground.anchorMax = textRt.anchorMax;
        turnTextBackground.pivot = textRt.pivot;
        turnTextBackground.anchoredPosition = textRt.anchoredPosition;
        turnTextBackground.sizeDelta = new Vector2(
            Mathf.Max(textRt.sizeDelta.x + 24f, 560f),
            Mathf.Max(textRt.sizeDelta.y + 8f, 92f));
    }

    void ApplyTurnBannerSize(bool largeCountdown)
    {
        if (turnText == null) return;
        LayoutStatusBanners();
        var textRt = turnText.GetComponent<RectTransform>();
        if (textRt == null) return;
        // Same compact lane for timers and short messages â€” avoids ballooning into name/score UI.
        textRt.sizeDelta = largeCountdown
            ? new Vector2(640f, 100f)
            : new Vector2(640f, 88f);
        SyncTurnBannerBackgroundRect(textRt);
    }

    /// <param name="autoHideSeconds">Si es &lt; 0, el banner queda visible hasta el prÃ³ximo <see cref="UpdateTurnText"/> (p. ej. durante la cuenta de 30 s).</param>
    /// <param name="turnCountdownUrgent">Panel del cronÃ³metro ligeramente mÃ¡s cÃ¡lido cuando quedan pocos segundos.</param>
    /// <param name="reconnectCountdown">Rival desconectado â€” panel negro mÃ¡s marcado para el timer de 60 s.</param>
    public void UpdateTurnText(string message, float autoHideSeconds = 2.25f, bool turnCountdownUrgent = false, bool reconnectCountdown = false)
    {
        if (turnText == null) return;
        CancelInvoke(nameof(DisableTurnText));
        bool persistentTimer = autoHideSeconds < 0f;
        LayoutStatusBanners();
        EnsureTurnBannerBackground();
        ApplyTurnBannerSize(persistentTimer);
        turnText.SetActive(true);
        if (turnTextBackground != null) turnTextBackground.gameObject.SetActive(true);
        ApplyTurnBannerUrgency(turnCountdownUrgent, reconnectCountdown);
        var tmp = turnText.GetComponent<TMPro.TMP_Text>();
        if (tmp != null)
        {
            ConfigureTurnTextStyle(tmp);
            tmp.text = message ?? string.Empty;
            tmp.fontStyle = TMPro.FontStyles.Normal;
            tmp.raycastTarget = false;
        }
        // Status lane stays above callout; mute stays top-right.
        if (turnTextBackground != null) turnTextBackground.SetAsLastSibling();
        turnText.transform.SetAsLastSibling();
        if (_calloutGo != null && _calloutGo.activeSelf)
            _calloutGo.transform.SetAsLastSibling();
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
            forfeit || (GameManager.Instance != null
                && (GameManager.Instance._gameEnded || GameManager.Instance.Is1v1SettlementBusy())));
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
        if (GameManager.Instance == null || GameManager.Instance._gameEnded || GameManager.Instance.HandResolved
            || GameManager.Instance.IsMatchEndPending())
        {
            DisableButtons();
            return;
        }
        if (!GameManager.Instance.IsMyTurn())
        {
            DisableButtons();
            return;
        }
        // Challenge response UI is configured by *Challenged methods — block play buttons while any canto is open.
        if (HasUnresolvedChallengeState())
        {
            // Stale Flor keys after auto-award must not permanently hide TRUCO/MAZO.
            if (_florPlayed)
            {
                ClearEnvidoFlorPendingAfterFlor();
                _isChallengepPending = PendingTrucoChainType() != ChallengeType.None;
            }
            if (HasUnresolvedChallengeState())
            {
                if (mazo != null) mazo.SetActive(false);
                return;
            }
        }

<<<<<<< Updated upstream
=======
        // A Flor still owed must be sung on an empty board — never flash Envido/Truco while it is queued.
        // Once Flor is already resolved, ignore a stale queue flag so TRUCO/MAZO can come back.
        if (_autoFlorQueued && !_florPlayed)
        {
            DisableButtons();
            return;
        }
>>>>>>> Stashed changes
        if (ShouldAutoCallFlorNow())
        {
            DisableButtons();
            QueueAutoFlor("turn buttons");
            return;
        }

        // Never mass-enable allUiButtons — that flashes illegal Envido/Flor/Quiero for a frame.
        DisableButtons();

        // Truco chain: only the opponent of the last raise may offer Retruco / Vale4.
        if (truco != null) truco.SetActive(false);
        if (retruco != null) retruco.SetActive(false);
        if (vale4 != null) vale4.SetActive(false);
        if (!invokedChallenges.Contains(ChallengeType.Truco))
        {
            if (truco != null) truco.SetActive(true);
        }
        else if (!invokedChallenges.Contains(ChallengeType.Retruco) && CanRaiseNextTrucoLevel())
        {
            if (retruco != null) retruco.SetActive(true);
        }
        else if (!invokedChallenges.Contains(ChallengeType.Vale4) && CanRaiseNextTrucoLevel())
        {
            if (vale4 != null) vale4.SetActive(true);
        }

        // Flor or finished Envido phase locks all Envido variants for the rest of the hand.
        bool florLocked = _florPlayed
                          || invokedChallenges.Contains(ChallengeType.Flor)
                          || invokedChallenges.Contains(ChallengeType.ContraFlor)
                          || invokedChallenges.Contains(ChallengeType.ConFlorQuiero)
                          || invokedChallenges.Contains(ChallengeType.FlorChica);
        bool envidoLocked = _envidoPlayed
                            || florLocked
                            || invokedChallenges.Contains(ChallengeType.Envido)
                            || invokedChallenges.Contains(ChallengeType.RealEnvido)
                            || invokedChallenges.Contains(ChallengeType.FaltaEnvido);
        
        if (GameManager.Instance.cardPlayed || florLocked || envidoLocked)
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
                if (!envidoLocked)
                {
                    envido.SetActive(true);
                    realEnvido.SetActive(true);
                    faltaEnvido.SetActive(true);
                }
                if (OneVsOneMatchSession.WithFlor
                                                         && GameManager.Instance.PlayerHasFlor() && !_florPlayed
                                                         && !invokedChallenges.Contains(ChallengeType.Flor) 
                                                         && !invokedChallenges.Contains(ChallengeType.Envido)
                                                         && !invokedChallenges.Contains(ChallengeType.FaltaEnvido)
                                                         && !invokedChallenges.Contains(ChallengeType.RealEnvido)
                                                         && !invokedChallenges.Contains(ChallengeType.Truco))
                {
                    QueueAutoFlor("turn flor button");
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
        // Always refresh sprite â€” without this, moved cards can stay as blank white Images.
        var card = cardTransform.GetComponent<Card>();
        if (card != null)
            card.SetupCard(suit, value);
        int idx = Mathf.Min(_myDisplayCardIndex, myDisplayCardsPosition.Count - 1);
        PrepareTrickLayout(cardTransform, true);
        MoveCardTo(cardTransform, myDisplayCardsPosition[idx].position, CardPlayMoveSeconds);
        _myDisplayCardIndex++;
    }

    /// <summary>Card-to-table LeanMove duration; GameManager holds the deciding trick at least this long.</summary>
    public const float CardPlayMoveSeconds = 0.5f;
    
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
        MoveCardTo(t, otherPlayersDisplayCardsPosition[_otherPlayersDisplayCardIndex].position, CardPlayMoveSeconds);
        _otherPlayersDisplayCardIndex++;
        if (envido != null) envido.SetActive(false);
    }

    /// <summary>Shows cards revealed for envido/flor scoring (visible to both players in the trick area).</summary>
    public void TriggerMazoOnTimeout()
    {
        if (SpectatorContext.IsSpectator || GameManager.Instance == null || GameManager.Instance._gameEnded) return;
        if (GameManager.Instance.HandResolved || !GameManager.Instance.IsMyTurn()) return;
        TrucoRulesScenarioLog.Ok("TriggerMazoOnTimeout â†’ ChallengeMazo");
        GameManager.Instance.SetCanPlayCard(false);
        DisableButtons();
        ChallengeMazo();
    }

    int _scoringRevealMyCount;
    int _scoringRevealOtherCount;

    /// <summary>Reset reveal slots so a new Flor/Envido show always starts at the first table seat.</summary>
    public void BeginScoringCardReveal()
    {
        _scoringRevealMyCount = 0;
        _scoringRevealOtherCount = 0;
    }

    public void ShowRevealedScoringCard(CardSuit suit, int value, bool isMine)
    {
        // Place on the shared trick/table seats — never world-space offsets (those threw cards off-screen).
        if (isMine)
        {
            if (myDisplayCardsPosition == null || myDisplayCardsPosition.Count == 0) return;
            Transform cardTf = null;
            if (myPlayerCards != null)
            {
                foreach (var card in myPlayerCards)
                {
                    if (card == null) continue;
                    var c = card.GetComponent<Card>();
                    if (c != null && c.suit == suit && c.value == value)
                    {
                        cardTf = card.transform;
                        c.SetupCard(suit, value);
                        break;
                    }
                }
            }
            if (cardTf == null) return;
            int idx = Mathf.Min(_scoringRevealMyCount, myDisplayCardsPosition.Count - 1);
            PrepareTrickLayout(cardTf, true);
            MoveCardTo(cardTf, TableRevealTarget(myDisplayCardsPosition[idx]), 0.4f);
            _scoringRevealMyCount++;
        }
        else
        {
            if (otherPlayersCards == null || otherPlayersDisplayCardsPosition == null
                || otherPlayersDisplayCardsPosition.Count == 0) return;
            int idx = Mathf.Min(_scoringRevealOtherCount, otherPlayersCards.Count - 1);
            idx = Mathf.Min(idx, otherPlayersDisplayCardsPosition.Count - 1);
            var o = otherPlayersCards[idx].GetComponent<Card>();
            if (o == null) return;
            o.SetupCard(suit, value);
            var t = o.transform;
            PrepareTrickLayout(t, true);
            MoveCardTo(t, TableRevealTarget(otherPlayersDisplayCardsPosition[idx]), 0.4f);
            _scoringRevealOtherCount++;
        }
    }

    /// <summary>Table seat world position with a small canvas-local lift (not world meters).</summary>
    static Vector3 TableRevealTarget(Transform seat)
    {
        if (seat == null) return Vector3.zero;
        var rt = seat as RectTransform;
        if (rt != null)
            return rt.TransformPoint(new Vector3(0f, 36f, 0f));
        return seat.position;
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
        if (!CanRaiseChallengeNow()) return;
        TrucoGameplayAudio.PlayLocalRaise(TRUCO_CHALLENGE);
        MarkTrucoRaiseByLocal();
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
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(TRUCO_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);
    }
    
    // This is called from UI Button To Call Retruco Challenge on other player
    public void ChallengeRetruco()
    {
        if (!CanRaiseChallengeNow()) return;
        TrucoGameplayAudio.PlayLocalRaise(RETRUCO_CHALLENGE);
        MarkChallengeSentNow();
        MarkTrucoRaiseByLocal();
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
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(RETRUCO_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);

    }
    
    // This is called from UI Button To Call Vale4 Challenge on other player
    public void ChallengeVale4()
    {
        if (!CanRaiseChallengeNow()) return;
        TrucoGameplayAudio.PlayLocalRaise(VALE4_CHALLENGE);
        MarkChallengeSentNow();
        MarkTrucoRaiseByLocal();
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
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(VALE4_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);

    }
    
    // This is called from UI Button To Call Envido Challenge on other player
    public void ChallengeEnvido()
    {
        if (!CanRaiseChallengeNow()) return;
        TrucoGameplayAudio.PlayLocalRaise(ENVIDO_CHALLENGE);
        MarkChallengeSentNow();
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
        GameManager.Instance.MarkEnvidoFirstCaller(PhotonNetwork.LocalPlayer.ActorNumber);
        GameManager.Instance.lastChallengeType = ChallengeType.Envido;
        GameManager.Instance.challengePoints += 2;
        GameManager.Instance.TrackEnvidoCanto(ChallengeType.Envido);
        TrucoRulesScenarioLog.Ok("Local RAISE Envido", "challengePts+=" + GameManager.Instance.challengePoints
            + " cantos=" + GameManager.Instance.envidoCantoCount);
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(ENVIDO_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);
    }
    
    // This is called from UI Button To Call RealEnvido Challenge on other player
    public void ChallengeRealEnvido()
    {
        if (!CanRaiseChallengeNow()) return;
        TrucoGameplayAudio.PlayLocalRaise(REALENVIDO_CHALLENGE);
        MarkChallengeSentNow();
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
        GameManager.Instance.MarkEnvidoFirstCaller(PhotonNetwork.LocalPlayer.ActorNumber);
        GameManager.Instance.lastChallengeType = ChallengeType.RealEnvido;
        GameManager.Instance.challengePoints += 3;
        GameManager.Instance.TrackEnvidoCanto(ChallengeType.RealEnvido);
        TrucoRulesScenarioLog.Ok("Local RAISE RealEnvido", "challengePts=" + GameManager.Instance.challengePoints
            + " cantos=" + GameManager.Instance.envidoCantoCount);
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(REALENVIDO_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);
    }
    
    // This is called from UI Button To Call Falta Envido Challenge on other player

    public void ChallengeFaltaEnvido()
    {
        if (!CanRaiseChallengeNow()) return;
        MarkChallengeSentNow();
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
            GameManager.Instance.MarkEnvidoFirstCaller(PhotonNetwork.LocalPlayer.ActorNumber);
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
            GameManager.Instance.MarkEnvidoFirstCaller(PhotonNetwork.LocalPlayer.ActorNumber);
            GameManager.Instance.lastChallengeType = ChallengeType.FaltaEnvido;
            GameManager.Instance.TrackEnvidoCanto(ChallengeType.FaltaEnvido);
            TrucoRulesScenarioLog.Ok("Local RAISE FaltaEnvido", "cantos=" + GameManager.Instance.envidoCantoCount);
            double deadline = MarkChallengeSentNow();
            PhotonNetwork.RaiseEvent(FALTAENVIDO_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);
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
            GameManager.Instance.MarkTrucoChainAccepted(ChallengeType.Truco);
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Retruco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            invokedChallenges.Add(ChallengeType.Retruco);
            GameManager.Instance.MarkTrucoChainAccepted(ChallengeType.Retruco);
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Vale4))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            invokedChallenges.Add(ChallengeType.Vale4);
            GameManager.Instance.MarkTrucoChainAccepted(ChallengeType.Vale4);
            unAnsweredChallenges.Clear();
        }
            else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido)
                 || GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido)
                 || GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            LockEnvidoFamilyForHand();
            GameManager.Instance.ActiveChallenges.Clear();
            // Keep pending Truco — do not Clear() the whole dictionary.
            ClearEnvidoFamilyFromUnanswered();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Flor))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            unAnsweredChallenges.Clear();
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.ContraFlor))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            faltaEnvido.SetActive(false);
            // Do NOT ShowAllCards — reveal only via GetScore → QueueFlorCardReveal → Flush.
            TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
                "Quiero ContraFlor — defer card reveal (no early ShowAllCards)");
            unAnsweredChallenges.Clear();
        }
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        ChallengeType acceptingType = GameManager.Instance.lastChallengeType;
        bool envidoJustResolved = acceptingType == ChallengeType.Envido
                                  || acceptingType == ChallengeType.RealEnvido
                                  || acceptingType == ChallengeType.FaltaEnvido;
        _isChallengepPending = false;
        double syncAt = PhotonNetwork.Time;
        TrucoRulesScenarioLog.Ok("Local RAISE Quiero (accept)",
            "accepting=" + acceptingType + " mazoPts=" + GameManager.Instance.mazoPoints);
        PhotonNetwork.RaiseEvent(QUEIRO_CHALLENGE, syncAt, RaiseEventOptions.Default, SendOptions.SendReliable);
        if (envidoJustResolved)
        {
            // Master is the only scorer; if local is Master and accepted Envido, score BEFORE clearing stake.
            if (PhotonNetwork.IsMasterClient)
                GameManager.Instance.GetScore(acceptingType);
            GameManager.Instance.lastChallengeType = ChallengeType.None;
            GameManager.Instance.challengePoints = 0;
        }
        if (envidoJustResolved && ResumePendingTrucoChainAfterEnvido())
        {
            // Pending Truco response UI is live — do not enable MAZO on the raiser.
            FinishChallengeUiAndRestartTimers("local Quiero→pending Truco", syncAt);
            return;
        }
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
        FinishChallengeUiAndRestartTimers("local Quiero", syncAt);
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
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido)
                 || GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido)
                 || GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            LockEnvidoFamilyForHand();
            GameManager.Instance.ActiveChallenges.Clear();
            ClearEnvidoFamilyFromUnanswered();
            GameManager.Instance.lastChallengeType = ChallengeType.None;
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Flor))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            envido.SetActive(false);
            realEnvido.SetActive(false);
            ResumePendingTrucoChainAfterFlor();
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
        bool envidoDeclined = decliningType == ChallengeType.Envido
                              || decliningType == ChallengeType.RealEnvido
                              || decliningType == ChallengeType.FaltaEnvido;
        _isChallengepPending = false;
        double syncAt = PhotonNetwork.Time;
        int awardPreview = TrucoRulePoints.IsEnvidoFamily(decliningType)
            ? TrucoRulePoints.EnvidoNoQuieroAward(decliningType, GameManager.Instance.challengePoints,
                GameManager.Instance.envidoCantoCount)
            : TrucoRulePoints.NoQuieroAward(decliningType);
        // Payload: type, sync clock, precomputed award (master must not depend on cleared local stake).
        object[] noQuieroPayload = { (byte)decliningType, syncAt, awardPreview };
        TrucoRulesScenarioLog.Ok("Local RAISE NoQuiero (decline)",
            "declining=" + decliningType + " awardViaMaster=" + awardPreview
            + " endsHand=" + TrucoRulePoints.NoQuieroEndsHand(decliningType));
        TrucoPunChallenges.RaiseToAll(NOQUEIRO_CHALLENGE, noQuieroPayload);
        if (TrucoRulePoints.NoQuieroEndsHand(decliningType))
        {
            CancelChallengeResponseCountdown();
            unAnsweredChallenges.Clear();
            _isChallengepPending = false;
            GameManager.Instance.SetCanPlayCard(false);
            GameManager.Instance.MarkHandResolved();
            TurnManager.Instance?.StopAllTurnTimers();
            GameManager.Instance.challengePoints = 0;
            return;
        }
        if (envidoDeclined && ResumePendingTrucoChainAfterEnvido())
        {
            FinishChallengeUiAndRestartTimers("local NoQuiero→pending Truco", syncAt);
            return;
        }
        if (unAnsweredChallenges.Count > 0)
        {
            StartCoroutine(CheckForUnansweredChallenges());
        }
        else if (GameManager.Instance.IsMyTurn()
                 && !GameManager.Instance.HandResolved
                 && !GameManager.Instance.IsMatchEndPending())
        {
            if (envidoDeclined)
            {
                if (!invokedChallenges.Contains(ChallengeType.Truco))
                    truco.SetActive(true);
            }
            GameManager.Instance.SetCanPlayCard(true);
        }
        else
        {
            DisableButtons();
        }
        GameManager.Instance.challengePoints = 0;
        FinishChallengeUiAndRestartTimers("local NoQuiero", syncAt);
    }
    
    // This is called from UI Button To Call Flor Challenge on other player

    public void ChallengeFlor()
    {
         if (!CanRaiseChallengeNow() && !_autoFlorQueued) return;
         MarkChallengeSentNow();
         _isChallengepPending = true;
        _florPlayed = true;
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

            GameManager.Instance.RequestNetworkAward(PhotonNetwork.LocalPlayer.ActorNumber, 3, false);
            TrucoPunChallenges.RaiseToOthers(FLOR_CHALLENGE, FlorAutoAwardFlag);
            TrucoRulesScenarioLog.Ok("Local RAISE Flor AUTO-AWARD (+3)", "rivalDeniedFlor=true");
            if (hasPendingTruco || hasPendingRetruco || hasPendingVale4)
                ResumePendingTrucoChainAfterFlor();
            else
                CompleteFlorAutoAwardUi("local rival denied flor");
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
        unAnsweredChallenges[ChallengeType.Flor] = PhotonNetwork.LocalPlayer.ActorNumber;
        double deadline = MarkChallengeSentNow();
        TrucoPunChallenges.RaiseToOthers(FLOR_CHALLENGE, deadline);
    }

    public void ChallengeFlorChica()
    {
        TrucoGameplayAudio.PlayLocalRaise(FLOR_CHICA_CHALLENGE);
        _isChallengepPending = false;
        _florPlayed = true;
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
        unAnsweredChallenges.Remove(ChallengeType.Flor);
        unAnsweredChallenges.Remove(ChallengeType.FlorChica);
        unAnsweredChallenges.Remove(ChallengeType.ConFlorQuiero);
        unAnsweredChallenges.Remove(ChallengeType.ContraFlor);
        double deadline = MarkChallengeSentNow();
        TrucoPunChallenges.RaiseToAll(FLOR_CHICA_CHALLENGE, deadline);
        // Close Flor phase locally; OnEvent All will also run on peer.
        if (!ResumePendingTrucoChainAfterFlorPublic())
        {
            if (GameManager.Instance.IsMyTurn())
                GameManager.Instance.SetCanPlayCard(true);
        }
    }
    
    // This is called from UI Button To Call Con FLor Quiero Challenge on other player

    public void ChallengeConFlorQuiero()
    {
        TrucoGameplayAudio.PlayLocalRaise(CON_FLOR_QUIERO_CHALLENGE);
        _isChallengepPending = false;
        _florPlayed = true;
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
        GameManager.Instance.challengePoints = 6;
        TrucoRulesScenarioLog.Ok("Local RAISE ConFlorQuiero", "challengePts=6 deferred to hand end");
        unAnsweredChallenges.Remove(ChallengeType.Flor);
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(CON_FLOR_QUIERO_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);
        if (!ResumePendingTrucoChainAfterFlorPublic())
        {
            if (GameManager.Instance.IsMyTurn())
                GameManager.Instance.SetCanPlayCard(true);
        }
    }
    
    // This is called from UI Button To Call Contra Flor Challenge on other player

    public void ChallengeContraFlor()
    {
        TrucoGameplayAudio.PlayLocalRaise(CONTRA_FLOR_CHALLENGE);
        MarkChallengeSentNow();
        _isChallengepPending = true;
        _florPlayed = true;
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
        double deadline = MarkChallengeSentNow();
        PhotonNetwork.RaiseEvent(CONTRA_FLOR_CHALLENGE, deadline, RaiseEventOptions.Default, SendOptions.SendReliable);
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
            TrucoRulesScenarioLog.Ok("ChallengeMazo skipped (hand already resolved) â†’ sync only");
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
        GameManager.Instance.mazoPoints = 1;
        GameManager.Instance.noQuieroPoints = 1;
        GameManager.Instance.challengePoints = 1;

        // Never show Quiero/Retruco/Envido first — P2 Flor must be announced with an empty board.
        DisableButtons();
        _iOweChallengeResponse = true;
        if (TryQueueResponseFlor("truco response"))
            return;

        queiro.SetActive(true);
        noQueiro.SetActive(true);
        retruco.SetActive(true);
        if (mazo != null) mazo.SetActive(false);

        bool florLocked = _florPlayed
                          || invokedChallenges.Contains(ChallengeType.Flor)
                          || invokedChallenges.Contains(ChallengeType.FlorChica)
                          || invokedChallenges.Contains(ChallengeType.ConFlorQuiero)
                          || invokedChallenges.Contains(ChallengeType.ContraFlor);
        if (!florLocked && !invokedChallenges.Contains(ChallengeType.Envido)
            && !GameManager.Instance.cardPlayed)
        {
            envido.SetActive(true);
            realEnvido.SetActive(true);
            faltaEnvido.SetActive(true);
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void RetrucoChallenged()
    {
        _isChallengepPending = true;
        invokedChallenges.Add(ChallengeType.Retruco);
        _cantChallenge = false;
        DisableButtons();
        queiro.SetActive(true);
        noQueiro.SetActive(true);
        vale4.SetActive(true);
        unAnsweredChallenges[ChallengeType.Retruco] = PhotonNetwork.PlayerListOthers[0].ActorNumber;
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
        DisableButtons();
        queiro.SetActive(true);
        noQueiro.SetActive(true);
        unAnsweredChallenges[ChallengeType.Vale4] = PhotonNetwork.PlayerListOthers[0].ActorNumber;
        GameManager.Instance.mazoPoints = 3;
        GameManager.Instance.noQuieroPoints = 3;
        GameManager.Instance.cardPlayed = true;
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void EnvidoChallenged()
    {
        _isChallengepPending = true;
        GameManager.Instance.ActiveChallenges.Add(ChallengeType.Envido);
        DisableButtons();
        _iOweChallengeResponse = true;
        if (TryQueueResponseFlor("envido response"))
            return;

        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            _isDoubleEnvido = true;
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
        }
        else
        {
            envido.SetActive(true);
            realEnvido.SetActive(true);
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void RealEnvidoChallenged()
    {
        _isChallengepPending = true;
        GameManager.Instance.ActiveChallenges.Add(ChallengeType.RealEnvido);
        DisableButtons();
        _iOweChallengeResponse = true;
        if (TryQueueResponseFlor("real envido response"))
            return;

        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido))
        {
            _isDoubleEnvido = false;
            _isDoubleRealEnvido = true;
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
        }
        else if(GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido))
        {
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
        }
        else
        {
            realEnvido.SetActive(true);
            faltaEnvido.SetActive(true);
            queiro.SetActive(true);
            noQueiro.SetActive(true);
        }
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void FaltaEnvidoChallenged()
    {
        _isChallengepPending = true;
        DisableButtons();
        _iOweChallengeResponse = true;
        if (TryQueueResponseFlor("falta envido response"))
            return;

        queiro.SetActive(true);
        noQueiro.SetActive(true);
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void FlorChallenged()
    {
        _isChallengepPending = true;
        _florPlayed = true;
        if (!invokedChallenges.Contains(ChallengeType.Flor))
            invokedChallenges.Add(ChallengeType.Flor);
        DisableButtons();
        // After Flor, Envido family stays off â€” only Flor responses + later Truco chain.
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        conFlorQuiero.SetActive(true);
        florChica.SetActive(true);
        contraFlor.SetActive(true);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        if (mazo != null) mazo.SetActive(false);
    }

    // This is called When Other player Challenges us with this Challenge
    public void FlorChicaChallenged()
    {
        _isChallengepPending = false;
        _florPlayed = true;
        unAnsweredChallenges.Remove(ChallengeType.Flor);
        unAnsweredChallenges.Remove(ChallengeType.FlorChica);
        unAnsweredChallenges.Remove(ChallengeType.ConFlorQuiero);
        unAnsweredChallenges.Remove(ChallengeType.ContraFlor);
        if (!invokedChallenges.Contains(ChallengeType.Flor))
            invokedChallenges.Add(ChallengeType.Flor);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActiveChallenges.Remove(ChallengeType.Flor);
            GameManager.Instance.ActiveChallenges.Remove(ChallengeType.FlorChica);
            if (TrucoRulePoints.IsEnvidoFamily(GameManager.Instance.lastChallengeType)
                || GameManager.Instance.lastChallengeType == ChallengeType.Flor
                || GameManager.Instance.lastChallengeType == ChallengeType.FlorChica)
                GameManager.Instance.lastChallengeType = ChallengeType.None;
        }
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        if (mazo != null) mazo.SetActive(false);
    }
    
    // This is called When Other player Challenges us with this Challenge
    public void ConFlorQuieroChallenged()
    {
        _isChallengepPending = false;
        _florPlayed = true;
        unAnsweredChallenges.Remove(ChallengeType.Flor);
        unAnsweredChallenges.Remove(ChallengeType.FlorChica);
        if (!invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
            invokedChallenges.Add(ChallengeType.ConFlorQuiero);
        if (!invokedChallenges.Contains(ChallengeType.Flor))
            invokedChallenges.Add(ChallengeType.Flor);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
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
        _florPlayed = true;
        if (!invokedChallenges.Contains(ChallengeType.Flor))
            invokedChallenges.Add(ChallengeType.Flor);
        envido.SetActive(false);
        realEnvido.SetActive(false);
        faltaEnvido.SetActive(false);
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        // After Contraflor: original Flor caller answers with Flor Chica or Quiero.
        florChica.SetActive(true);
        queiro.SetActive(true);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);
        if (mazo != null) mazo.SetActive(false);
    }
    
    // This block of code is called when the player accepts the challenge
    public void QueiroChallenged(double syncStartedAt = -1)
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
        bool envidoAccepted = false;
        if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Truco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            GameManager.Instance.MarkTrucoChainAccepted(ChallengeType.Truco);
            unAnsweredChallenges.Clear();
            invokedChallenges.Add(ChallengeType.Truco);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Retruco))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            GameManager.Instance.MarkTrucoChainAccepted(ChallengeType.Retruco);
            unAnsweredChallenges.Clear();
            invokedChallenges.Add(ChallengeType.Retruco);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Vale4))
        {
            truco.SetActive(false);
            retruco.SetActive(false);
            vale4.SetActive(false);
            GameManager.Instance.MarkTrucoChainAccepted(ChallengeType.Vale4);
            unAnsweredChallenges.Clear();
            invokedChallenges.Add(ChallengeType.Vale4);
        }
        else if (GameManager.Instance.lastChallengeType.Equals(ChallengeType.Envido)
                 || GameManager.Instance.lastChallengeType.Equals(ChallengeType.RealEnvido)
                 || GameManager.Instance.lastChallengeType.Equals(ChallengeType.FaltaEnvido))
        {
            flor.SetActive(false);
            contraFlor.SetActive(false);
            LockEnvidoFamilyForHand();
            GameManager.Instance.ActiveChallenges.Clear();
            ClearEnvidoFamilyFromUnanswered();
            envidoAccepted = true;
            // Clear Envido stake type so later MAZO cannot re-score it (ResumePendingTruco may set Truco).
            GameManager.Instance.lastChallengeType = ChallengeType.None;
            GameManager.Instance.challengePoints = 0;
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

        _cantChallenge = false;
        if (envidoAccepted && ResumePendingTrucoChainAfterEnvido())
        {
            FinishChallengeUiAndRestartTimers("recv Quieroâ†’pending Truco", syncStartedAt);
            return;
        }
        FinishChallengeUiAndRestartTimers("recv Quiero", syncStartedAt);
        if (GameManager.Instance.IsMyTurn())
            GameManager.Instance.SetCanPlayCard(true);
    }

    // This block of code is called when the player does not accept the challenge
    public void NoQueiroChallenged(double syncStartedAt = -1)
    {
        ChallengeType declined = GameManager.Instance != null
            ? GameManager.Instance.lastChallengeType
            : ChallengeType.None;
        bool endsHand = TrucoRulePoints.NoQuieroEndsHand(declined);

        _isChallengepPending = false;
        _cantChallenge = false;
        DisableButtons();
        flor.SetActive(false);
        contraFlor.SetActive(false);
        conFlorQuiero.SetActive(false);
        florChica.SetActive(false);
        queiro.SetActive(false);
        noQueiro.SetActive(false);
        truco.SetActive(false);
        retruco.SetActive(false);
        vale4.SetActive(false);

        bool envidoDeclined = false;
        if (declined == ChallengeType.Truco
            || declined == ChallengeType.Retruco
            || declined == ChallengeType.Vale4)
        {
            trucoPlayed = true;
            unAnsweredChallenges.Remove(ChallengeType.Truco);
            unAnsweredChallenges.Remove(ChallengeType.Retruco);
            unAnsweredChallenges.Remove(ChallengeType.Vale4);
        }
        else if (declined == ChallengeType.Envido
                 || declined == ChallengeType.RealEnvido
                 || declined == ChallengeType.FaltaEnvido)
        {
            LockEnvidoFamilyForHand();
            ClearEnvidoFamilyFromUnanswered();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ActiveChallenges.Clear();
                GameManager.Instance.lastChallengeType = ChallengeType.None;
                GameManager.Instance.challengePoints = 0;
            }
            envidoDeclined = true;
        }
        else if (declined == ChallengeType.Flor || declined == ChallengeType.ContraFlor)
        {
            envido.SetActive(false);
            realEnvido.SetActive(false);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.noQuieroPoints = 0;

        // Hand-ending NoQuiero (Truco/Retruco/Vale4): never re-enable TRUCO/NO QUIERO.
        if (endsHand)
        {
            CancelChallengeResponseCountdown();
            unAnsweredChallenges.Clear();
            _isChallengepPending = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetCanPlayCard(false);
                GameManager.Instance.MarkHandResolved();
            }
            TurnManager.Instance?.StopAllTurnTimers();
            TrucoRulesScenarioLog.Ok("NoQuiero ends hand — buttons locked",
                "declined=" + declined);
            return;
        }

        if (envidoDeclined && ResumePendingTrucoChainAfterEnvido())
        {
            FinishChallengeUiAndRestartTimers("recv NoQuiero→pending Truco", syncStartedAt);
            return;
        }
        if (unAnsweredChallenges.Count > 0)
        {
            StartCoroutine(CheckForUnansweredChallenges());
        }
        else if (GameManager.Instance != null && GameManager.Instance.IsMyTurn()
                 && !GameManager.Instance.HandResolved && !GameManager.Instance.IsMatchEndPending())
        {
            GameManager.Instance.SetCanPlayCard(true);
        }
        else
        {
            DisableButtons();
        }

        FinishChallengeUiAndRestartTimers("recv NoQuiero", syncStartedAt);
    }

    
    // This block of code checks for any unanswered challenges after a delay
    public IEnumerator CheckForUnansweredChallenges()
    {
        yield return new WaitForSeconds(0.35f);
        ChallengeType pending = PendingTrucoChainType();
        if (pending == ChallengeType.None || unAnsweredChallenges.Count == 0) yield break;
        int raiser = unAnsweredChallenges[pending];
        GameManager.Instance.lastChallengeType = pending;
        bool iMustRespond = raiser != PhotonNetwork.LocalPlayer.ActorNumber;
        TrucoRulesScenarioLog.Ok("CheckForUnansweredChallenges",
            "type=" + pending + " raiser=" + raiser + " iMustRespond=" + iMustRespond);
        if (iMustRespond)
        {
            switch (pending)
            {
                case ChallengeType.Truco: TrucoChallenged(); break;
                case ChallengeType.Retruco: RetrucoChallenged(); break;
                case ChallengeType.Vale4: Vale4Challenged(); break;
            }
            BeginChallengeResponseCountdown();
            if (mazo != null) mazo.SetActive(false);
        }
        else
        {
            DisableButtons();
            if (mazo != null) mazo.SetActive(false);
        }
    }
    
}
