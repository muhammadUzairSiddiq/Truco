using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using MH.Multiplayer;
using UnityEngine.UI;

public enum RoundState
{
    WIN,
    LOSE,
    DRAW
}

public class GameManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static GameManager Instance { get; private set; }
    [SerializeField] private Deck deck;
    [SerializeField] private List<Card> cards;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private Button[] _menuBtns;

    private bool _myTurn = false;
    private bool _canPlayCard = true;
    private static byte _cardSendEvent = 50;

    public List<ScoreClass> player1Score = new List<ScoreClass>();
    public List<ScoreClass> player2Score = new List<ScoreClass>();
    public PlayerScoreHandler myPlayerScoreHandler;
    public PlayerScoreHandler otherPlayerScoreHandler;

    public ChallengeType lastChallengeType = ChallengeType.None;

    public int points = 0;
    public int challengePoints = 0;
    public int mazoPoints = 0;
    public int noQuieroPoints = 0;
    /// <summary>0=none, 1=Truco accepted, 2=Retruco accepted, 3=Vale4 accepted. Independent of Envido/Flor.</summary>
    public int acceptedTrucoLevel = 0;
    /// <summary>Photon actor who led the current trick — leads again after a parda.</summary>
    int _trickLeadActor;
    /// <summary>How many Envido/Real/Falta cantos have been raised this hand (for NoQuiero stake).</summary>
    public int envidoCantoCount = 0;

    private List<DeckCards> _player1Cards;
    private List<DeckCards> _player2Cards;
    public bool _gameEnded = false;
    /// <summary>Current hand already conceded (Mazo) â€” blocks repeat Mazo / timeout turn advance.</summary>
    public bool HandResolved { get; private set; }
    public bool cardPlayed = false;
    public bool otherPlayerDeniedFlor = false;
    public bool deniedFlor = false;
    public int EnvidoFirstCallerActor { get; private set; }
<<<<<<< Updated upstream
=======
    /// <summary>Envido/Real/Falta stake already awarded this hand — Mazo must not concede it again.</summary>
    public bool envidoResolvedThisHand;
    /// <summary>Flor points already awarded this hand (auto +3, Flor Chica, Con Flor Quiero, Contra Flor).</summary>
    public bool florResolvedThisHand;
    /// <summary>Actor who owns the resolved Flor — their three cards must be shown before the hand closes.</summary>
    public int florOwnerActorThisHand;
    /// <summary>The Flor hand was already pushed to the table, so Mazo/hand-end must not reveal it twice.</summary>
    bool _florCardsRevealedThisHand;
    /// <summary>Con Flor Quiero accepted but not yet scored — settled when the hand closes.</summary>
    public bool conFlorQuieroPending;
    /// <summary>How long the Flor cards stay on the table before the hand result is applied.</summary>
    const float FlorRevealHoldSeconds = 2.5f;
    /// <summary>Envido/Falta winning cards stay readable this long before NuevaMano / result panel.</summary>
    const float EnvidoRevealHoldSeconds = 2.2f;
    /// <summary>Card LeanMove (0.5 s) + readable pause before the deciding trick closes the hand.</summary>
    const float FinalCardHoldSeconds = 1.6f;
    /// <summary>Winning score reached — show Flor/Envido cards first; block TRUCO/MAZO until the panel.</summary>
    bool _matchEndPending;
    bool _matchEndRevealRoutineRunning;
    /// <summary>A local coroutine is holding the hand open (final card / Flor / Envido reveal) and will close it itself.</summary>
    bool _handCloseHoldActive;
    /// <summary>Accepted Envido/Real/Falta this hand — its winning cards must be shown before the hand or match closes.</summary>
    bool _envidoRevealOwed;
    bool _envidoRevealHeld;
>>>>>>> Stashed changes

    public List<ChallengeType> ActiveChallenges = new List<ChallengeType>();

    // Track per-trick results: 1 = player1 win, 2 = player2 win, 0 = draw
    private List<int> trickResults = new List<int>();
    bool _pendingScoringCardReveal;
    bool _scoringRevealFlushScheduled;
    string _pendingRevealMasterJson = "{}";
    string _pendingRevealGuestJson = "{}";
    /// <summary>Photon actor of Envido/Flor scoring winner â€” only their cards are revealed.</summary>
    int _pendingRevealWinnerActor;

    // Tournament-specific variables
    private bool _isInTournament = false;
    private bool _tournamentMatchFinalized = false;
    private bool _1v1ResultPosted;
    private bool _1v1SettlementBusy;
    [SerializeField] private bool _isSpectator;

    /// <summary>Winner/Mazo already applied this hand â€” blocks duplicate +2 from timeout ensure.</summary>
    bool _handOutcomeCommitted;
    /// <summary>Nueva mano scene reload already scheduled â€” ignore late Mazo/Winner.</summary>
    bool _restartScheduled;
    Coroutine _restartRoutine;

    private void Awake()
    {
        Instance = this;
        _isSpectator = SpectatorContext.IsSpectator;
        _handOutcomeCommitted = false;
        _restartScheduled = false;
        _restartRoutine = null;
        TrucoDebugLog.Always(TrucoDebugLog.Category.Gameplay,
            "Gameplay Awake spectator=" + _isSpectator
            + " match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?")
            + " isHost=" + OneVsOneMatchSession.IsHost
            + " inRoom=" + PhotonNetwork.InRoom
            + " players=" + (PhotonNetwork.CurrentRoom?.PlayerCount ?? 0)
            + " logFile=" + TrucoDebugLog.LogFilePath);
        if (_isSpectator) return;
        if (GetComponent<TrucoPunReconnectionManager>() == null)
            gameObject.AddComponent<TrucoPunReconnectionManager>();
        if (GetComponent<GameplayTrucoPlayerAvatars>() == null)
            gameObject.AddComponent<GameplayTrucoPlayerAvatars>();
        TrucoGameplayAudio.EnsureUnder(transform);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (_isSpectator) return;
        points = 1;
        trickResults.Clear();
        player1Score.Clear();
        player2Score.Clear();
        if (TrucoMatchProgress.ConsumeFreshMatchScores())
        {
            DataHandler.Instance.points = 0;
            DataHandler.Instance.opponentPoints = 0;
            DataHandler.Instance.roundNumber = 0;
        }
        if (myPlayerScoreHandler != null)
            myPlayerScoreHandler.SetScore(DataHandler.Instance.points);
        if (otherPlayerScoreHandler != null)
            otherPlayerScoreHandler.SetScore(DataHandler.Instance.opponentPoints);
        HandResolved = false;
        _handOutcomeCommitted = false;
        _restartScheduled = false;
        _restartRoutine = null;
        // Pie must not see active challenge buttons before Turn RPC assigns mano.
        SetMyTurn(false);
        SetCanPlayCard(false);
        UIMANAGER.Instance?.DisableButtons();
    }

    void PersistMatchScoresToDataHandler()
    {
        if (DataHandler.Instance == null) return;
        DataHandler.Instance.points = myPlayerScoreHandler != null ? myPlayerScoreHandler.GetCurrentScore() : 0;
        DataHandler.Instance.opponentPoints = otherPlayerScoreHandler != null ? otherPlayerScoreHandler.GetCurrentScore() : 0;
    }

    int GetScoreForPhotonActor(int actorNumber)
    {
        if (myPlayerScoreHandler != null && actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            return myPlayerScoreHandler.GetCurrentScore();
        if (otherPlayerScoreHandler != null && actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            return otherPlayerScoreHandler.GetCurrentScore();
        return 0;
    }

    /// <summary>
    /// Scores are keyed by stable ActorNumber order (low, high) â€” never by Photon MasterClient.
    /// Master rotates each mano for dealing; mapping by master was flipping me/opp on both devices.
    /// RPC still uses two ints for wire compatibility; meaning is always (scoreLowActor, scoreHighActor).
    /// </summary>
    bool TryGetOrderedTrucoActors(out int lowActor, out int highActor)
    {
        lowActor = highActor = 0;
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players == null || players.Count < 2) return false;
        lowActor = players[0].ActorNumber;
        highActor = players[1].ActorNumber;
        return true;
    }

    int GetManoActorForCurrentHand()
    {
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players == null || players.Count < 2)
            return PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;

        int manoPlayer = TrucoHandWinner.GetManoPlayerNumber(DataHandler.Instance.roundNumber);
        return manoPlayer == 1 ? players[0].ActorNumber : players[1].ActorNumber;
    }

    void ApplyAuthoritativeScores(int scoreLowActor, int scoreHighActor)
    {
        if (_isSpectator) return;
        if (!TryGetOrderedTrucoActors(out int lowActor, out int highActor))
        {
            TrucoRulesScenarioLog.Ok("ScoreApply skipped", "need 2 truco players");
            return;
        }
        int local = PhotonNetwork.LocalPlayer.ActorNumber;
        int mine = local == lowActor ? scoreLowActor : scoreHighActor;
        int other = local == lowActor ? scoreHighActor : scoreLowActor;
        myPlayerScoreHandler?.SetScore(mine);
        otherPlayerScoreHandler?.SetScore(other);
        PersistMatchScoresToDataHandler();
        TrucoRulesScenarioLog.Ok("Scores applied by actor",
            "a" + lowActor + "=" + scoreLowActor + " a" + highActor + "=" + scoreHighActor
            + " local=" + local + " â†’ me=" + mine + " opp=" + other);
    }

    void BroadcastAuthoritativeScores()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || photonView == null) return;
        if (!TryGetOrderedTrucoActors(out int lowActor, out int highActor)) return;
        int scoreLow = GetScoreForPhotonActor(lowActor);
        int scoreHigh = GetScoreForPhotonActor(highActor);
        // All (not AllBuffered): buffered score RPCs re-ran after SetMasterClient and flipped seats.
        photonView.RPC(nameof(RPC_ApplyMatchScores), RpcTarget.All, scoreLow, scoreHigh);
        TrucoRulesScenarioLog.Ok("BroadcastScores by actor",
            "a" + lowActor + "=" + scoreLow + " a" + highActor + "=" + scoreHigh);
    }

    void SyncScoresAfterPointChange()
    {
        PersistMatchScoresToDataHandler();
        if (PhotonNetwork.IsMasterClient)
            BroadcastAuthoritativeScores();
        else if (photonView != null && PhotonNetwork.InRoom)
            // Scores only. The old full SyncMatchState reply rebuilt canto UI / snapped cards and,
            // with handResolved=1, made the guest ResetGame before the Flor reveal hold finished.
            photonView.RPC(nameof(RequestScoresFromClient), RpcTarget.MasterClient);
    }

    [PunRPC]
    void RequestScoresFromClient()
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        BroadcastAuthoritativeScores();
    }

    [PunRPC]
    void RPC_ApplyMatchScores(int scoreLowActor, int scoreHighActor)
    {
        ApplyAuthoritativeScores(scoreLowActor, scoreHighActor);
    }

    void ApplyPointsToActor(int winnerActor, int points)
    {
        if (winnerActor == PhotonNetwork.LocalPlayer.ActorNumber)
            myPlayerScoreHandler?.UpdateScore(points, true);
        else
            otherPlayerScoreHandler?.UpdateScore(points, true);
    }

    /// <summary>Master-only: broadcast identical score change to every client.</summary>
    public void RequestNetworkAward(int winnerActor, int points, bool endHand = false)
    {
        if (!PhotonNetwork.InRoom || photonView == null) return;
        if (PhotonNetwork.IsMasterClient)
            BroadcastNetworkAward(winnerActor, points, endHand);
        else
            photonView.RPC(nameof(RequestAwardFromMaster), RpcTarget.MasterClient, winnerActor, points, endHand);
    }

    [PunRPC]
    void RequestAwardFromMaster(int winnerActor, int points, bool endHand)
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        BroadcastNetworkAward(winnerActor, points, endHand);
    }

    void BroadcastNetworkAward(int winnerActor, int points, bool endHand)
    {
        if (!PhotonNetwork.InRoom || photonView == null || _gameEnded) return;
        photonView.RPC(nameof(RPC_NetworkAwardPoints), RpcTarget.All, winnerActor, points, endHand);
    }

    void MasterResolveNoQuiero(int declinerActor, ChallengeType type, int forcedPoints = -1)
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        int winnerActor = GetOpponentActorNumber(declinerActor);
        int pts = forcedPoints >= 0
            ? forcedPoints
            : (TrucoRulePoints.IsEnvidoFamily(type)
                ? TrucoRulePoints.EnvidoNoQuieroAward(type, challengePoints, envidoCantoCount)
                : TrucoRulePoints.NoQuieroAward(type));
        bool endHand = TrucoRulePoints.NoQuieroEndsHand(type);
        TrucoRulesScenarioLog.Ok("MasterResolveNoQuiero",
            "decliner=" + declinerActor + " winner=" + winnerActor
            + " type=" + type + " pts=" + pts + " endHand=" + endHand
            + " chPts=" + challengePoints + " envidoCantos=" + envidoCantoCount);
        BroadcastNetworkAward(winnerActor, pts, endHand);
    }

    public void TrackEnvidoCanto(ChallengeType type)
    {
        if (!TrucoRulePoints.IsEnvidoFamily(type)) return;
        envidoCantoCount++;
    }

    static ChallengeType ParseChallengeTypeFromEvent(EventData photonEvent, ChallengeType fallback)
    {
        if (photonEvent.CustomData is byte b) return (ChallengeType)b;
        if (photonEvent.CustomData is object[] arr && arr.Length > 0 && arr[0] is byte bb)
            return (ChallengeType)bb;
        return fallback;
    }

    /// <summary>Shared Photon clock stamp carried in Quiero/NoQuiero payloads so both seats restart the same timer.</summary>
    static double ParseTimerSyncFromEvent(EventData photonEvent)
    {
        if (photonEvent.CustomData is double d) return d;
        if (photonEvent.CustomData is object[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] is double dd) return dd;
                if (arr[i] is float f) return f;
            }
        }
        return PhotonNetwork.Time;
    }

    /// <summary>Shared Photon deadline for canto response (raiser stamped, responder reuses).</summary>
    static double ParseChallengeDeadlineFromEvent(EventData photonEvent)
    {
        if (photonEvent.CustomData is double d && d > 0) return d;
        if (photonEvent.CustomData is float f && f > 0) return f;
        if (photonEvent.CustomData is object[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] is double dd && dd > 0) return dd;
                if (arr[i] is float ff && ff > 0) return ff;
            }
        }
        return -1;
    }

    static int ParseAwardPointsFromEvent(EventData photonEvent)
    {
        if (photonEvent.CustomData is object[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] is int ii && i > 0) return ii;
                if (arr[i] is byte bb && i >= 2) return bb;
            }
        }
        return -1;
    }

    public void MarkEnvidoFirstCaller(int actorNumber)
    {
        if (actorNumber > 0 && EnvidoFirstCallerActor <= 0)
            EnvidoFirstCallerActor = actorNumber;
    }

    [PunRPC]
    void RPC_NetworkAwardPoints(int winnerActor, int points, bool endHand)
    {
        if (_isSpectator) return;
        bool mine = winnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        TrucoRulesScenarioLog.Ok("ScoreAward",
            "winnerActor=" + winnerActor + " pts=+" + points
            + " toMe=" + mine + " endHand=" + endHand);
        ApplyPointsToActor(winnerActor, points);
        SyncScoresAfterPointChange();
        if (EvaluateMatchOutcomeAfterPoints()) return;
        if (endHand)
            EndRoundAfterOptionalFlorReveal();
    }

    /// <summary>NoQuiero / endHand awards must still show sung Flor cards before NuevaMano.</summary>
    void EndRoundAfterOptionalFlorReveal()
    {
        bool needHold = false;
        if (PhotonNetwork.IsMasterClient)
        {
            needHold = SettleFlorBeforeHandCloses(
                PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0,
                handConceded: false);
        }
        else
        {
            var ui = UIMANAGER.Instance;
            needHold = ui != null && (ui._florPlayed || florResolvedThisHand);
        }
        if (needHold)
        {
            MarkHandResolved();
            _handCloseHoldActive = true;
            StartCoroutine(CoEndRoundAfterFlorReveal());
            return;
        }
        EndRound();
    }

    IEnumerator CoEndRoundAfterFlorReveal()
    {
        yield return new WaitForSeconds(FlorRevealHoldSeconds);
        _handCloseHoldActive = false;
        if (_gameEnded || _restartScheduled) yield break;
        EndRound();
    }

    /// <summary>
    /// Hand is over on every seat: if an accepted Envido left winning cards on the table, keep them
    /// readable before NuevaMano. Deterministic on both clients (flag set by the Envido winner RPCs).
    /// </summary>
    void ScheduleResetGameAfterReveals()
    {
        if (_gameEnded || _restartScheduled) return;
        if (_envidoRevealOwed && !_envidoRevealHeld)
        {
            _envidoRevealHeld = true;
            MarkHandResolved();
            _handCloseHoldActive = true;
            StartCoroutine(CoResetGameAfterEnvidoReveal());
            return;
        }
        ResetGame();
    }

    IEnumerator CoResetGameAfterEnvidoReveal()
    {
        UIMANAGER.Instance?.EnsureLocalHandSpritesVisible();
        UIMANAGER.Instance?.EnsureOpponentRevealSpritesVisible();
        yield return new WaitForSeconds(EnvidoRevealHoldSeconds);
        _handCloseHoldActive = false;
        if (_gameEnded || _restartScheduled) yield break;
        ResetGame();
    }

    public void MarkHandResolved()
    {
        if (HandResolved || _gameEnded) return;
        HandResolved = true;
        SetCanPlayCard(false);
        SetMyTurn(false);
        TurnManager.Instance?.StopAllTurnTimers();
        UIMANAGER.Instance?.DisableButtons();
        UIMANAGER.Instance?.ClearTurnBanner();
        UIMANAGER.Instance?.CancelChallengeResponseCountdown();
    }

    /// <summary>True after Winner/Mazo committed or NuevaMano reload scheduled.</summary>
    public bool IsHandOutcomeLocked() => _handOutcomeCommitted || _restartScheduled || HandResolved;

    /// <summary>Match target reached; scoring cards may still be showing — no new cantos.</summary>
    public bool IsMatchEndPending() => _matchEndPending || _gameEnded;

    /// <summary>NuevaMano scene reload already queued â€” do not deal/start another hand.</summary>
    public bool IsRestartScheduled() => _restartScheduled;

    /// <summary>True once this mano has cards â€” used to block mid-hand re-deal on master rotate.</summary>
    public bool HasHandBeenDealt()
    {
        if (cardPlayed) return true;
        if (_player1Cards != null && _player1Cards.Count > 0) return true;
        if (_player2Cards != null && _player2Cards.Count > 0) return true;
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].value > 0)
                    return true;
            }
        }
        return false;
    }

    void ResetHandState()
    {
        HandResolved = false;
        cardPlayed = false;
        EnvidoFirstCallerActor = 0;
        trickResults.Clear();
        player1Score.Clear();
        player2Score.Clear();
        points = 1;
        challengePoints = 0;
        mazoPoints = 0;
        noQuieroPoints = 0;
        acceptedTrucoLevel = 0;
        _trickLeadActor = 0;
        envidoCantoCount = 0;
<<<<<<< Updated upstream
=======
        envidoResolvedThisHand = false;
        florResolvedThisHand = false;
        florOwnerActorThisHand = 0;
        _florCardsRevealedThisHand = false;
        conFlorQuieroPending = false;
        _matchEndPending = false;
        _matchEndRevealRoutineRunning = false;
        _handCloseHoldActive = false;
        _envidoRevealOwed = false;
        _envidoRevealHeld = false;
>>>>>>> Stashed changes
        lastChallengeType = ChallengeType.None;
        ActiveChallenges.Clear();
        _pendingScoringCardReveal = false;
        _scoringRevealFlushScheduled = false;
    }

    /// <summary>Called after reconnect so the returning client catches up on score / turn / canto state.</summary>
    public void RequestStateSyncAfterReconnect()
    {
        if (_isSpectator || _gameEnded || !PhotonNetwork.InRoom) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "RequestStateSyncAfterReconnect master=" + PhotonNetwork.IsMasterClient
            + " lastCh=" + lastChallengeType + " chPts=" + challengePoints + " mazo=" + mazoPoints);
        if (PhotonNetwork.IsMasterClient)
            BroadcastMatchState();
        else
            photonView.RPC(nameof(RequestSyncFromClient), RpcTarget.MasterClient);
        // Peer who still has live canto UI pushes snapshot (covers master reconnect wipe).
        photonView.RPC(nameof(RequestChallengeSnapshot), RpcTarget.Others);
    }

    [PunRPC]
    void RequestSyncFromClient()
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        BroadcastMatchState();
    }

    [PunRPC]
    void RequestChallengeSnapshot()
    {
        if (_isSpectator || _gameEnded || UIMANAGER.Instance == null) return;
        bool pending = UIMANAGER.Instance._isChallengepPending
                       || UIMANAGER.Instance.unAnsweredChallenges.Count > 0
                       || lastChallengeType != ChallengeType.None;
        if (!pending)
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "RequestChallengeSnapshot â€” nothing pending locally");
            return;
        }
        BuildChallengeSnapshot(out int type, out int chPts, out int mzPts, out int pendingFlag,
            out int raiser, out int responder, out int trucoLv, out int envidoFlag, out int lastRaise);
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "RequestChallengeSnapshot â†’ send type=" + type + " raiser=" + raiser
            + " responder=" + responder + " pending=" + pendingFlag);
        photonView.RPC(nameof(ApplyChallengeSnapshot), RpcTarget.Others,
            type, chPts, mzPts, pendingFlag, raiser, responder, trucoLv, envidoFlag, lastRaise);
    }

    void BuildChallengeSnapshot(out int type, out int chPts, out int mzPts, out int pendingFlag,
        out int raiser, out int responder, out int trucoLv, out int envidoFlag, out int lastTrucoRaise)
    {
        type = (int)lastChallengeType;
        chPts = challengePoints;
        mzPts = mazoPoints;
        pendingFlag = 0;
        raiser = 0;
        responder = 0;
        trucoLv = 0;
        envidoFlag = 0;
        lastTrucoRaise = 0;
        if (UIMANAGER.Instance == null) return;
        envidoFlag = UIMANAGER.Instance._envidoPlayed ? 1 : 0;
        lastTrucoRaise = UIMANAGER.Instance.LastTrucoRaiseActor;
        trucoLv = acceptedTrucoLevel;
        if (trucoLv == 0 && UIMANAGER.Instance.trucoPlayed) trucoLv = 1;
        if (UIMANAGER.Instance.unAnsweredChallenges.Count > 0)
        {
            foreach (var kv in UIMANAGER.Instance.unAnsweredChallenges)
            {
                type = (int)kv.Key;
                raiser = kv.Value;
            }
            pendingFlag = 1;
            if (TryGetOrderedTrucoActors(out int low, out int high))
                responder = raiser == low ? high : low;
        }
        else if (UIMANAGER.Instance._isChallengepPending || lastChallengeType != ChallengeType.None)
        {
            pendingFlag = UIMANAGER.Instance._isChallengepPending ? 1 : 0;
        }
    }

    void BroadcastMatchState()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!TryGetOrderedTrucoActors(out int lowActor, out int highActor)) return;
        int scoreLow = GetScoreForPhotonActor(lowActor);
        int scoreHigh = GetScoreForPhotonActor(highActor);
        int turnActor = TurnManager.Instance != null
            ? TurnManager.Instance.GetCurrentTurnActorNumber()
            : PhotonNetwork.LocalPlayer.ActorNumber;
        BuildChallengeSnapshot(out int type, out int chPts, out int mzPts, out int pendingFlag,
            out int raiser, out int responder, out int trucoLv, out int envidoFlag, out int lastRaise);
        string handJson = SerializeHandsForSync();
        string trickJson = SerializeTricksForSync();
        double turnStartedAt = TurnManager.Instance != null
            ? TurnManager.Instance.GetTurnStartedAtPhoton()
            : PhotonNetwork.Time;
        double chDeadline = UIMANAGER.Instance != null
            ? UIMANAGER.Instance.GetChallengeDeadlinePhoton()
            : -1;
        photonView.RPC(nameof(SyncMatchState), RpcTarget.Others,
            scoreLow, scoreHigh, turnActor, DataHandler.Instance.roundNumber, HandResolved ? 1 : 0,
            type, chPts, mzPts, pendingFlag, raiser, responder, trucoLv, envidoFlag, lastRaise,
            turnStartedAt, chDeadline, handJson ?? "", trickJson ?? "");
        TrucoRulesScenarioLog.Ok("BroadcastMatchState by actor",
            "a" + lowActor + "=" + scoreLow + " a" + highActor + "=" + scoreHigh
            + " turn=" + turnActor + " round=" + DataHandler.Instance.roundNumber
            + " ch=" + (ChallengeType)type + " pending=" + pendingFlag
            + " raiser=" + raiser + " responder=" + responder
            + " turnAt=" + turnStartedAt + " chDl=" + chDeadline);
    }

    string SerializeHandsForSync()
    {
        if (_player1Cards == null || _player2Cards == null) return "";
        string p1 = JsonUtility.ToJson(new DeckCardsWrapper { cards = _player1Cards });
        string p2 = JsonUtility.ToJson(new DeckCardsWrapper { cards = _player2Cards });
        return p1 + "!" + p2;
    }

    string SerializeTricksForSync()
    {
        var w = new TrickSyncWrapper
        {
            p1 = player1Score != null ? new List<ScoreClass>(player1Score) : new List<ScoreClass>(),
            p2 = player2Score != null ? new List<ScoreClass>(player2Score) : new List<ScoreClass>(),
            tricks = trickResults != null ? new List<int>(trickResults) : new List<int>(),
            cardPlayed = cardPlayed ? 1 : 0
        };
        return JsonUtility.ToJson(w);
    }

    void ApplyHandTrickStateFromSync(string handJson, string trickJson)
    {
        if (!string.IsNullOrEmpty(handJson) && handJson.Contains("!"))
        {
            string[] parts = handJson.Split('!');
            if (parts.Length >= 2)
            {
                var w1 = JsonUtility.FromJson<DeckCardsWrapper>(parts[0]);
                var w2 = JsonUtility.FromJson<DeckCardsWrapper>(parts[1]);
                if (w1 != null && w1.cards != null) _player1Cards = w1.cards;
                if (w2 != null && w2.cards != null) _player2Cards = w2.cards;
            }
        }
        if (!string.IsNullOrEmpty(trickJson))
        {
            var tw = JsonUtility.FromJson<TrickSyncWrapper>(trickJson);
            if (tw != null)
            {
                player1Score = tw.p1 ?? new List<ScoreClass>();
                player2Score = tw.p2 ?? new List<ScoreClass>();
                trickResults = tw.tricks ?? new List<int>();
                cardPlayed = tw.cardPlayed == 1;
            }
        }
        List<DeckCards> localDeal = PhotonNetwork.IsMasterClient ? _player1Cards : _player2Cards;
        List<ScoreClass> myPlayed = PhotonNetwork.IsMasterClient ? player1Score : player2Score;
        List<ScoreClass> oppPlayed = PhotonNetwork.IsMasterClient ? player2Score : player1Score;
        UIMANAGER.Instance?.RebuildHandAndTricksFromSync(localDeal, myPlayed, oppPlayed);
    }

    [PunRPC]
    void SyncMatchState(int scoreLowActor, int scoreHighActor, int turnActor, int roundNum, int handResolvedFlag,
        int challengeType, int challengePts, int mazoPts, int pendingFlag,
        int raiserActor, int responderActor, int trucoLevel, int envidoPlayedFlag, int lastTrucoRaiseActor = 0,
        double turnStartedAt = -1, double challengeDeadline = -1, string handJson = "", string trickJson = "")
    {
        if (_isSpectator || _gameEnded) return;
        ApplyAuthoritativeScores(scoreLowActor, scoreHighActor);
        DataHandler.Instance.roundNumber = roundNum;
        TrucoRulesScenarioLog.Ok("SyncMatchState applied",
            "scoreLow=" + scoreLowActor + " scoreHigh=" + scoreHighActor
            + " turnActor=" + turnActor + " round=" + roundNum
            + " handResolved=" + handResolvedFlag
            + " ch=" + (ChallengeType)challengeType + " pending=" + pendingFlag
            + " responder=" + responderActor
            + " turnAt=" + turnStartedAt + " chDl=" + challengeDeadline
            + " me=" + (myPlayerScoreHandler != null ? myPlayerScoreHandler.GetCurrentScore() : -1)
            + " opp=" + (otherPlayerScoreHandler != null ? otherPlayerScoreHandler.GetCurrentScore() : -1));
        if (handResolvedFlag == 1 && !HandResolved)
            MarkHandResolved();
        // After Mazo/Winner: catch up to NuevaMano instead of freezing with timer + stale table.
        if (HandResolved || _restartScheduled || _handOutcomeCommitted || handResolvedFlag == 1)
        {
            SetMyTurn(false);
            SetCanPlayCard(false);
            TurnManager.Instance?.StopAllTurnTimers();
            UIMANAGER.Instance?.DisableButtons();
            // A local reveal hold (Flor / Envido / final card) already owns the close — never
            // short-circuit it with an early ResetGame (that hid the Flor cards after NoQuiero).
            if (!_gameEnded && !_restartScheduled && handResolvedFlag == 1
                && !_handCloseHoldActive && _restartRoutine == null)
            {
                if (!EvaluateMatchOutcomeAfterPoints())
                    ResetGame();
            }
            return;
        }
        ApplyHandTrickStateFromSync(handJson, trickJson);
        ApplyChallengeSnapshot(challengeType, challengePts, mazoPts, pendingFlag,
            raiserActor, responderActor, trucoLevel, envidoPlayedFlag, lastTrucoRaiseActor);
        if (challengeDeadline > 0)
            UIMANAGER.Instance?.ApplySharedChallengeDeadline(challengeDeadline);
        if (pendingFlag == 1
            && responderActor == PhotonNetwork.LocalPlayer.ActorNumber
            && challengeType > 0)
        {
            // Responder must answer canto - do not resume card play yet.
            SetCanPlayCard(false);
            TurnManager.Instance?.StopAllTurnTimers();
            if (challengeDeadline > 0)
                UIMANAGER.Instance?.BeginChallengeResponseCountdown(challengeDeadline);
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
                "SyncMatchState - local owes challenge response, skip ResumeTurn");
            return;
        }
        TurnManager.Instance?.ResumeTurnAfterSync(turnActor);
        TurnManager.Instance?.RestartTurnTimersIfActive(force: false, startedAt: turnStartedAt);
    }

    [PunRPC]
    void ApplyChallengeSnapshot(int challengeType, int challengePts, int mazoPts, int pendingFlag,
        int raiserActor, int responderActor, int trucoLevel, int envidoPlayedFlag, int lastTrucoRaiseActor = 0)
    {
        if (_isSpectator || _gameEnded) return;
        lastChallengeType = (ChallengeType)challengeType;
        challengePoints = challengePts;
        mazoPoints = mazoPts;
        acceptedTrucoLevel = trucoLevel;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "ApplyChallengeSnapshot type=" + lastChallengeType + " pts=" + challengePts
            + " mazo=" + mazoPts + " pending=" + pendingFlag
            + " raiser=" + raiserActor + " responder=" + responderActor
            + " trucoLv=" + trucoLevel + " envido=" + envidoPlayedFlag
            + " lastRaise=" + lastTrucoRaiseActor);
        UIMANAGER.Instance?.RestoreChallengeStateFromSync(
            lastChallengeType, pendingFlag == 1, raiserActor, responderActor,
            trucoLevel, envidoPlayedFlag == 1, lastTrucoRaiseActor);
    }

    private void Start()
    {
        if (_isSpectator)
        {
            EnterSpectatorMode();
            return;
        }
        CheckTournamentStatus();
        InvokeRepeating(nameof(CacheTrucoOpponentUserId), 1.5f, 2f);
        if (PhotonNetwork.IsMasterClient)
            BroadcastAuthoritativeScores();
        // Fresh hand: scores only. The full reconnect sync + RequestChallengeSnapshot here rebuilt the
        // mano's in-flight Flor canto on the slower phone (Flor response buttons flashed on the guest).
        // Real reconnects still call RequestStateSyncAfterReconnect() from TrucoPunReconnectionManager.
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && photonView != null)
            photonView.RPC(nameof(RequestScoresFromClient), RpcTarget.MasterClient);
        if (!_isInTournament && !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId))
        {
            // Only once per match â€” scene reloads every mano would otherwise spam POST /start-game.
            if (PhotonNetwork.IsMasterClient && !OneVsOneMatchSession.GameStarted)
            {
                TrucoRulesScenarioLog.Backend("POST /start-game",
                    "match=" + OneVsOneMatchSession.CurrentMatchId);
                _ = ApiController.TryStartMatchGame1v1(OneVsOneMatchSession.CurrentMatchId);
            }
            OneVsOneMatchSession.MarkGameStarted();
        }
        // Safety: never leave the screen black if deal RPC is delayed.
        Invoke(nameof(ReleaseTransitionCoverSafe), 2.5f);
    }

    void ReleaseTransitionCoverSafe() => TrucoSceneTransition.ReleaseCover();

    void OnDestroy()
    {
        CancelInvoke(nameof(CacheTrucoOpponentUserId));
        if (!_isSpectator)
            TrucoReturnFromGameplayCleanup.MarkLeavingGameplay(_1v1ResultPosted || _1v1SettlementBusy);
    }

    void CacheTrucoOpponentUserId()
    {
        if (_gameEnded || _isSpectator || !PhotonNetwork.InRoom) return;
        var id = PhotonPlayerHelper.GetOtherTrucoPlayerUserId();
        if (!string.IsNullOrEmpty(id)) OneVsOneMatchSession.SetCachedOpponentUserId(id);
    }

    /// <summary>Public for <see cref="TrucoPunReconnectionManager"/> / UI exit.</summary>
    public bool IsTournamentGameplay() => _isInTournament;

    public bool Is1v1SettlementBusy() => _1v1SettlementBusy;

    /// <summary>After reconnect window expires: 1v1 = if match started and we could not rejoin, treat as mutual disconnect (cancel+refund attempt) unless a stayer already claimed walkover. Tournament = leave + loss.</summary>
    public void HandleReconnectionFailedExit()
    {
        if (_isSpectator)
        {
            OneVsOneMatchSession.Clear();
            TrucoSceneTransition.Go("MainMenu");
            return;
        }
        if (_isInTournament)
        {
            if (!_tournamentMatchFinalized) _tournamentMatchFinalized = true;
            MultiplayerController.LeaveTournamentMatchmaking(null);
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            if (ApiController.GetSessionUser?.Data?.stats != null)
                ApiController.GetSessionUser.Data.stats.losses++;
            OneVsOneMatchSession.Clear();
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconectarFallo);
            TrucoSceneTransition.Go("MainMenu");
            return;
        }
        string matchIdSnapshot = OneVsOneMatchSession.CurrentMatchId;
        bool gameHadStarted = OneVsOneMatchSession.GameStarted;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "HandleReconnectionFailedExit match=" + (matchIdSnapshot ?? "?")
            + " gameStarted=" + gameHadStarted
            + " inRoom=" + PhotonNetwork.InRoom
            + " resultPosted=" + _1v1ResultPosted);
        // Do NOT award the opponent from the failing client — the stayer uses WinByOpponentWalkover.
        // If both fail to reconnect, both call mutual cancel (leave+end) for refund/cleanup.
        // Neither player is the winner; do not increment losses for mutual disconnect.
        if (!_1v1ResultPosted && !string.IsNullOrEmpty(matchIdSnapshot))
        {
            _1v1ResultPosted = true;
            if (gameHadStarted)
            {
                TrucoRulesScenarioLog.Backend("MUTUAL_OR_SELF reconnect-fail → cancel+refund (no winner)",
                    "match=" + matchIdSnapshot);
                _ = ApiController.CancelMutualDisconnect1v1(matchIdSnapshot);
            }
            else
            {
                TrucoRulesScenarioLog.Backend("Pre-game reconnect-fail → CancelPreGame",
                    "match=" + matchIdSnapshot);
                _ = ApiController.CancelPreGameMatch1v1(matchIdSnapshot);
            }
        }
        OneVsOneMatchSession.Clear();
        AppManager.Instance?.DisplayNotification(
            gameHadStarted
                ? TrucoTextosClient.DesconexionMutuaReembolso
                : TrucoTextosClient.ReconectarFallo);
        TrucoSceneTransition.Go("MainMenu");
    }

    void EnterSpectatorMode()
    {
        if (UIMANAGER.Instance != null && UIMANAGER.Instance.myPlayerCards != null)
        {
            foreach (var go in UIMANAGER.Instance.myPlayerCards)
            {
                if (go != null) go.SetActive(false);
            }
        }
        UIMANAGER.Instance?.DisableButtons();
        SpectatorOverlayRuntime.Create();
    }

    #region Tournament

    private void ToggleMenuBtns(bool state)
    {
        foreach (Button btn in _menuBtns)
        {
            btn.gameObject.SetActive(state);
        }
    }

    private void CheckTournamentStatus()
    {
        _isInTournament = MultiplayerController.IsTournamentActive;

        if (!_isInTournament)
        {
            ToggleMenuBtns(true);
            return;
        }

        ToggleMenuBtns(false);
        TournamentManager.Instance.HideTournamentWaitingRoomOverlay();
        Debug.Log($"[GameManager] Tournament status: {_isInTournament}");
    }

    private void OnTournamentMatchWon()
    {
        _tournamentMatchFinalized = true;
        ToggleMenuBtns(false);
        winPanel.SetActive(true);

        if (IsInFinalBracket())
        {
            UIMANAGER.Instance.UpdateTurnText("Tournament Champion!", 5f);
            AppManager.Instance.DisplayNotification(TrucoTextosClient.ChampionCongrats);
            MultiplayerController.FinalizeCurrentMatch(true);

            LeanTween.delayedCall(5, () =>
            {
                ToggleMenuBtns(true);
            });
        }
        else
        {
            UIMANAGER.Instance.UpdateTurnText("Waiting for next round...", 4f);

            LeanTween.delayedCall(5, () =>
            {
                SceneManager.LoadScene("TempScene");
                MultiplayerController.PlayNextBracket();
            });
        }
    }

    private void OnTournamentMatchLose()
    {
        _tournamentMatchFinalized = true;
        ToggleMenuBtns(false);
        losePanel.SetActive(true);

        MultiplayerController.LeaveTournamentMatchmaking(() =>
        {
            PhotonNetwork.Disconnect();
            ToggleMenuBtns(true);
        });
    }

    private bool IsInFinalBracket()
    {
        var bracketInfo = MultiplayerController.CurrentBracketLevel;
        return bracketInfo.bracketTitle == "Finals" || bracketInfo.roundNumber <= 1;
    }

    #endregion

    [PunRPC]
    private void NoFlor()
    {
        if (_isSpectator) return;
        otherPlayerDeniedFlor = true;
    }

    public void ShowOtherPlayerScore(int _score)
    {
        otherPlayerScoreHandler.SetScore(_score);
        if (DataHandler.Instance != null)
            DataHandler.Instance.opponentPoints = _score;
        // A side-bet score reaching the target does not end the match until this hand finishes.
    }

    public void SetCards()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            _player1Cards = new List<DeckCards>();
            _player2Cards = new List<DeckCards>();
            deck.InitializeDeck();
            for (int i = 0; i < cards.Count; i++)
            {
                DeckCards _card = deck.DrawRandomCard();
                _player1Cards.Add(_card);
                cards[i].SetupCard(_card.suit, _card.rank);
            }
            UIMANAGER.Instance?.ShowDealtHandCards();
            List<DeckCards> player2Cards = new List<DeckCards>();
            for (int i = 0; i < 3; i++)
            {
                DeckCards _card = deck.DrawRandomCard();
                _player2Cards.Add(_card);
                player2Cards.Add(_card);
            }
            string player1JsonString = JsonUtility.ToJson(new DeckCardsWrapper { cards = _player1Cards });
            string player2JsonString = JsonUtility.ToJson(new DeckCardsWrapper { cards = _player2Cards });
            string jsonString = player1JsonString + "!" + player2JsonString;
            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.Others,
                CachingOption = EventCaching.AddToRoomCache
            };
            PhotonNetwork.RaiseEvent(_cardSendEvent, jsonString, options, SendOptions.SendReliable);
            if (!PlayerHasFlor())
            {
                deniedFlor = true;
                photonView.RPC(nameof(NoFlor), RpcTarget.OthersBuffered);
            }
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (TrucoPunChallenges.IsChallengeEventCode(photonEvent.Code))
            TrucoGameplayAudio.PlayFromPhotonEvent(photonEvent);
        if (_isSpectator) return;
        if (photonEvent.Code == _cardSendEvent || TrucoPunChallenges.IsChallengeEventCode(photonEvent.Code))
            TrucoDebugLog.Log(TrucoDebugLog.Category.Rules, "EventReceived: " + photonEvent.Code);
        if (photonEvent.Code == _cardSendEvent)
        {
            string jsonString = photonEvent.CustomData as string;
            string player1Cards = jsonString.Split("!")[0];
            string player2Cards = jsonString.Split("!")[1];
            DeckCardsWrapper _cardWrapper1 = JsonUtility.FromJson<DeckCardsWrapper>(player1Cards);
            DeckCardsWrapper _cardWrapper2 = JsonUtility.FromJson<DeckCardsWrapper>(player2Cards);
            _player1Cards = _cardWrapper1.cards;
            _player2Cards = _cardWrapper2.cards;
            bool localWasDealingPlayer = photonEvent.Sender > 0
                                         && PhotonNetwork.LocalPlayer != null
                                         && photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber;
            List<DeckCards> localCards = localWasDealingPlayer ? _cardWrapper1.cards : _cardWrapper2.cards;
            if (localCards != null)
            {
                for (int i = 0; i < localCards.Count && i < cards.Count; i++)
                {
                    cards[i].SetupCard(localCards[i].suit, localCards[i].rank);
                }
            }
            UIMANAGER.Instance?.ShowDealtHandCards();
            if (!PlayerHasFlor())
            {
                deniedFlor = true;
                photonView.RPC(nameof(NoFlor), RpcTarget.OthersBuffered);
            }
        }
        else if (photonEvent.Code == UIMANAGER.TRUCO_CHALLENGE)
        {
            challengePoints = 1;
            lastChallengeType = ChallengeType.Truco;
            if (photonEvent.Sender > 0)
            {
                UIMANAGER.Instance.unAnsweredChallenges[ChallengeType.Truco] = photonEvent.Sender;
                UIMANAGER.Instance.MarkTrucoRaiseByActor(photonEvent.Sender);
            }
            TrucoRulesScenarioLog.Opp("RECV Truco", "fromActor=" + photonEvent.Sender + " responseTimer=30s");
            UIMANAGER.Instance.TrucoChallenged();
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.RETRUCO_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV Retruco", "fromActor=" + photonEvent.Sender);
            challengePoints = 2;
            if (photonEvent.Sender > 0)
                UIMANAGER.Instance.MarkTrucoRaiseByActor(photonEvent.Sender);
            UIMANAGER.Instance.RetrucoChallenged();
            lastChallengeType = ChallengeType.Retruco;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.VALE4_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV Vale4", "fromActor=" + photonEvent.Sender);
            challengePoints = 3;
            if (photonEvent.Sender > 0)
                UIMANAGER.Instance.MarkTrucoRaiseByActor(photonEvent.Sender);
            UIMANAGER.Instance.Vale4Challenged();
            lastChallengeType = ChallengeType.Vale4;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.ENVIDO_CHALLENGE)
        {
            if (lastChallengeType != ChallengeType.Envido)
            {
                challengePoints = 0;
            }
            challengePoints += 2;
            TrackEnvidoCanto(ChallengeType.Envido);
            MarkEnvidoFirstCaller(photonEvent.Sender);
            TrucoRulesScenarioLog.Opp("RECV Envido", "fromActor=" + photonEvent.Sender + " challengePts=" + challengePoints
                + " cantos=" + envidoCantoCount);
            UIMANAGER.Instance.EnvidoChallenged();
            lastChallengeType = ChallengeType.Envido;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.REALENVIDO_CHALLENGE)
        {
            if (lastChallengeType == ChallengeType.Truco)
            {
                challengePoints = 0;
            }
            challengePoints += 3;
            TrackEnvidoCanto(ChallengeType.RealEnvido);
            MarkEnvidoFirstCaller(photonEvent.Sender);
            TrucoRulesScenarioLog.Opp("RECV RealEnvido", "fromActor=" + photonEvent.Sender + " challengePts=" + challengePoints
                + " cantos=" + envidoCantoCount);
            UIMANAGER.Instance.RealEnvidoChallenged();
            lastChallengeType = ChallengeType.RealEnvido;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.FALTAENVIDO_CHALLENGE)
        {
            TrackEnvidoCanto(ChallengeType.FaltaEnvido);
            MarkEnvidoFirstCaller(photonEvent.Sender);
            TrucoRulesScenarioLog.Opp("RECV FaltaEnvido", "fromActor=" + photonEvent.Sender
                + " cantos=" + envidoCantoCount);
            lastChallengeType = ChallengeType.FaltaEnvido;
            UIMANAGER.Instance.FaltaEnvidoChallenged();
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.QUEIRO_CHALLENGE)
        {
            double syncStartedAt = ParseTimerSyncFromEvent(photonEvent);
            // Snapshot BEFORE QueiroChallenged — ResumePendingTruco can overwrite lastChallengeType.
            ChallengeType acceptedType = lastChallengeType;
            int acceptedPts = challengePoints;
            TrucoRulesScenarioLog.Opp("RECV Quiero (accept)",
                "fromActor=" + photonEvent.Sender + " for=" + acceptedType + " pts=" + acceptedPts);
            UIMANAGER.Instance.QueiroChallenged(syncStartedAt);
            if (acceptedType == ChallengeType.Envido ||
                acceptedType == ChallengeType.RealEnvido ||
                acceptedType == ChallengeType.FaltaEnvido)
            {
                // QueiroChallenged cleared stake — restore for scoring.
                challengePoints = acceptedPts;
                lastChallengeType = acceptedType;
                // Only Master scores. Master-as-acceptor already scored in ChallengeQueiro.
                if (PhotonNetwork.IsMasterClient
                    && photonEvent.Sender != PhotonNetwork.LocalPlayer.ActorNumber)
                    GetScore(acceptedType);
            }
            else if (acceptedType == ChallengeType.ContraFlor)
            {
                TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
                    "RECV Quiero ContraFlor - defer card reveal (no early ShowAllCards)");
                photonView.RPC(nameof(GetScore), RpcTarget.MasterClient, ChallengeType.ContraFlor);
            }
            else if (acceptedType == ChallengeType.Truco)
            {
                MarkTrucoChainAccepted(ChallengeType.Truco);
            }
            else if (acceptedType == ChallengeType.Retruco)
            {
                MarkTrucoChainAccepted(ChallengeType.Retruco);
            }
            else if (acceptedType == ChallengeType.Vale4)
            {
                MarkTrucoChainAccepted(ChallengeType.Vale4);
            }
            if (UIMANAGER.Instance.unAnsweredChallenges.Count > 0)
            {
                StartCoroutine(UIMANAGER.Instance.CheckForUnansweredChallenges());
            }
            else if (IsMyTurn())
            {
                SetCanPlayCard(true);
            }
        }
        else if (photonEvent.Code == UIMANAGER.NOQUEIRO_CHALLENGE)
        {
            ChallengeType resolvedType = ParseChallengeTypeFromEvent(photonEvent, lastChallengeType);
            int forcedPts = ParseAwardPointsFromEvent(photonEvent);
            int previewPts = forcedPts >= 0
                ? forcedPts
                : (TrucoRulePoints.IsEnvidoFamily(resolvedType)
                    ? TrucoRulePoints.EnvidoNoQuieroAward(resolvedType, challengePoints, envidoCantoCount)
                    : TrucoRulePoints.NoQuieroAward(resolvedType));
            double syncStartedAt = ParseTimerSyncFromEvent(photonEvent);
            TrucoRulesScenarioLog.Opp("RECV NoQuiero (decline)",
                "fromActor=" + photonEvent.Sender + " type=" + resolvedType
                + " awardPts=" + previewPts
                + " endsHand=" + TrucoRulePoints.NoQuieroEndsHand(resolvedType));
            UIMANAGER.Instance.NoQueiroChallenged(syncStartedAt);
            if (resolvedType == ChallengeType.Truco ||
                resolvedType == ChallengeType.Retruco ||
                resolvedType == ChallengeType.Vale4)
                UIMANAGER.Instance.trucoPlayed = true;
            if (PhotonNetwork.IsMasterClient)
                MasterResolveNoQuiero(photonEvent.Sender, resolvedType, previewPts);
            challengePoints = 0;
        }
        else if (photonEvent.Code == UIMANAGER.FLOR_CHALLENGE)
        {
            bool autoAward = photonEvent.CustomData is byte fb && fb == UIMANAGER.FlorAutoAwardFlag;
            if (autoAward)
            {
                TrucoRulesScenarioLog.Opp("RECV Flor AUTO-AWARD +3", "fromActor=" + photonEvent.Sender);
                // Sender already closed UI locally — only peers need CompleteFlorAutoAwardUi.
                if (photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber)
                    return;
                UIMANAGER.Instance._florPlayed = true;
                UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
                UIMANAGER.Instance.CompleteFlorAutoAwardUi("recv auto award");
                return;
            }
            if (deniedFlor)
            {
                TrucoRulesScenarioLog.Opp("RECV Flor -> auto +3 (local deniedFlor)", "fromActor=" + photonEvent.Sender);
                UIMANAGER.Instance._florPlayed = true;
                UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
                if (photonEvent.Sender > 0)
                    RequestNetworkAward(photonEvent.Sender, 3, false);
                // Notify rival only (not self) so raiser leaves pending Flor without double UI.
                TrucoPunChallenges.RaiseToOthers(UIMANAGER.FLOR_CHALLENGE, UIMANAGER.FlorAutoAwardFlag);
                UIMANAGER.Instance.CompleteFlorAutoAwardUi("recv local denied flor");
                return;
            }
            TrucoRulesScenarioLog.Opp("RECV Flor challenge", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.Flor;
            if (!UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Flor))
                UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
            if (photonEvent.Sender > 0)
                UIMANAGER.Instance.unAnsweredChallenges[ChallengeType.Flor] = photonEvent.Sender;
            UIMANAGER.Instance.FlorChallenged();
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown(ParseChallengeDeadlineFromEvent(photonEvent));
        }
        else if (photonEvent.Code == UIMANAGER.FLOR_CHICA_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV FlorChica -> rival gets +4", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.FlorChica;
            UIMANAGER.Instance.FlorChicaChallenged();
            if (PhotonNetwork.IsMasterClient && photonEvent.Sender > 0)
                BroadcastNetworkAward(GetOpponentActorNumber(photonEvent.Sender), 4, false);
            // Sender already resumed locally after RaiseToAll — peers only.
            if (photonEvent.Sender != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                if (!UIMANAGER.Instance.ResumePendingTrucoChainAfterFlorPublic())
                {
                    if (IsMyTurn())
                        SetCanPlayCard(true);
                    TurnManager.Instance?.RestartTurnTimersIfActive(force: true);
                }
            }
        }
        else if (photonEvent.Code == UIMANAGER.CON_FLOR_QUIERO_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV ConFlorQuiero", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.ConFlorQuiero;
            challengePoints = 6;
            UIMANAGER.Instance.ConFlorQuieroChallenged();
            if (photonEvent.Sender != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                if (!UIMANAGER.Instance.ResumePendingTrucoChainAfterFlorPublic())
                {
                    if (IsMyTurn())
                        SetCanPlayCard(true);
                    TurnManager.Instance?.RestartTurnTimersIfActive(force: true);
                }
            }
        }
        else if (photonEvent.Code == UIMANAGER.CONTRA_FLOR_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV ContraFlor", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.ContraFlor;
            UIMANAGER.Instance.ContraFlorChallenged();
        }
        else if (photonEvent.Code == UIMANAGER.MAZO_CHALLENGE)
        {
            if (_isSpectator || _gameEnded || HandResolved || _handOutcomeCommitted || _restartScheduled)
            {
                TrucoRulesScenarioLog.Ok("RECV Mazo ignored (already resolved)",
                    "handResolved=" + HandResolved
                    + " committed=" + _handOutcomeCommitted
                    + " restart=" + _restartScheduled);
                return;
            }
            if (!PhotonNetwork.IsMasterClient) return;
            int winnerActor = GetOpponentActorNumber(photonEvent.Sender);
            TrucoRulesScenarioLog.Ok("RECV Mazo â†’ master ResolveMazoHand",
                "folderActor=" + photonEvent.Sender + " winnerActor=" + winnerActor);
            ResolveMazoHand(winnerActor);
        }
    }

    static int GetOpponentActorNumber(int actor)
    {
        foreach (var p in PhotonNetwork.PlayerList)
            if (p != null && p.ActorNumber != actor)
                return p.ActorNumber;
        return actor;
    }

    public void MarkTrucoChainAccepted(ChallengeType type)
    {
        int level = 0;
        if (type == ChallengeType.Truco) level = 1;
        else if (type == ChallengeType.Retruco) level = 2;
        else if (type == ChallengeType.Vale4) level = 3;
        if (level <= 0) return;
        if (level > acceptedTrucoLevel) acceptedTrucoLevel = level;
        mazoPoints = TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(acceptedTrucoLevel);
        if (UIMANAGER.Instance != null)
            UIMANAGER.Instance.trucoPlayed = true;
    }

    int ComputeMazoHandPoints()
    {
        StripEnvidoFlorFromActiveChallenges();
        if (TrucoRulePoints.IsEnvidoFamily(lastChallengeType) || IsFlorFamilyInHand())
            lastChallengeType = ChallengeType.None;

        int level = acceptedTrucoLevel;
        if (level <= 0 && UIMANAGER.Instance != null && UIMANAGER.Instance.trucoPlayed)
            level = 1;
        int award = TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(level);
        TrucoRulesScenarioLog.Ok("ComputeMazoHandPoints",
            "acceptedLevel=" + level + " award=" + award
            + " last=" + lastChallengeType
            + " trucoPlayed=" + (UIMANAGER.Instance != null && UIMANAGER.Instance.trucoPlayed));
        return award;
    }

    void StripEnvidoFlorFromActiveChallenges()
    {
        ActiveChallenges.Remove(ChallengeType.Envido);
        ActiveChallenges.Remove(ChallengeType.RealEnvido);
        ActiveChallenges.Remove(ChallengeType.FaltaEnvido);
        ActiveChallenges.Remove(ChallengeType.Flor);
        ActiveChallenges.Remove(ChallengeType.ContraFlor);
        ActiveChallenges.Remove(ChallengeType.ConFlorQuiero);
        ActiveChallenges.Remove(ChallengeType.FlorChica);
    }

    bool IsFlorFamilyInHand()
    {
        if (lastChallengeType == ChallengeType.Flor ||
            lastChallengeType == ChallengeType.FlorChica ||
            lastChallengeType == ChallengeType.ConFlorQuiero ||
            lastChallengeType == ChallengeType.ContraFlor)
            return true;

        if (UIMANAGER.Instance == null) return false;
        return UIMANAGER.Instance._florPlayed ||
               UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Flor) ||
               UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.FlorChica) ||
               UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero) ||
               UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ContraFlor);
    }

    void ResolveMazoHand(int winnerActor)
    {
        if (_gameEnded || HandResolved || _handOutcomeCommitted || _restartScheduled) return;

        // Con Flor Quiero is scored at hand end — MAZO still ends the Truco part.
        bool conFlorPending = UIMANAGER.Instance != null
                              && UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero);
        if (conFlorPending && PhotonNetwork.IsMasterClient)
        {
            UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
            GetScore(ChallengeType.ConFlorQuiero);
            FlushPendingScoringCardReveal(immediate: true);
        }

        // P2 still has an unannounced Flor — award +3 and reveal before the MAZO Truco point.
        if (PhotonNetwork.IsMasterClient
            && UIMANAGER.Instance != null && !UIMANAGER.Instance._florPlayed)
        {
            int florActor = ActorWithUnannouncedFlor(winnerActor);
            if (florActor > 0)
            {
                UIMANAGER.Instance._florPlayed = true;
                if (!UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Flor))
                    UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
                QueueOpponentFlorReveal(florActor);
                FlushPendingScoringCardReveal(immediate: true);
                RequestNetworkAward(florActor, 3, false);
            }
        }

        MarkHandResolved();
        int points = ComputeMazoHandPoints();
        TrucoRulesScenarioLog.Ok("ResolveMazoHand",
            "winnerActor=" + winnerActor + " handPts=" + points
            + " last=" + lastChallengeType + " cardPlayed=" + cardPlayed
            + " trucoPlayed=" + (UIMANAGER.Instance != null && UIMANAGER.Instance.trucoPlayed));
<<<<<<< Updated upstream
        photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, points);
    }

=======
        if (florShown)
            StartCoroutine(CoSendHandWinnerAfterFlorReveal(winnerActor, points));
        else
            photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, points);
    }

    /// <summary>Flor cards must stay readable on the table before the hand result closes the board.</summary>
    IEnumerator CoSendHandWinnerAfterFlorReveal(int winnerActor, int points)
    {
        _handCloseHoldActive = true;
        UIMANAGER.Instance?.EnsureLocalHandSpritesVisible();
        UIMANAGER.Instance?.EnsureOpponentRevealSpritesVisible();
        yield return new WaitForSeconds(FlorRevealHoldSeconds);
        _handCloseHoldActive = false;
        if (_gameEnded || _handOutcomeCommitted || _restartScheduled) yield break;
        UIMANAGER.Instance?.EnsureLocalHandSpritesVisible();
        UIMANAGER.Instance?.EnsureOpponentRevealSpritesVisible();
        photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, points);
    }

    /// <summary>
    /// Master-only. The deciding card must finish its LeanMove and stay readable on the table before
    /// the hand result is applied; then any sung Flor is shown, then Winner goes out to every seat.
    /// </summary>
    IEnumerator CoCloseHandAfterFinalCard(int winnerActor)
    {
        _handCloseHoldActive = true;
        yield return new WaitForSeconds(FinalCardHoldSeconds);
        if (_gameEnded || _handOutcomeCommitted || _restartScheduled)
        {
            _handCloseHoldActive = false;
            yield break;
        }
        bool florShown = SettleFlorBeforeHandCloses(winnerActor, handConceded: false);
        int handPts = ComputeMazoHandPoints();
        TrucoRulesScenarioLog.Ok("Hand decided → Winner RPC",
            "winnerActor=" + winnerActor + " handPts=" + handPts
            + " tricks=" + trickResults.Count
            + " florShown=" + florShown
            + " acceptedTruco=" + acceptedTrucoLevel);
        if (florShown)
        {
            UIMANAGER.Instance?.EnsureLocalHandSpritesVisible();
            UIMANAGER.Instance?.EnsureOpponentRevealSpritesVisible();
            yield return new WaitForSeconds(FlorRevealHoldSeconds);
            if (_gameEnded || _handOutcomeCommitted || _restartScheduled)
            {
                _handCloseHoldActive = false;
                yield break;
            }
            UIMANAGER.Instance?.EnsureLocalHandSpritesVisible();
            UIMANAGER.Instance?.EnsureOpponentRevealSpritesVisible();
        }
        _handCloseHoldActive = false;
        photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, handPts);
    }

    /// <summary>
    /// Master-only. Flor scoring is independent of the Truco part, so before a hand closes we settle an
    /// open Con Flor Quiero and put the Flor owner's three cards on the table. When the hand was
    /// conceded (Mazo) a Flor the owner never got to sing is also announced and paid, since the cards
    /// would otherwise never be seen. Returns true when cards were revealed.
    /// </summary>
    bool SettleFlorBeforeHandCloses(int handWinnerActor, bool handConceded)
    {
        var ui = UIMANAGER.Instance;
        if (ui == null) return false;

        // Con Flor Quiero is scored at hand end — its 6 points are separate from the Truco part.
        if (conFlorQuieroPending || ui.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
        {
            conFlorQuieroPending = false;
            ui.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
            GetScore(ChallengeType.ConFlorQuiero);
            FlushPendingScoringCardReveal(immediate: true);
            return true;
        }

        // Opponent still holds an unannounced Flor — canto, cards and +3 before the Truco point.
        if (handConceded && !ui._florPlayed)
        {
            int florActor = ActorWithUnannouncedFlor(handWinnerActor);
            if (florActor > 0)
            {
                ui._florPlayed = true;
                if (!ui.invokedChallenges.Contains(ChallengeType.Flor))
                    ui.invokedChallenges.Add(ChallengeType.Flor);
                photonView.RPC(nameof(AnnounceFlorCanto), RpcTarget.All, florActor);
                BroadcastFlorResolved(florActor);
                QueueOpponentFlorReveal(florActor);
                _florCardsRevealedThisHand = true;
                FlushPendingScoringCardReveal(immediate: true);
                RequestNetworkAward(florActor, 3, false);
                return true;
            }
        }

        // Flor already sung (auto +3 etc.) — always show all three cards before the hand ends,
        // including early 2-trick wins and Vale4/hand closes that never reach the third trick.
        if (!ui._florPlayed && !florResolvedThisHand) return false;
        int owner = florOwnerActorThisHand > 0 ? florOwnerActorThisHand : SoleFlorHolderActor();
        if (owner <= 0) return false;
        if (!FlorOwnerHasUnplayedCard(owner)) return false;
        return RevealFlorCardsOnce(owner);
    }

    /// <summary>True when the Flor owner still has at least one card that never hit the table.</summary>
    bool FlorOwnerHasUnplayedCard(int florOwnerActor)
    {
        if (florOwnerActor <= 0) return true;
        if (!PhotonNetwork.IsMasterClient) return true;
        bool masterOwns = PhotonNetwork.LocalPlayer != null
                          && florOwnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        int played = masterOwns
            ? (player1Score != null ? player1Score.Count : 0)
            : (player2Score != null ? player2Score.Count : 0);
        return played < 3;
    }

    /// <summary>Master-only: show the Flor owner's three cards, at most once per hand.</summary>
    bool RevealFlorCardsOnce(int florOwnerActor)
    {
        if (!PhotonNetwork.IsMasterClient || _florCardsRevealedThisHand) return false;
        if (florOwnerActor <= 0) return false;
        _florCardsRevealedThisHand = true;
        QueueFlorCardReveal(florOwnerActor);
        FlushPendingScoringCardReveal(immediate: true);
        return true;
    }

    /// <summary>Master-only. The one seat holding a Flor, or 0 when neither or both do.</summary>
    int SoleFlorHolderActor()
    {
        if (!OneVsOneMatchSession.WithFlor) return 0;
        bool masterHasFlor = HasFlor(_player1Cards);
        bool guestHasFlor = HasFlor(_player2Cards);
        if (masterHasFlor == guestHasFlor) return 0;
        if (masterHasFlor) return PhotonNetwork.LocalPlayer.ActorNumber;
        return PhotonNetwork.PlayerListOthers.Length > 0
            ? PhotonNetwork.PlayerListOthers[0].ActorNumber
            : 0;
    }

    /// <summary>Every seat records who owns the resolved Flor so the hand-end reveal is deterministic.</summary>
    public void BroadcastFlorResolved(int florOwnerActor)
    {
        MarkFlorResolvedRpc(florOwnerActor);
        if (PhotonNetwork.InRoom && photonView != null)
            photonView.RPC(nameof(MarkFlorResolvedRpc), RpcTarget.Others, florOwnerActor);
    }

    [PunRPC]
    void MarkFlorResolvedRpc(int florOwnerActor)
    {
        florResolvedThisHand = true;
        if (florOwnerActor > 0) florOwnerActorThisHand = florOwnerActor;
    }

    /// <summary>Flor canto (voice + text) for a Flor raised by game logic instead of a button press.</summary>
    [PunRPC]
    void AnnounceFlorCanto(int florActor)
    {
        if (_isSpectator || UIMANAGER.Instance == null) return;
        bool mine = PhotonNetwork.LocalPlayer != null && florActor == PhotonNetwork.LocalPlayer.ActorNumber;
        TrucoGameplayAudio.PlayFlorCanto(mine);
    }

>>>>>>> Stashed changes
    int ActorWithUnannouncedFlor(int mazoWinnerActor)
    {
        if (!OneVsOneMatchSession.WithFlor) return 0;
        bool p1Flor = HasFlor(_player1Cards);
        bool p2Flor = HasFlor(_player2Cards);
        int masterActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int guestActor = PhotonNetwork.PlayerListOthers.Length > 0
            ? PhotonNetwork.PlayerListOthers[0].ActorNumber
            : 0;
        // Only the player who did NOT fold, and only if they still have an unannounced Flor.
        if (mazoWinnerActor == masterActor && p1Flor && !p2Flor) return masterActor;
        if (mazoWinnerActor == guestActor && p2Flor && !p1Flor) return guestActor;
        return 0;
    }

    void QueueOpponentFlorReveal(int winnerActor)
    {
        if (!PhotonNetwork.IsMasterClient || _player1Cards == null || _player2Cards == null) return;
        _pendingRevealMasterJson = SerializeRevealCards(_player1Cards);
        _pendingRevealGuestJson = SerializeRevealCards(_player2Cards);
        _pendingRevealWinnerActor = winnerActor;
        _pendingScoringCardReveal = true;
    }

    /// <summary>Master adjudicates Mazo; clients re-send the event as fallback if needed.</summary>
    public void ForceMazoTimeoutResolution()
    {
        if (_gameEnded || HandResolved || _handOutcomeCommitted || _restartScheduled)
        {
            TrucoRulesScenarioLog.Ok("ForceMazoTimeoutResolution skipped",
                "handResolved=" + HandResolved
                + " committed=" + _handOutcomeCommitted
                + " restart=" + _restartScheduled);
            return;
        }
        TrucoRulesScenarioLog.Ok("ForceMazoTimeoutResolution",
            "isMaster=" + PhotonNetwork.IsMasterClient);
        if (PhotonNetwork.IsMasterClient)
            ResolveMazoHand(GetOpponentActorNumber(PhotonNetwork.LocalPlayer.ActorNumber));
        else
            PhotonNetwork.RaiseEvent(UIMANAGER.MAZO_CHALLENGE, null,
                new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    public int CheckForMazo()
    {
        int pointsToAdd = 0;

        for (int i = 0; i < ActiveChallenges.Count; i++)
        {
            switch (ActiveChallenges[i])
            {
                case ChallengeType.Truco:
                    pointsToAdd = 1;
                    break;
                case ChallengeType.Retruco:
                    pointsToAdd = 2;
                    break;
                case ChallengeType.Vale4:
                    pointsToAdd = 3;
                    break;
                // Envido/Flor stakes are awarded at resolve time — never again on MAZO.
                case ChallengeType.Envido:
                case ChallengeType.RealEnvido:
                case ChallengeType.FaltaEnvido:
                case ChallengeType.Flor:
                case ChallengeType.ContraFlor:
                case ChallengeType.ConFlorQuiero:
                case ChallengeType.FlorChica:
                    break;
            }
        }

        if (!UIMANAGER.Instance.trucoPlayed && ActiveChallenges.Count <= 0)
        {
            mazoPoints = 1;
        }

        return pointsToAdd + mazoPoints;
    }

    public void CheckToAwardsPoints()
    {
        // Legacy path â€” scoring is master-authoritative via RPC_NetworkAwardPoints.
        if (!PhotonNetwork.IsMasterClient) return;
    }

    [PunRPC]
    private void CheckForTrick()
    {
        // Legacy no-op — ConFlorQuiero is scored from CheckAfterTurn before Winner only.
        if (_isSpectator) return;
    }

    [PunRPC]
    public void GetScore(ChallengeType _type)
    {
        if (_isSpectator) return;
        switch (_type)
        {
            case ChallengeType.Envido:
            case ChallengeType.RealEnvido:
                {
                    int player1Score = CalculateEnvido(_player1Cards);
                    int player2Score = CalculateEnvido(_player2Cards);

                    int myscore = myPlayerScoreHandler.GetCurrentScore();
                    int otherscore = otherPlayerScoreHandler.GetCurrentScore();
                    int biggerScore = Mathf.Max(myscore, otherscore);

                    int target = OneVsOneMatchSession.TargetScore;
                    int pointsToAward = (biggerScore + challengePoints) > target ? target - biggerScore : challengePoints;

                    TrucoRulesScenarioLog.Ok("GetScore Envido/Real compare",
                        "type=" + _type + " p1Envido=" + player1Score + " p2Envido=" + player2Score
                        + " awardPts=" + pointsToAward + " challengePts=" + challengePoints);

                    int winnerActor = TrucoMatchRules.ResolveEnvidoWinnerActor(
                        player1Score, player2Score,
                        PhotonNetwork.LocalPlayer.ActorNumber,
                        PhotonNetwork.PlayerListOthers[0].ActorNumber,
                        GetManoActorForCurrentHand());

                    photonView.RPC(nameof(AnnounceEnvidoPoints), RpcTarget.All,
                        player1Score, player2Score, winnerActor, EnvidoFirstCallerActor);
                    QueueEnvidoCardReveal(winnerActor);
                    photonView.RPC(nameof(EnvidoWinner), RpcTarget.All, winnerActor, pointsToAward);

                    Debug.LogWarning("Envido Points Awarded");
                    challengePoints = 0;
                    break;
                }
            case ChallengeType.FaltaEnvido:
                {
                    int player1Score = CalculateEnvido(_player1Cards);
                    int player2Score = CalculateEnvido(_player2Cards);
                    int myscore = myPlayerScoreHandler.GetCurrentScore();
                    int otherscore = otherPlayerScoreHandler.GetCurrentScore();
                    int biggerScore = Mathf.Max(myscore, otherscore);
                    int faltaPts = OneVsOneMatchSession.TargetScore - biggerScore;
                    TrucoRulesScenarioLog.Ok("GetScore FaltaEnvido",
                        "p1=" + player1Score + " p2=" + player2Score + " award=" + faltaPts);

                    int winnerActor = TrucoMatchRules.ResolveEnvidoWinnerActor(
                        player1Score, player2Score,
                        PhotonNetwork.LocalPlayer.ActorNumber,
                        PhotonNetwork.PlayerListOthers[0].ActorNumber,
                        GetManoActorForCurrentHand());

                    photonView.RPC(nameof(AnnounceEnvidoPoints), RpcTarget.All,
                        player1Score, player2Score, winnerActor, EnvidoFirstCallerActor);
                    QueueEnvidoCardReveal(winnerActor);
                    photonView.RPC(nameof(FaltaEnvidoWinner), RpcTarget.All, winnerActor, faltaPts);
                    break;
                }
            case ChallengeType.ConFlorQuiero:
                {
                    int player1FlorScore = CalculateFlor(_player1Cards);
                    int player2FlorScore = CalculateFlor(_player2Cards);
                    photonView.RPC(nameof(AnnounceFlorPoints), RpcTarget.All, player1FlorScore, player2FlorScore);
                    int florWinnerActor = player2FlorScore > player1FlorScore
                        ? PhotonNetwork.PlayerListOthers[0].ActorNumber
                        : player1FlorScore > player2FlorScore
                            ? PhotonNetwork.LocalPlayer.ActorNumber
                            : GetManoActorForCurrentHand();
                    QueueFlorCardReveal(florWinnerActor);
                    TrucoRulesScenarioLog.Ok("GetScore ConFlorQuiero",
                        "p1Flor=" + player1FlorScore + " p2Flor=" + player2FlorScore + " award=6 winner=" + florWinnerActor);
                    photonView.RPC(nameof(ConFlorQuieroWinner), RpcTarget.All, florWinnerActor, 6);
                    break;
                }
            case ChallengeType.ContraFlor:
                {
                    int player1FlorScore = CalculateFlor(_player1Cards);
                    int player2FlorScore = CalculateFlor(_player2Cards);
                    photonView.RPC(nameof(AnnounceFlorPoints), RpcTarget.All, player1FlorScore, player2FlorScore);
                    QueueFlorCardReveal();
                    if (player2FlorScore > player1FlorScore)
                    {
                        photonView.RPC(nameof(ContraFlorWinner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber);
                    }
                    else
                    {
                        photonView.RPC(nameof(ContraFlorWinner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
                    }
                    points = 1;
                    break;
                }
        }
    }

    [System.Serializable]
    class CardRevealPayload { public CardSuit suit; public int rank; }

    [System.Serializable]
    class CardRevealPayloadList { public CardRevealPayload[] items; }

    static string SerializeRevealCards(List<DeckCards> cards)
    {
        if (cards == null || cards.Count == 0) return "{}";
        var list = new CardRevealPayloadList
        {
            items = cards.Select(c => new CardRevealPayload { suit = c.suit, rank = c.rank }).ToArray()
        };
        return JsonUtility.ToJson(list);
    }

    static List<DeckCards> GetBestEnvidoRevealCards(List<DeckCards> hand)
    {
        if (hand == null || hand.Count != 3) return new List<DeckCards>();

        int bestPairScore = 0;
        DeckCards pairA = null, pairB = null;
        for (int i = 0; i < hand.Count; i++)
        {
            for (int j = i + 1; j < hand.Count; j++)
            {
                if (hand[i].suit != hand[j].suit) continue;
                int envido = 20 + GetEnvidoValue(hand[i].rank) + GetEnvidoValue(hand[j].rank);
                if (envido > bestPairScore) { bestPairScore = envido; pairA = hand[i]; pairB = hand[j]; }
            }
        }
        if (pairA != null && pairB != null) return new List<DeckCards> { pairA, pairB };

        int bestSingle = hand.Max(c => GetEnvidoValue(c.rank));
        if (bestSingle == 0)
        {
            var figuras = hand.FindAll(c => c.rank >= 10 && c.rank <= 12);
            if (figuras.Count == 3)
            {
                var suits = new HashSet<CardSuit>();
                foreach (var f in figuras) suits.Add(f.suit);
                if (suits.Count == 3) return figuras;
            }
        }

        var single = hand.OrderByDescending(c => GetEnvidoValue(c.rank)).First();
        return new List<DeckCards> { single };
    }

    void QueueEnvidoCardReveal(int winnerActor)
    {
        if (!PhotonNetwork.IsMasterClient || _player1Cards == null || _player2Cards == null) return;
        _pendingRevealMasterJson = SerializeRevealCards(GetBestEnvidoRevealCards(_player1Cards));
        _pendingRevealGuestJson = SerializeRevealCards(GetBestEnvidoRevealCards(_player2Cards));
        _pendingRevealWinnerActor = winnerActor;
        _pendingScoringCardReveal = true;
    }

    void QueueFlorCardReveal(int winnerActor = 0)
    {
        if (!PhotonNetwork.IsMasterClient || _player1Cards == null || _player2Cards == null) return;
        _pendingRevealMasterJson = SerializeRevealCards(_player1Cards);
        _pendingRevealGuestJson = SerializeRevealCards(_player2Cards);
        _pendingRevealWinnerActor = winnerActor;
        _pendingScoringCardReveal = true;
    }

    void FlushPendingScoringCardReveal(bool immediate = false)
    {
        if (!_pendingScoringCardReveal) return;
        if (immediate)
        {
            _scoringRevealFlushScheduled = false;
            _pendingScoringCardReveal = false;
            UIMANAGER.Instance?.BeginScoringCardReveal();
            photonView.RPC(nameof(RevealScoringCardsRpc), RpcTarget.All,
                _pendingRevealMasterJson, _pendingRevealGuestJson, _pendingRevealWinnerActor);
            return;
        }
        if (_scoringRevealFlushScheduled) return;
        _scoringRevealFlushScheduled = true;
        StartCoroutine(CoFlushScoringRevealAfterTrickSettles());
    }

    IEnumerator CoFlushScoringRevealAfterTrickSettles()
    {
        // Keep the final Truco play readable before Envido scoring cards appear.
        yield return new WaitForSeconds(1.75f);
        _scoringRevealFlushScheduled = false;
        if (!_pendingScoringCardReveal) yield break;
        _pendingScoringCardReveal = false;
        UIMANAGER.Instance?.BeginScoringCardReveal();
        photonView.RPC(nameof(RevealScoringCardsRpc), RpcTarget.All,
            _pendingRevealMasterJson, _pendingRevealGuestJson, _pendingRevealWinnerActor);
    }

    [PunRPC]
    void RevealScoringCardsRpc(string masterJson, string guestJson, int winnerActor = 0)
    {
        if (UIMANAGER.Instance == null) return;
        var m = JsonUtility.FromJson<CardRevealPayloadList>(masterJson);
        var g = JsonUtility.FromJson<CardRevealPayloadList>(guestJson);
        var mine = PhotonNetwork.IsMasterClient ? m : g;
        var theirs = PhotonNetwork.IsMasterClient ? g : m;
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        // winnerActor == 0 â†’ Flor: both hands. Else only the winner's envido cards.
        bool showMine = winnerActor == 0 || winnerActor == localActor;
        bool showTheirs = winnerActor == 0 || (winnerActor != 0 && winnerActor != localActor);
        if (showMine && mine?.items != null)
            foreach (var c in mine.items)
                UIMANAGER.Instance.ShowRevealedScoringCard(c.suit, c.rank, true);
        if (showTheirs && theirs?.items != null)
            foreach (var c in theirs.items)
                UIMANAGER.Instance.ShowRevealedScoringCard(c.suit, c.rank, false);
        // Re-stamp faces after LeanMove so Mazo→Flor→NuevaMano never flashes blank white Images.
        UIMANAGER.Instance.EnsureLocalHandSpritesVisible();
        UIMANAGER.Instance.EnsureOpponentRevealSpritesVisible();
    }

    [PunRPC]
    private void AnnounceEnvidoPoints(int p1, int p2, int winnerActor = 0, int firstCallerActor = 0)
    {
        if (UIMANAGER.Instance == null) return;
        if (_isSpectator)
        {
            UIMANAGER.Instance.ShowDeclarationCallout($"Envido: {p1} - {p2}", false);
            return;
        }

        bool localIsMaster = PhotonNetwork.IsMasterClient;
        // Master = player1 seat for scores p1/p2 in GetScore.
        int localScore = localIsMaster ? p1 : p2;
        int rivalScore = localIsMaster ? p2 : p1;
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int rivalActor = PhotonNetwork.PlayerListOthers.Length > 0
            ? PhotonNetwork.PlayerListOthers[0].ActorNumber
            : 0;
        if (firstCallerActor <= 0)
        {
            int manoNum = TrucoHandWinner.GetManoPlayerNumber(DataHandler.Instance.roundNumber);
            firstCallerActor = (manoNum == 1 && localIsMaster) || (manoNum == 2 && !localIsMaster)
                ? localActor
                : rivalActor;
        }
        bool localIsFirstCaller = firstCallerActor == localActor;
        bool localWon = winnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        bool rivalWon = winnerActor != 0 && winnerActor != PhotonNetwork.LocalPlayer.ActorNumber;

        if (localIsFirstCaller)
        {
            // First Envido caller always announces their points (audio + text).
            TrucoGameplayAudio.PlayDeclarationPhrase($"Tengo {localScore}", true);
            if (rivalWon)
                StartCoroutine(CoDelayedDeclaration($"Tengo {rivalScore}", false, 1.15f));
            else if (!localWon && rivalActor > 0)
                // Tie goes to Mano (first caller) â€” rival lost; they say Son buenas after hearing us.
                StartCoroutine(CoDelayedDeclaration("Son buenas", false, 1.15f));
        }
        else
        {
            // Second player announces only if they win; otherwise "Son buenas".
            if (localWon)
            {
                TrucoGameplayAudio.PlayDeclarationPhrase($"Tengo {rivalScore}", false);
                StartCoroutine(CoDelayedDeclaration($"Tengo {localScore}", true, 1.15f));
            }
            else
            {
                TrucoGameplayAudio.PlayDeclarationPhrase($"Tengo {rivalScore}", false);
                StartCoroutine(CoDelayedDeclaration("Son buenas", true, 1.15f));
            }
        }
    }

    IEnumerator CoDelayedDeclaration(string phrase, bool mine, float delay)
    {
        yield return new WaitForSeconds(delay);
        TrucoGameplayAudio.PlayDeclarationPhrase(phrase, mine);
    }

    /// <summary>Shows each player their own declared Flor points; spectators see both.</summary>
    [PunRPC]
    private void AnnounceFlorPoints(int p1, int p2)
    {
        if (UIMANAGER.Instance == null) return;
        if (_isSpectator)
        {
            UIMANAGER.Instance.ShowDeclarationCallout($"Flor: {p1} - {p2}", false);
            return;
        }
        int mine = PhotonNetwork.IsMasterClient ? p1 : p2;
        int theirs = PhotonNetwork.IsMasterClient ? p2 : p1;
        TrucoGameplayAudio.PlayDeclarationPhrase($"Flor: {mine}", true);
        TrucoGameplayAudio.PlayDeclarationPhrase($"Flor: {theirs}", false);
    }

    [PunRPC]
    private void ConFlorQuieroWinner(int ID, int _points)
    {
        if (_isSpectator) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne, "ConFlorQuieroWinner queued scoring cards id=" + ID);
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            myPlayerScoreHandler.UpdateScore(_points, true);
            Debug.Log("You Win Con Flor Quiero!");
            UIMANAGER.Instance.DisableButtons();
        }
        else
        {
            otherPlayerScoreHandler.UpdateScore(_points, true);
            Debug.Log("You lose Con Flor Quiero!");
            UIMANAGER.Instance.DisableButtons();
        }
        SyncScoresAfterPointChange();
        FlushPendingScoringCardReveal(immediate: true);
        // Winner RPC (hand/mazo) evaluates match end after both Flor + Truco points are applied.
    }

    public void ShowAllCards()
    {
        Debug.LogWarning("Showing Player 1 Cards");
        if (PhotonNetwork.IsMasterClient)
        {
            foreach (var card in UIMANAGER.Instance.myPlayerCards)
            {
                Card _c = card.GetComponent<Card>();
                UIMANAGER.Instance.ShowMyCard(card.transform, _c.suit, _c.value);
            }
            Debug.LogWarning("Showing Player 2 Cards");
            foreach (var card in _player2Cards)
            {
                UIMANAGER.Instance.ShowOtherPlayersCard(card.suit, card.rank);
            }
        }
        else
        {
            foreach (var card in UIMANAGER.Instance.myPlayerCards)
            {
                Card _c = card.GetComponent<Card>();
                UIMANAGER.Instance.ShowMyCard(card.transform, _c.suit, _c.value);
            }
            Debug.LogWarning("Showing Player 2 Cards");
            foreach (var card in _player1Cards)
            {
                UIMANAGER.Instance.ShowOtherPlayersCard(card.suit, card.rank);
            }
        }
    }

    [PunRPC]
    public void EnvidoWinner(int ID, int _points)
    {
        if (_isSpectator) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "EnvidoWinner points queued reveal id=" + ID + " pts=" + _points);
<<<<<<< Updated upstream
=======
        envidoResolvedThisHand = true;
        _envidoRevealOwed = true;
>>>>>>> Stashed changes
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            Debug.Log("You win!");
            myPlayerScoreHandler.UpdateScore(_points, true);
            challengePoints = 0;
        }
        else
        {
            otherPlayerScoreHandler.UpdateScore(_points, true);
            challengePoints = 0;
        }
        SyncScoresAfterPointChange();
        if (WouldReachMatchTarget())
        {
            BeginPendingMatchEnd("envido target");
            FlushPendingScoringCardReveal(immediate: true);
            TryRevealFlorForPendingMatchEnd();
            StartCoroutine(CoEvaluateMatchAfterScoringReveal());
            return;
        }
    }

    [PunRPC]
    private void ContraFlorWinner(int ID)
    {
        if (_isSpectator) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne, "ContraFlorWinner queued scoring cards id=" + ID);
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            int score = myPlayerScoreHandler.GetCurrentScore();
            int scoreToAdd = OneVsOneMatchSession.TargetScore - score;
            myPlayerScoreHandler.UpdateScore(scoreToAdd, true);
            Debug.Log("You Win!");
            UIMANAGER.Instance.UpdateTurnText("You Win!", 3.5f);
            UIMANAGER.Instance.DisableButtons();
        }
        else
        {
            int score = otherPlayerScoreHandler.GetCurrentScore();
            int scoreToAdd = OneVsOneMatchSession.TargetScore - score;
            otherPlayerScoreHandler.UpdateScore(scoreToAdd, true);
            Debug.Log("You lose!");
            UIMANAGER.Instance.UpdateTurnText("You lose!", 3.5f);
            UIMANAGER.Instance.DisableButtons();
        }
        SyncScoresAfterPointChange();
        FlushPendingScoringCardReveal(immediate: true);
<<<<<<< Updated upstream
        // Do not Evaluate/Continue here — Winner (truco/mazo) or the Falta path owns match-end.
=======
        // An accepted Contra Flor is decisive: the higher Flor takes the whole match right away.
        BeginPendingMatchEnd("contra flor");
        StartCoroutine(CoEvaluateMatchAfterScoringReveal());
>>>>>>> Stashed changes
    }

    [PunRPC]
    private void FaltaEnvidoWinner(int ID, int _points)
    {
        if (_isSpectator) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "FaltaEnvidoWinner points + reveal cards id=" + ID);
<<<<<<< Updated upstream
=======
        envidoResolvedThisHand = true;
        _envidoRevealOwed = true;
>>>>>>> Stashed changes
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            myPlayerScoreHandler.UpdateScore(_points, true);
            Debug.Log("You Win falta Envido!");
            UIMANAGER.Instance.DisableButtons();
        }
        else
        {
            otherPlayerScoreHandler.UpdateScore(_points, true);
            Debug.Log("You lose Falta Envido!");
            UIMANAGER.Instance.DisableButtons();
        }
        ContinueAfterSideBetResolution();
    }

    bool WouldReachMatchTarget()
    {
        int target = OneVsOneMatchSession.TargetScore;
        return TrucoMatchRules.HasReachedTarget(myPlayerScoreHandler.GetCurrentScore(), target)
               || TrucoMatchRules.HasReachedTarget(otherPlayerScoreHandler.GetCurrentScore(), target);
    }

    void ContinueAfterSideBetResolution()
    {
        SyncScoresAfterPointChange();
        if (WouldReachMatchTarget())
        {
            BeginPendingMatchEnd("side-bet target");
            FlushPendingScoringCardReveal(immediate: true);
            TryRevealFlorForPendingMatchEnd();
            StartCoroutine(CoEvaluateMatchAfterScoringReveal());
            return;
        }
        if (EvaluateMatchOutcomeAfterPoints()) return;
        if (_gameEnded || _matchEndPending || UIMANAGER.Instance == null) return;
        if (!UIMANAGER.Instance.trucoPlayed)
        {
            if (UIMANAGER.Instance.unAnsweredChallenges.Count > 0)
                StartCoroutine(UIMANAGER.Instance.CheckForUnansweredChallenges());
            else if (IsMyTurn())
                SetCanPlayCard(true);
        }
    }

    IEnumerator CoEvaluateMatchAfterScoringReveal()
    {
        // Show Falta/Envido/Flor winning cards on the table before the result panel.
        yield return new WaitForSeconds(2.8f);
        if (!_gameEnded)
            ForceEvaluateMatchOutcome();
    }

    void BeginPendingMatchEnd(string reason)
    {
        if (_matchEndPending || _gameEnded) return;
        _matchEndPending = true;
        MarkHandResolved();
        UIMANAGER.Instance?.DisableButtons();
        TrucoRulesScenarioLog.Ok("MatchEndPending", "reason=" + reason);
    }

    void TryRevealFlorForPendingMatchEnd()
    {
        if (!PhotonNetwork.IsMasterClient || _florCardsRevealedThisHand) return;
        var ui = UIMANAGER.Instance;
        if (ui == null || (!ui._florPlayed && !florResolvedThisHand)) return;
        int owner = florOwnerActorThisHand > 0 ? florOwnerActorThisHand : SoleFlorHolderActor();
        if (owner <= 0) return;
        RevealFlorCardsOnce(owner);
    }

    /// <summary>Declare winner immediately (cards already held long enough).</summary>
    void ForceEvaluateMatchOutcome()
    {
        int me = myPlayerScoreHandler.GetCurrentScore();
        int opp = otherPlayerScoreHandler.GetCurrentScore();
        int target = OneVsOneMatchSession.TargetScore;
        if (TrucoMatchRules.HasReachedTarget(me, target))
        {
            TrucoRulesScenarioLog.Ok("MatchEnd → GameWon", "target=" + target + " me=" + me + " opp=" + opp);
            GameWon();
            return;
        }
        if (TrucoMatchRules.HasReachedTarget(opp, target))
        {
            TrucoRulesScenarioLog.Ok("MatchEnd → GameLost", "target=" + target + " me=" + me + " opp=" + opp);
            GameLost();
        }
    }

    public bool PlayerHasFlor()
    {
        if (deniedFlor || LocalPlayerHasPlayedCard())
            return false;
        List<DeckCards> localHand = PhotonNetwork.IsMasterClient ? _player1Cards : _player2Cards;
        Debug.LogWarning("Checking local player's flor. Cards Count: " + (localHand != null ? localHand.Count : 0));
        return HasFlor(localHand);
    }

    public bool HasFlor(List<DeckCards> hand)
    {
        if (hand == null || hand.Count != 3)
            return false;
        return hand.All(card => card.suit == hand[0].suit);
    }

    bool LocalPlayerHasPlayedCard()
    {
        return PhotonNetwork.IsMasterClient ? player1Score.Count > 0 : player2Score.Count > 0;
    }

    /// <summary>
    /// Flor = 20 + the three card values (10/11/12 count 0): 7-5-2 → 34, 6-3-4 → 33, 12-10-4 → 24.
    /// House rule: three face cards score 30 (not 20).
    /// </summary>
    public int CalculateFlor(List<DeckCards> hand)
    {
        if (!HasFlor(hand))
            return 0;
        return TrucoFlorScore.Compute(hand.Select(card => card.rank));
    }

    public int CalculateEnvido(List<DeckCards> hand)
    {
        if (hand == null || hand.Count != 3)
        {
            UnityEngine.Debug.LogError("Envido requires exactly 3 cards.");
            return 0;
        }

        int maxEnvido = 0;

        for (int i = 0; i < hand.Count; i++)
        {
            for (int j = i + 1; j < hand.Count; j++)
            {
                var card1 = hand[i];
                var card2 = hand[j];

                if (card1.suit == card2.suit)
                {
                    int val1 = GetEnvidoValue(card1.rank);
                    int val2 = GetEnvidoValue(card2.rank);
                    int envido = 20 + val1 + val2;

                    if (envido > maxEnvido)
                        maxEnvido = envido;
                }
            }
        }

        if (maxEnvido == 0)
        {
            maxEnvido = hand.Max(card => GetEnvidoValue(card.rank));
        }

        return maxEnvido;
    }

    private static int GetEnvidoValue(int rank)
    {
        return (rank >= 10) ? 0 : rank;
    }

    public void SetCanPlayCard(bool _value)
    {
        if (_value && (_gameEnded || _matchEndPending || HandResolved))
        {
            _canPlayCard = false;
            UIMANAGER.Instance?.DisableButtons();
            return;
        }
        _canPlayCard = _value;
        if (_value && IsMyTurn() && !_gameEnded && !_matchEndPending && !HandResolved)
            UIMANAGER.Instance.EnableButtons();
    }

    public bool CanPlayCard()
    {
        return _canPlayCard && !_gameEnded && !_matchEndPending && !HandResolved;
    }

    public bool IsMyTurn() => _myTurn;

    public void SetMyTurn(bool _value)
    {
        _myTurn = _value;
    }

    public void PlayFirstHandCardOnTimeout()
    {
        if (!IsMyTurn() || _gameEnded || HandResolved || _handOutcomeCommitted || _restartScheduled) return;
        TrucoRulesScenarioLog.Ok("PlayTimeout â†’ TriggerMazoOnTimeout (no card auto-play)");
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.TiempoEsgotadoJugada);
        UIMANAGER.Instance?.UpdateTurnText(TrucoTextosClient.TiempoEsgotadoJugada, 2f);
        UIMANAGER.Instance?.TriggerMazoOnTimeout();
    }

    [PunRPC]
    void SubmitMazoHandWin(int winnerActor, int handPoints)
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        ResolveMazoHand(winnerActor);
    }

    public void CardSelected(Transform cardTransform, CardSuit suit, int value)
    {
        SetCanPlayCard(false);
        UIMANAGER.Instance?.DisableButtons();
        UIMANAGER.Instance?.UpdateTurnText(TrucoTextosClient.FormatoBannerEsperandoRival(TrucoTextosClient.EsperandoJugadaRival), -1f);
        TrucoRulesScenarioLog.Ok("CardPlay LOCAL", "suit=" + suit + " rank=" + value);
        if (PhotonNetwork.IsMasterClient)
        {
            player1Score.Add(new ScoreClass { suit = suit, value = value });
        }
        else
        {
            // Non-host must record own trick locally; RPC only updates the other machine.
            player2Score.Add(new ScoreClass { suit = suit, value = value });
        }
        cardPlayed = true;
        deniedFlor = true;
        NoteTrickLeadAfterLocalPlay();
        UIMANAGER.Instance.ShowMyCard(cardTransform, suit, value);
        TrucoGameplayAudio.PlayCardPlaced();
        photonView.RPC(nameof(SelectedCardRPC), RpcTarget.Others, suit, value);
    }

    [PunRPC]
    private void SelectedCardRPC(CardSuit suit, int value)
    {
        if (_isSpectator)
        {
            UIMANAGER.Instance?.ShowOtherPlayersCard(suit, value);
            return;
        }
        otherPlayerDeniedFlor = true;
        if (PhotonNetwork.IsMasterClient)
        {
            // Host receives: the client played, so that card belongs to player2.
            player2Score.Add(new ScoreClass { suit = suit, value = value });
        }
        else
        {
            // Client receives: the host played, so that card belongs to player1.
            player1Score.Add(new ScoreClass { suit = suit, value = value });
        }
        UIMANAGER.Instance.ShowOtherPlayersCard(suit, value);
        TrucoGameplayAudio.PlayCardPlaced();
        NoteTrickLeadAfterRemotePlay();
        TrucoRulesScenarioLog.Opp("CardPlay RIVAL", "suit=" + suit + " rank=" + value);
        if (!_gameEnded)
        {
            TurnManager.Instance.EndTurn();
        }
    }

    private void CheckForTurn()
    {
        if (!player1Score.Count.Equals(player2Score.Count))
            return;
        int calculatedRank1 = CalculateTrucoRank(player1Score[^1].value, player1Score[^1].suit);
        int calculatedRank2 = CalculateTrucoRank(player2Score[^1].value, player2Score[^1].suit);
        // Runs on the master client: player1 == master/local, player2 == the other player.
        // The winner of the trick must LEAD the next trick, so give the turn to that exact actor
        // (mapping by actor number, not by an ambiguous turn-order index).
        if (calculatedRank1 > calculatedRank2)
        {
            TurnManager.Instance.GiveTurnAgainToActor(PhotonNetwork.LocalPlayer.ActorNumber);
            TrucoRulesScenarioLog.Ok("TrickWinner P1 leads next",
                "rank1=" + calculatedRank1 + " rank2=" + calculatedRank2);
        }
        else if (calculatedRank2 > calculatedRank1)
        {
            int otherActor = PhotonNetwork.PlayerListOthers.Length > 0
                ? PhotonNetwork.PlayerListOthers[0].ActorNumber
                : PhotonNetwork.LocalPlayer.ActorNumber;
            TurnManager.Instance.GiveTurnAgainToActor(otherActor);
            TrucoRulesScenarioLog.Ok("TrickWinner P2 leads next",
                "rank1=" + calculatedRank1 + " rank2=" + calculatedRank2 + " actor=" + otherActor);
        }
        else
        {
            int leadActor = _trickLeadActor > 0 ? _trickLeadActor : GetManoActorForCurrentHand();
            TurnManager.Instance.GiveTurnAgainToActor(leadActor);
            TrucoRulesScenarioLog.Ok("Trick parda — leader plays first again",
                "leadActor=" + leadActor + " trick=" + player1Score.Count);
        }
    }

    void NoteTrickLeadAfterLocalPlay()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (player1Score.Count == player2Score.Count + 1)
                _trickLeadActor = PhotonNetwork.LocalPlayer.ActorNumber;
        }
        else if (player2Score.Count == player1Score.Count + 1)
        {
            _trickLeadActor = PhotonNetwork.LocalPlayer.ActorNumber;
        }
    }

    void NoteTrickLeadAfterRemotePlay()
    {
        int otherActor = PhotonNetwork.PlayerListOthers.Length > 0
            ? PhotonNetwork.PlayerListOthers[0].ActorNumber
            : 0;
        if (PhotonNetwork.IsMasterClient)
        {
            if (player2Score.Count == player1Score.Count + 1)
                _trickLeadActor = otherActor;
        }
        else if (player1Score.Count == player2Score.Count + 1)
        {
            _trickLeadActor = otherActor;
        }
    }

    public bool IsCurrentTrickComplete() =>
        player1Score != null && player2Score != null
        && player1Score.Count > 0
        && player1Score.Count == player2Score.Count;

    private void CheckForWinner()
    {
        if (!player1Score.Count.Equals(player2Score.Count))
            return;

        if (trickResults.Count < player1Score.Count)
        {
            int calculatedRank1 = CalculateTrucoRank(player1Score[^1].value, player1Score[^1].suit);
            int calculatedRank2 = CalculateTrucoRank(player2Score[^1].value, player2Score[^1].suit);

            if (calculatedRank1 > calculatedRank2)
            {
                trickResults.Add(1);
            }
            else if (calculatedRank2 > calculatedRank1)
            {
                trickResults.Add(2);
            }
            else
            {
                trickResults.Add(0);
            }
        }
    }

    [PunRPC]
    private void Winner(int ID, int points)
    {
        if (_isSpectator || _gameEnded) return;
        // Duplicate Winner (timeout ensure after first award) must not add points twice.
        if (_handOutcomeCommitted || _restartScheduled)
        {
            TrucoRulesScenarioLog.Ok("HandWinner RPC ignored (duplicate)",
                "winnerActor=" + ID + " handPts=+" + points
                + " committed=" + _handOutcomeCommitted + " restart=" + _restartScheduled);
            return;
        }
        _handOutcomeCommitted = true;
        HandResolved = true;
        bool mine = ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber);
        TurnManager.Instance?.StopAllTurnTimers();
        SetMyTurn(false);
        SetCanPlayCard(false);
        FlushPendingScoringCardReveal(immediate: true);
        // Keep Flor/Envido challenge flags until after match-end evaluate — clearing _florPlayed
        // here made pending Flor reveal skip when Vale4 points hit the target.
        UIMANAGER.Instance?.DisableButtons();
        if (mine)
        {
            myPlayerScoreHandler.UpdateScore(points, true);
            UIMANAGER.Instance.DisableButtons();
        }
        else
        {
            UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.PerdisteMano, 3.5f);
            otherPlayerScoreHandler.UpdateScore(points, true);
            UIMANAGER.Instance.DisableButtons();
        }

        // Winner already ran on All clients with the same actor id â€” only persist locally.
        // Re-broadcasting via master/guest sync was flipping scores when MasterClient rotated.
        PersistMatchScoresToDataHandler();
        TrucoRulesScenarioLog.Ok("HandWinner RPC",
            "winnerActor=" + ID + " handPts=+" + points + " toMe=" + mine
            + " meNow=" + myPlayerScoreHandler.GetCurrentScore()
            + " oppNow=" + otherPlayerScoreHandler.GetCurrentScore()
            + " localActor=" + PhotonNetwork.LocalPlayer.ActorNumber);

        if (!EvaluateMatchOutcomeAfterPoints())
        {
            UIMANAGER.Instance?.ResetHandChallengeState();
            ScheduleResetGameAfterReveals();
        }
    }

    bool EvaluateMatchOutcomeAfterPoints()
    {
        if (!WouldReachMatchTarget()) return false;

        // Reach 15/30 mid-hand: lock actions immediately, show any pending Flor cards, then declare.
        BeginPendingMatchEnd("score target");
        bool florOwed = !_florCardsRevealedThisHand
                        && UIMANAGER.Instance != null
                        && (UIMANAGER.Instance._florPlayed || florResolvedThisHand);
        // Falta/Envido won earlier in the hand: the rival must see the winning cards before the panel.
        bool envidoOwed = _envidoRevealOwed;
        if (florOwed || envidoOwed)
        {
            if (!_matchEndRevealRoutineRunning)
            {
                _matchEndRevealRoutineRunning = true;
                TryRevealFlorForPendingMatchEnd();
                FlushPendingScoringCardReveal(immediate: true);
                StartCoroutine(CoEvaluateMatchAfterScoringReveal());
            }
            return true;
        }

        ForceEvaluateMatchOutcome();
        return true;
    }

    public void EndRound()
    {
        // Immediate: the old 1.75 s deferred flush landed inside the NuevaMano fade after a NoQuiero.
        FlushPendingScoringCardReveal(immediate: true);
        Debug.LogWarning("Setting My Player Score: " + myPlayerScoreHandler.GetCurrentScore());
        SyncScoresAfterPointChange();
        PhotonNetwork.AutomaticallySyncScene = true;
        UIMANAGER.Instance.DisableButtons();
        if (!EvaluateMatchOutcomeAfterPoints())
            ScheduleResetGameAfterReveals();
    }

  void TryReport1v1MatchToBackend(string winnerUserId, System.Action onSettled = null, bool submitResult = true)
    {
        StartCoroutine(CoSettle1v1MatchBackend(winnerUserId, onSettled, submitResult));
    }

    static string ResolveWinnerIdForSettlement(string winnerUserId, bool submitResult)
    {
        if (!string.IsNullOrEmpty(winnerUserId)) return winnerUserId;
        if (!submitResult) return null;
        return PhotonPlayerHelper.GetLocalTrucoPlayerUserId();
    }

    IEnumerator CoSettle1v1MatchBackend(string winnerUserId, System.Action onSettled, bool submitResult)
    {
        if (_isInTournament || _isSpectator) yield break;
        if (string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId)) yield break;
        if (_1v1ResultPosted && submitResult)
        {
            var refreshTask = ApiController.GetCurrentUserProfile();
            while (!refreshTask.IsCompleted) yield return null;
            onSettled?.Invoke();
            TrucoWalletHudRefresh.Apply();
            yield break;
        }
        if (_1v1SettlementBusy) yield break;

        string resolvedWinner = ResolveWinnerIdForSettlement(winnerUserId, submitResult);
        if (submitResult && string.IsNullOrEmpty(resolvedWinner))
        {
            TrucoRulesScenarioLog.BackendFail("POST /result skipped",
                "winnerUserId empty after resolve â€” check Photon userId props + CachedOpponentUserId");
            yield break;
        }

        _1v1SettlementBusy = true;
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        int balBefore = ApiController.GetSessionUser?.Data?.wallet?.balance ?? -1;
        TrucoRulesScenarioLog.Backend(submitResult ? "POST /result start" : "SETTLE loser backup /result",
            "match=" + matchId
            + " winnerUserId=" + (resolvedWinner ?? "null")
            + " submitResult=" + submitResult
            + " localActor=" + PhotonNetwork.LocalPlayer.ActorNumber
            + " isMaster=" + PhotonNetwork.IsMasterClient
            + " bal=" + balBefore);

        var settleTask = ApiController.Finalize1v1MatchSettlement(
            matchId, resolvedWinner, submitResult, onSettled, ResolveLoserIdForSettlement(resolvedWinner, submitResult));
        while (!settleTask.IsCompleted) yield return null;

        bool ok = false;
        try { ok = settleTask.Result; }
        catch (System.Exception ex)
        {
            TrucoRulesScenarioLog.BackendFail("SETTLE task", ex.Message);
        }

        int balAfter = ApiController.GetSessionUser?.Data?.wallet?.balance ?? -1;
        bool prizeVisible = balBefore >= 0 && balAfter > balBefore;
        if (submitResult && (ok || prizeVisible))
            _1v1ResultPosted = true;

        _1v1SettlementBusy = false;

        // Prize toast only for the WINNER — losers never receive the prize and must not see support copy.
        bool iAmWinner = !string.IsNullOrEmpty(resolvedWinner)
                         && resolvedWinner == PhotonPlayerHelper.GetLocalTrucoPlayerUserId();
        if (submitResult && iAmWinner && !ok && !prizeVisible)
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ErrorPremioNoConfirmado);
        else if (submitResult && iAmWinner && (ok || prizeVisible))
            AppManager.Instance?.DisplayNotification(FormatWinPrizeAwardedMessage());

        TrucoWalletHudRefresh.Apply();
    }

    static string FormatWinPrizeAwardedMessage()
    {
        string rival = null;
        if (PhotonNetwork.PlayerListOthers != null && PhotonNetwork.PlayerListOthers.Length > 0)
            rival = PhotonNetwork.PlayerListOthers[0]?.NickName;
        if (string.IsNullOrEmpty(rival))
            rival = TrucoLocalization.IsEnglish ? "your rival" : "tu rival";
        return string.Format(TrucoTextosClient.PremioYaAcreditadoConRival, rival);
    }

    static string ResolveLoserIdForSettlement(string winnerUserId, bool submitResult)
    {
        if (!submitResult || string.IsNullOrEmpty(winnerUserId)) return null;
        string local = PhotonPlayerHelper.GetLocalTrucoPlayerUserId();
        if (!string.IsNullOrEmpty(local) && local == winnerUserId)
            return ResolveOpponentUserIdForSettlement();
        if (!string.IsNullOrEmpty(local) && local != winnerUserId)
            return local;
        return ResolveOpponentUserIdForSettlement();
    }

    static string ResolveOpponentUserIdForSettlement()
    {
        if (!string.IsNullOrEmpty(OneVsOneMatchSession.CachedOpponentUserId))
            return OneVsOneMatchSession.CachedOpponentUserId;
        return PhotonPlayerHelper.GetOtherTrucoPlayerUserId();
    }

    /// <summary>The opponent failed to reconnect within the window: the player who stayed wins (walkover).</summary>
    public async void WinByOpponentWalkover()
    {
        if (_gameEnded) return;
        TrucoRulesScenarioLog.Ok("Walkover WIN (rival reconnect failed)",
            "match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?"));
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "WinByOpponentWalkover claimer=" + (PhotonPlayerHelper.GetLocalTrucoPlayerUserId() ?? "?")
            + " match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?")
            + " inRoom=" + PhotonNetwork.InRoom);
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.GanaPorAbandono);
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        string claimerId = PhotonPlayerHelper.GetLocalTrucoPlayerUserId();
        if (!string.IsNullOrEmpty(matchId) && !string.IsNullOrEmpty(claimerId) && !_1v1ResultPosted)
        {
            bool ok = await ApiController.ClaimWalkover1v1(matchId, claimerId, err =>
            {
                TrucoRulesScenarioLog.BackendFail("ClaimWalkover", err);
                Debug.LogWarning("[GameManager] walkover API: " + err);
            });
            if (ok)
                _1v1ResultPosted = true;
            else
                TryReport1v1MatchToBackend(claimerId);
        }
        GameWon();
    }

    private void GameLost()
    {
        if (_gameEnded) return;
        StartCoroutine(CoShowMatchEndPanel(false));
    }

    private void GameWon()
    {
        if (_gameEnded) return;
        StartCoroutine(CoShowMatchEndPanel(true));
    }

    IEnumerator CoShowMatchEndPanel(bool won)
    {
        CancelPendingHandRestart();
        int entryFee = OneVsOneMatchSession.EntryFee;
        int me = myPlayerScoreHandler.GetCurrentScore();
        int opp = otherPlayerScoreHandler.GetCurrentScore();
        TrucoRulesScenarioLog.Ok(won ? "GameWon UI + backend settle" : "GameLost UI + backend settle",
            "me=" + me + " opp=" + opp + (won ? " fee=" + entryFee : ""));

        TrucoMatchProgress.ClearAllMatchMemory();
        UIMANAGER.Instance.DisableButtons();
        _gameEnded = true;
        FinalizePhotonRoomAfterMatchEnd();

        if (!_isInTournament && !_isSpectator)
        {
            if (won)
            {
                yield return CoSettle1v1MatchBackend(
                    PhotonPlayerHelper.GetLocalTrucoPlayerUserId(),
                    () => TrucoMatchEndUiPolish.RefreshBalance(winPanel),
                    submitResult: true);
            }
            else
            {
                // Loser: refresh profile only — winner owns POST /result. Never toast prize errors here.
                yield return CoSettle1v1MatchBackend(
                    ResolveOpponentUserIdForSettlement(),
                    () => TrucoMatchEndUiPolish.RefreshBalance(losePanel),
                    submitResult: false);
            }
        }

        if (!won)
            photonView.Controller.SetScore(myPlayerScoreHandler.GetCurrentScore());

        if (_isInTournament && !_tournamentMatchFinalized)
        {
            if (won) OnTournamentMatchWon();
            else OnTournamentMatchLose();
            yield break;
        }

        if (won)
        {
            winPanel.SetActive(true);
            TrucoMatchEndUiPolish.Apply(winPanel, true, entryFee);
            TrucoMatchEndUiPolish.RefreshBalance(winPanel);
        }
        else
        {
            losePanel.SetActive(true);
            TrucoMatchEndUiPolish.Apply(losePanel, false, entryFee);
            TrucoMatchEndUiPolish.RefreshBalance(losePanel);
        }

        ToggleMenuBtns(true);
    }

    void CancelPendingHandRestart()
    {
        if (_restartRoutine != null)
        {
            StopCoroutine(_restartRoutine);
            _restartRoutine = null;
            TrucoRulesScenarioLog.Ok("CancelPendingHandRestart",
                "reason=match ended â€” do not reload Gameplay");
        }
        _restartScheduled = true;
        HandResolved = true;
    }

    static void FinalizePhotonRoomAfterMatchEnd()
    {
        if (!PhotonNetwork.InRoom) return;
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.CurrentRoom.EmptyRoomTtl = 0;
        }
    }

    private void ResetGame()
    {
        if (_gameEnded || _restartScheduled) return;
        _restartScheduled = true;
        HandResolved = true;
        UIMANAGER.Instance.DisableButtons();
        TurnManager.Instance?.StopAllTurnTimers();
        SetMyTurn(false);
        SetCanPlayCard(false);
        PersistMatchScoresToDataHandler();
        // Do NOT ResetHandState() here â€” clearing HandResolved let the 2.5s timeout
        // ensure fire a second Mazo/Winner (+2 twice) before the scene reload.
        TrucoRulesScenarioLog.Ok("ResetGame â†’ NuevaMano reload scheduled",
            "keep HandResolved=true until next scene");
        _restartRoutine = StartCoroutine(RestartGameAfterDelay());
    }

    private IEnumerator RestartGameAfterDelay()
    {
        UIMANAGER.Instance?.UpdateTurnText(TrucoTextosClient.NuevaMano, 1.2f);
        UIMANAGER.Instance?.FreezeTableCardsForHandTransition();
        // Brief hold so Flor/Mazo faces stay readable, then cover the screen before reload
        // (avoids the mid-transition flash / false deal the tester reported).
        yield return new WaitForSeconds(1.15f);
        _restartRoutine = null;
        if (_gameEnded)
        {
            TrucoRulesScenarioLog.Ok("NuevaMano reload aborted (match ended)");
            yield break;
        }
        PhotonNetwork.AutomaticallySyncScene = true;
        bool covered = false;
        TrucoSceneTransition.FadeOutThen(() => covered = true, 0.4f);
        float wait = 0f;
        while (!covered && wait < 1.2f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        TrucoSceneTransition.HoldCoverUntilReleased();
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LoadLevel("Gameplay");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
        yield return null;
    }

    private int CalculateTrucoRank(int rank, CardSuit suit)
    {
        if (rank == 1 && suit == CardSuit.Swords) return 14;
        if (rank == 1 && suit == CardSuit.Clubs) return 13;
        if (rank == 7 && suit == CardSuit.Swords) return 12;
        if (rank == 7 && suit == CardSuit.Coins) return 11;
        if (rank == 3) return 10;
        if (rank == 2) return 9;
        if (rank == 1) return 8;
        if (rank == 12) return 7;
        if (rank == 11) return 6;
        if (rank == 10) return 5;
        if (rank == 7) return 4;
        if (rank == 6) return 3;
        if (rank == 5) return 2;
        if (rank == 4) return 1;

        return 0;
    }

    public void AwardPointsToOtherPlayer(int points)
    {
        int winner = PhotonNetwork.PlayerListOthers.Length > 0
            ? PhotonNetwork.PlayerListOthers[0].ActorNumber
            : PhotonNetwork.LocalPlayer.ActorNumber;
        RequestNetworkAward(winner, points, false);
    }

    public void AwardPointsToThisPlayer(int points)
    {
        RequestNetworkAward(PhotonNetwork.LocalPlayer.ActorNumber, points, false);
    }

    [PunRPC]
    private void AwardOtherPlayerPoints(int points)
    {
        if (_isSpectator) return;
        otherPlayerScoreHandler.UpdateScore(points, true);
        SyncScoresAfterPointChange();
        if (EvaluateMatchOutcomeAfterPoints()) return;
        if (_gameEnded || _matchEndPending) return;
        if (IsMyTurn())
            SetCanPlayCard(true);
        else
            UIMANAGER.Instance.DisableButtons();
    }

    [PunRPC]
    private void AwardPoints(int points)
    {
        if (_isSpectator) return;
        myPlayerScoreHandler.UpdateScore(points, true);
        SyncScoresAfterPointChange();
        if (EvaluateMatchOutcomeAfterPoints()) return;
        if (_gameEnded || _matchEndPending) return;
        if (IsMyTurn())
            SetCanPlayCard(true);
    }

    public void CheckAfterTurn()
    {
        CheckForWinner();
        CheckForTurn();
        if (player1Score.Count != player2Score.Count) return;
        if (!PhotonNetwork.IsMasterClient) return;

        int manoPlayer = TrucoHandWinner.GetManoPlayerNumber(DataHandler.Instance.roundNumber);
        int? winner = TrucoHandWinner.Evaluate(trickResults, manoPlayer);
        if (!winner.HasValue) return;

        int winnerActor = winner.Value == 1
            ? PhotonNetwork.LocalPlayer.ActorNumber
            : (PhotonNetwork.PlayerListOthers.Length > 0
                ? PhotonNetwork.PlayerListOthers[0].ActorNumber
                : PhotonNetwork.LocalPlayer.ActorNumber);

<<<<<<< Updated upstream
        if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
        {
            UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
            GetScore(ChallengeType.ConFlorQuiero);
            FlushPendingScoringCardReveal(immediate: true);
        }
        int handPts = ComputeMazoHandPoints();
        TrucoRulesScenarioLog.Ok("Hand decided → Winner RPC",
            "winnerActor=" + winnerActor + " handPts=" + handPts
            + " tricks=" + trickResults.Count
            + " acceptedTruco=" + acceptedTrucoLevel);
        photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, handPts);
=======
        // Lock the hand now (no more cantos / plays), but let the deciding card land and stay
        // visible before Flor reveal + Winner. Guest seats only learn the result via Winner.
        MarkHandResolved();
        TrucoRulesScenarioLog.Ok("Hand decided → hold final card",
            "winnerActor=" + winnerActor + " tricks=" + trickResults.Count
            + " hold=" + FinalCardHoldSeconds + "s");
        StartCoroutine(CoCloseHandAfterFinalCard(winnerActor));
>>>>>>> Stashed changes
    }

    private void CheckChallengePoints()
    {
        int _points = 0;
        _points = UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Vale4) ? 3 :
            UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Retruco) ? 2 :
            UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Truco) ? 1 : 0;
        challengePoints = _points;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (_isSpectator) return;
        if (otherPlayer == null || _gameEnded) return;
        if (otherPlayer.IsLocal) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        if (_isInTournament) return;
        // TrucoPunReconnectionManager waits 60s before walkover â€” do not award win instantly.
        if (TrucoPunReconnectionManager.Instance != null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount > 1) return;

        UIMANAGER.Instance.DisableButtons();
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.GanaPorAbandono);
        GameWon();
    }
}

[System.Serializable]
public class TrickSyncWrapper
{
    public List<ScoreClass> p1 = new List<ScoreClass>();
    public List<ScoreClass> p2 = new List<ScoreClass>();
    public List<int> tricks = new List<int>();
    public int cardPlayed;
}

[System.Serializable]
public class ScoreClass
{
    public CardSuit suit;
    public int value;
}

public enum ChallengeType
{
    None,
    Truco,
    Retruco,
    Vale4,
    Envido,
    RealEnvido,
    FaltaEnvido,
    Flor,
    ContraFlor,
    ConFlorQuiero,
    FlorChica
}
