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

    private List<DeckCards> _player1Cards;
    private List<DeckCards> _player2Cards;
    public bool _gameEnded = false;
    /// <summary>Current hand already conceded (Mazo) — blocks repeat Mazo / timeout turn advance.</summary>
    public bool HandResolved { get; private set; }
    public bool cardPlayed = false;
    public bool otherPlayerDeniedFlor = false;
    public bool deniedFlor = false;

    public List<ChallengeType> ActiveChallenges = new List<ChallengeType>();

    // Track per-trick results: 1 = player1 win, 2 = player2 win, 0 = draw
    private List<int> trickResults = new List<int>();
    bool _pendingScoringCardReveal;
    string _pendingRevealMasterJson = "{}";
    string _pendingRevealGuestJson = "{}";

    // Tournament-specific variables
    private bool _isInTournament = false;
    private bool _tournamentMatchFinalized = false;
    private bool _1v1ResultPosted;
    [SerializeField] private bool _isSpectator;

    /// <summary>Winner/Mazo already applied this hand — blocks duplicate +2 from timeout ensure.</summary>
    bool _handOutcomeCommitted;
    /// <summary>Nueva mano scene reload already scheduled — ignore late Mazo/Winner.</summary>
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
    /// Scores are keyed by stable ActorNumber order (low, high) — never by Photon MasterClient.
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
            + " local=" + local + " → me=" + mine + " opp=" + other);
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
        else if (photonView != null)
            photonView.RPC(nameof(RequestSyncFromClient), RpcTarget.MasterClient);
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

    void MasterResolveNoQuiero(int declinerActor, ChallengeType type)
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        int winnerActor = GetOpponentActorNumber(declinerActor);
        int pts = TrucoRulePoints.NoQuieroAward(type);
        bool endHand = TrucoRulePoints.NoQuieroEndsHand(type);
        TrucoRulesScenarioLog.Ok("MasterResolveNoQuiero",
            "decliner=" + declinerActor + " winner=" + winnerActor
            + " type=" + type + " pts=" + pts + " endHand=" + endHand);
        BroadcastNetworkAward(winnerActor, pts, endHand);
    }

    static ChallengeType ParseChallengeTypeFromEvent(EventData photonEvent, ChallengeType fallback)
    {
        if (photonEvent.CustomData is byte b) return (ChallengeType)b;
        if (photonEvent.CustomData is object[] arr && arr.Length > 0 && arr[0] is byte bb)
            return (ChallengeType)bb;
        return fallback;
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
            EndRound();
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

    /// <summary>NuevaMano scene reload already queued — do not deal/start another hand.</summary>
    public bool IsRestartScheduled() => _restartScheduled;

    void ResetHandState()
    {
        HandResolved = false;
        cardPlayed = false;
        trickResults.Clear();
        player1Score.Clear();
        player2Score.Clear();
        points = 1;
        challengePoints = 0;
        mazoPoints = 0;
        noQuieroPoints = 0;
        lastChallengeType = ChallengeType.None;
        ActiveChallenges.Clear();
    }

    /// <summary>Called after reconnect so the returning client catches up on score / turn.</summary>
    public void RequestStateSyncAfterReconnect()
    {
        if (_isSpectator || _gameEnded || !PhotonNetwork.InRoom) return;
        if (PhotonNetwork.IsMasterClient)
            BroadcastMatchState();
        else
            photonView.RPC(nameof(RequestSyncFromClient), RpcTarget.MasterClient);
    }

    [PunRPC]
    void RequestSyncFromClient()
    {
        if (!PhotonNetwork.IsMasterClient || _gameEnded) return;
        BroadcastMatchState();
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
        photonView.RPC(nameof(SyncMatchState), RpcTarget.Others, scoreLow, scoreHigh, turnActor, DataHandler.Instance.roundNumber, HandResolved ? 1 : 0);
        TrucoRulesScenarioLog.Ok("BroadcastMatchState by actor",
            "a" + lowActor + "=" + scoreLow + " a" + highActor + "=" + scoreHigh
            + " turn=" + turnActor + " round=" + DataHandler.Instance.roundNumber);
    }

    [PunRPC]
    void SyncMatchState(int scoreLowActor, int scoreHighActor, int turnActor, int roundNum, int handResolvedFlag)
    {
        if (_isSpectator || _gameEnded) return;
        ApplyAuthoritativeScores(scoreLowActor, scoreHighActor);
        DataHandler.Instance.roundNumber = roundNum;
        TrucoRulesScenarioLog.Ok("SyncMatchState applied",
            "scoreLow=" + scoreLowActor + " scoreHigh=" + scoreHighActor
            + " turnActor=" + turnActor + " round=" + roundNum
            + " handResolved=" + handResolvedFlag
            + " me=" + (myPlayerScoreHandler != null ? myPlayerScoreHandler.GetCurrentScore() : -1)
            + " opp=" + (otherPlayerScoreHandler != null ? otherPlayerScoreHandler.GetCurrentScore() : -1));
        if (handResolvedFlag == 1 && !HandResolved)
            MarkHandResolved();
        // After Mazo/Winner do not revive the folder's turn — wait for NuevaMano.
        if (HandResolved || _restartScheduled || _handOutcomeCommitted || handResolvedFlag == 1)
        {
            SetMyTurn(false);
            SetCanPlayCard(false);
            TurnManager.Instance?.StopAllTurnTimers();
            return;
        }
        TurnManager.Instance?.ResumeTurnAfterSync(turnActor);
        TurnManager.Instance?.RestartTurnTimersIfActive();
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
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            RequestStateSyncAfterReconnect();
        if (!_isInTournament && !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId))
        {
            // Only once per match — scene reloads every mano would otherwise spam POST /start-game.
            if (PhotonNetwork.IsMasterClient && !OneVsOneMatchSession.GameStarted)
            {
                TrucoRulesScenarioLog.Backend("POST /start-game",
                    "match=" + OneVsOneMatchSession.CurrentMatchId);
                _ = ApiController.TryStartMatchGame1v1(OneVsOneMatchSession.CurrentMatchId);
            }
            OneVsOneMatchSession.MarkGameStarted();
        }
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(CacheTrucoOpponentUserId));
        if (!_isSpectator) TrucoReturnFromGameplayCleanup.MarkLeavingGameplay(_1v1ResultPosted);
    }

    void CacheTrucoOpponentUserId()
    {
        if (_gameEnded || _isSpectator || !PhotonNetwork.InRoom) return;
        var id = PhotonPlayerHelper.GetOtherTrucoPlayerUserId();
        if (!string.IsNullOrEmpty(id)) OneVsOneMatchSession.SetCachedOpponentUserId(id);
    }

    /// <summary>Public for <see cref="TrucoPunReconnectionManager"/> / UI exit.</summary>
    public bool IsTournamentGameplay() => _isInTournament;

    /// <summary>After reconnect window expires: 1v1 = report opponent as winner; tournament = leave + loss; return to main menu. Server is source of truth for coins; client submits result when possible.</summary>
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
        if (!_1v1ResultPosted && !string.IsNullOrEmpty(matchIdSnapshot))
        {
            var winner = OneVsOneMatchSession.CachedOpponentUserId;
            if (!string.IsNullOrEmpty(winner))
            {
                _1v1ResultPosted = true;
                if (PhotonNetwork.IsMasterClient)
                    _ = ApiController.Finalize1v1MatchAsMaster(matchIdSnapshot, winner);
                else
                    _ = ApiController.Finalize1v1MatchAsGuest(matchIdSnapshot);
            }
        }
        if (ApiController.GetSessionUser?.Data?.stats != null)
            ApiController.GetSessionUser.Data.stats.losses++;
        OneVsOneMatchSession.Clear();
        if (!_1v1ResultPosted && !string.IsNullOrEmpty(matchIdSnapshot))
            _ = ApiController.CancelPreGameMatch1v1(matchIdSnapshot);
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexionPerdida1v1);
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
        if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
            GameLost();
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
            if (_cardWrapper2.cards != null)
            {
                for (int i = 0; i < _cardWrapper2.cards.Count; i++)
                {
                    cards[i].SetupCard(_cardWrapper2.cards[i].suit, _cardWrapper2.cards[i].rank);
                }
            }
            if (!PlayerHasFlor())
            {
                deniedFlor = true;
                photonView.RPC(nameof(NoFlor), RpcTarget.OthersBuffered);
            }
        }
        else if (photonEvent.Code == UIMANAGER.TRUCO_CHALLENGE)
        {
            challengePoints = 1;
            otherPlayerDeniedFlor = true;
            lastChallengeType = ChallengeType.Truco;
            if (photonEvent.Sender > 0)
                UIMANAGER.Instance.unAnsweredChallenges[ChallengeType.Truco] = photonEvent.Sender;
            TrucoRulesScenarioLog.Opp("RECV Truco", "fromActor=" + photonEvent.Sender + " responseTimer=30s");
            UIMANAGER.Instance.TrucoChallenged();
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown();
        }
        else if (photonEvent.Code == UIMANAGER.RETRUCO_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV Retruco", "fromActor=" + photonEvent.Sender);
            challengePoints = 2;
            UIMANAGER.Instance.RetrucoChallenged();
            lastChallengeType = ChallengeType.Retruco;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown();
        }
        else if (photonEvent.Code == UIMANAGER.VALE4_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV Vale4", "fromActor=" + photonEvent.Sender);
            challengePoints = 3;
            UIMANAGER.Instance.Vale4Challenged();
            lastChallengeType = ChallengeType.Vale4;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown();
        }
        else if (photonEvent.Code == UIMANAGER.ENVIDO_CHALLENGE)
        {
            otherPlayerDeniedFlor = true;
            if (lastChallengeType != ChallengeType.Envido)
            {
                challengePoints = 0;
            }
            challengePoints += 2;
            TrucoRulesScenarioLog.Opp("RECV Envido", "fromActor=" + photonEvent.Sender + " challengePts=" + challengePoints);
            UIMANAGER.Instance.EnvidoChallenged();
            lastChallengeType = ChallengeType.Envido;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown();
        }
        else if (photonEvent.Code == UIMANAGER.REALENVIDO_CHALLENGE)
        {
            otherPlayerDeniedFlor = true;
            if (lastChallengeType == ChallengeType.Truco)
            {
                challengePoints = 0;
            }
            challengePoints += 3;
            TrucoRulesScenarioLog.Opp("RECV RealEnvido", "fromActor=" + photonEvent.Sender + " challengePts=" + challengePoints);
            UIMANAGER.Instance.RealEnvidoChallenged();
            lastChallengeType = ChallengeType.RealEnvido;
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown();
        }
        else if (photonEvent.Code == UIMANAGER.FALTAENVIDO_CHALLENGE)
        {
            otherPlayerDeniedFlor = true;
            TrucoRulesScenarioLog.Opp("RECV FaltaEnvido", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.FaltaEnvido;
            UIMANAGER.Instance.FaltaEnvidoChallenged();
            SetCanPlayCard(false);
            UIMANAGER.Instance.BeginChallengeResponseCountdown();
        }
        else if (photonEvent.Code == UIMANAGER.QUEIRO_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV Quiero (accept)",
                "fromActor=" + photonEvent.Sender + " for=" + lastChallengeType);
            UIMANAGER.Instance.QueiroChallenged();
            if (lastChallengeType == ChallengeType.Envido ||
                lastChallengeType == ChallengeType.RealEnvido ||
                lastChallengeType == ChallengeType.FaltaEnvido)
            {
                photonView.RPC(nameof(GetScore), RpcTarget.MasterClient, lastChallengeType);
            }
            else if (lastChallengeType == ChallengeType.ContraFlor)
            {
                ShowAllCards();
                photonView.RPC(nameof(GetScore), RpcTarget.MasterClient, ChallengeType.ContraFlor);
            }
            else if (lastChallengeType == ChallengeType.Truco)
            {
                if (UIMANAGER.Instance._envidoPlayed)
                {
                    mazoPoints = 1;
                }
                else
                {
                    mazoPoints = 2;
                }
                UIMANAGER.Instance.trucoPlayed = true;
            }
            else if (lastChallengeType == ChallengeType.Retruco)
            {
                mazoPoints = 3;
                UIMANAGER.Instance.trucoPlayed = true;
            }
            else if (lastChallengeType == ChallengeType.Vale4)
            {
                mazoPoints = 4;
                UIMANAGER.Instance.trucoPlayed = true;
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
            TrucoRulesScenarioLog.Opp("RECV NoQuiero (decline)",
                "fromActor=" + photonEvent.Sender + " type=" + resolvedType
                + " awardPts=" + TrucoRulePoints.NoQuieroAward(resolvedType)
                + " endsHand=" + TrucoRulePoints.NoQuieroEndsHand(resolvedType));
            UIMANAGER.Instance.NoQueiroChallenged();
            if (resolvedType == ChallengeType.Truco ||
                resolvedType == ChallengeType.Retruco ||
                resolvedType == ChallengeType.Vale4)
                UIMANAGER.Instance.trucoPlayed = true;
            if (PhotonNetwork.IsMasterClient)
                MasterResolveNoQuiero(photonEvent.Sender, resolvedType);
        }
        else if (photonEvent.Code == UIMANAGER.FLOR_CHALLENGE)
        {
            bool autoAward = photonEvent.CustomData is byte fb && fb == UIMANAGER.FlorAutoAwardFlag;
            if (autoAward)
            {
                TrucoRulesScenarioLog.Opp("RECV Flor AUTO-AWARD +3", "fromActor=" + photonEvent.Sender);
                UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
                if (PhotonNetwork.IsMasterClient && photonEvent.Sender > 0)
                    BroadcastNetworkAward(photonEvent.Sender, 3, false);
                if (IsMyTurn())
                    SetCanPlayCard(true);
                return;
            }
            if (deniedFlor)
            {
                TrucoRulesScenarioLog.Opp("RECV Flor → auto +3 (local deniedFlor)", "fromActor=" + photonEvent.Sender);
                UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
                if (PhotonNetwork.IsMasterClient && photonEvent.Sender > 0)
                    BroadcastNetworkAward(photonEvent.Sender, 3, false);
                if (IsMyTurn())
                    SetCanPlayCard(true);
                return;
            }
            TrucoRulesScenarioLog.Opp("RECV Flor challenge", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.Flor;
            UIMANAGER.Instance.FlorChallenged();
        }
        else if (photonEvent.Code == UIMANAGER.FLOR_CHICA_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV FlorChica → rival gets +4", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.FlorChica;
            UIMANAGER.Instance.FlorChicaChallenged();
            if (PhotonNetwork.IsMasterClient && photonEvent.Sender > 0)
                BroadcastNetworkAward(GetOpponentActorNumber(photonEvent.Sender), 4, false);
            if (IsMyTurn())
                SetCanPlayCard(true);
        }
        else if (photonEvent.Code == UIMANAGER.CON_FLOR_QUIERO_CHALLENGE)
        {
            TrucoRulesScenarioLog.Opp("RECV ConFlorQuiero", "fromActor=" + photonEvent.Sender);
            lastChallengeType = ChallengeType.ConFlorQuiero;
            challengePoints = 5;
            UIMANAGER.Instance.ConFlorQuieroChallenged();
            if (IsMyTurn())
            {
                SetCanPlayCard(true);
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
            TrucoRulesScenarioLog.Ok("RECV Mazo → master ResolveMazoHand",
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

    int ComputeMazoHandPoints()
    {
        int _points = 0;
        if (!lastChallengeType.Equals(ChallengeType.None))
        {
            ActiveChallenges.Remove(lastChallengeType);
            _points = CheckForMazo();
        }
        else if (!cardPlayed)
        {
            // Timeout / fold without a challenge: always +2 (or +1 if Truco already accepted).
            // Do NOT award +4 for undeclared flor in hand — that flipped first-hand vs later mazos.
            if (UIMANAGER.Instance != null && UIMANAGER.Instance.trucoPlayed)
                _points = 1;
            else if (deniedFlor && UIMANAGER.Instance != null
                     && UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Flor))
                _points = 1;
            else
                _points = 2;
        }
        else
            _points = 1;
        return _points;
    }

    void ResolveMazoHand(int winnerActor)
    {
        if (_gameEnded || HandResolved || _handOutcomeCommitted || _restartScheduled) return;
        MarkHandResolved();
        int points = ComputeMazoHandPoints();
        TrucoRulesScenarioLog.Ok("ResolveMazoHand",
            "winnerActor=" + winnerActor + " handPts=" + points
            + " last=" + lastChallengeType + " cardPlayed=" + cardPlayed
            + " trucoPlayed=" + (UIMANAGER.Instance != null && UIMANAGER.Instance.trucoPlayed));
        photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, points);
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
                case ChallengeType.Envido:
                    pointsToAdd += 2;
                    break;
                case ChallengeType.RealEnvido:
                    pointsToAdd += 3;
                    break;
            }
        }

        if (!UIMANAGER.Instance.trucoPlayed && ActiveChallenges.Count <= 0)
        {
            mazoPoints = 1;
        }

        if (ActiveChallenges.Count <= 0 && lastChallengeType == ChallengeType.Envido && !UIMANAGER.Instance._envidoPlayed)
        {
            pointsToAdd = 1;
        }

        return pointsToAdd + mazoPoints;
    }

    public void CheckToAwardsPoints()
    {
        // Legacy path — scoring is master-authoritative via RPC_NetworkAwardPoints.
        if (!PhotonNetwork.IsMasterClient) return;
    }

    [PunRPC]
    private void CheckForTrick()
    {
        if (_isSpectator) return;
        if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
        {
            UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
            Debug.LogWarning("Checking for Con Flor Quiero");
            GetScore(ChallengeType.ConFlorQuiero);
        }
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
                    photonView.RPC(nameof(AnnounceEnvidoPoints), RpcTarget.All, player1Score, player2Score);
                    QueueEnvidoCardReveal();

                    int myscore = myPlayerScoreHandler.GetCurrentScore();
                    int otherscore = otherPlayerScoreHandler.GetCurrentScore();
                    int biggerScore = Mathf.Max(myscore, otherscore);

                    int pointsToAward = (biggerScore + challengePoints) > 15 ? 15 - biggerScore : challengePoints;

                    TrucoRulesScenarioLog.Ok("GetScore Envido/Real compare",
                        "type=" + _type + " p1Envido=" + player1Score + " p2Envido=" + player2Score
                        + " awardPts=" + pointsToAward + " challengePts=" + challengePoints);

                    if (player2Score > player1Score)
                    {
                        photonView.RPC(nameof(EnvidoWinner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, pointsToAward);
                    }
                    else if (player1Score > player2Score)
                    {
                        photonView.RPC(nameof(EnvidoWinner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, pointsToAward);
                    }
                    else
                    {
                        int manoActor = TrucoHandWinner.GetManoPlayerNumber(DataHandler.Instance.roundNumber) == 1
                            ? PhotonNetwork.LocalPlayer.ActorNumber
                            : PhotonNetwork.PlayerListOthers[0].ActorNumber;
                        photonView.RPC(nameof(EnvidoWinner), RpcTarget.All, manoActor, pointsToAward);
                    }

                    Debug.LogWarning("Envido Points Awarded");
                    challengePoints = 0;
                    break;
                }
            case ChallengeType.FaltaEnvido:
                {
                    int player1Score = CalculateEnvido(_player1Cards);
                    int player2Score = CalculateEnvido(_player2Cards);
                    photonView.RPC(nameof(AnnounceEnvidoPoints), RpcTarget.All, player1Score, player2Score);
                    QueueEnvidoCardReveal();
                    int myscore = myPlayerScoreHandler.GetCurrentScore();
                    int otherscore = otherPlayerScoreHandler.GetCurrentScore();
                    int biggerScore = Mathf.Max(myscore, otherscore);
                    int faltaPts = 15 - biggerScore;
                    TrucoRulesScenarioLog.Ok("GetScore FaltaEnvido",
                        "p1=" + player1Score + " p2=" + player2Score + " award=" + faltaPts);
                    if (player2Score > player1Score)
                    {
                        photonView.RPC(nameof(FaltaEnvidoWinner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, faltaPts);
                    }
                    else if (player1Score > player2Score)
                    {
                        photonView.RPC(nameof(FaltaEnvidoWinner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, faltaPts);
                    }
                    else
                    {
                        int manoActor = TrucoHandWinner.GetManoPlayerNumber(DataHandler.Instance.roundNumber) == 1
                            ? PhotonNetwork.LocalPlayer.ActorNumber
                            : PhotonNetwork.PlayerListOthers[0].ActorNumber;
                        photonView.RPC(nameof(FaltaEnvidoWinner), RpcTarget.All, manoActor, faltaPts);
                    }
                    break;
                }
            case ChallengeType.ConFlorQuiero:
                {
                    int player1FlorScore = CalculateFlor(_player1Cards);
                    int player2FlorScore = CalculateFlor(_player2Cards);
                    photonView.RPC(nameof(AnnounceFlorPoints), RpcTarget.All, player1FlorScore, player2FlorScore);
                    QueueFlorCardReveal();
                    TrucoRulesScenarioLog.Ok("GetScore ConFlorQuiero",
                        "p1Flor=" + player1FlorScore + " p2Flor=" + player2FlorScore + " award=6");
                    if (player2FlorScore > player1FlorScore)
                    {
                        photonView.RPC(nameof(ConFlorQuieroWinner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, 6);
                    }
                    else
                    {
                        photonView.RPC(nameof(ConFlorQuieroWinner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, 6);
                    }
                    points = 1;
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

    void QueueEnvidoCardReveal()
    {
        if (!PhotonNetwork.IsMasterClient || _player1Cards == null || _player2Cards == null) return;
        _pendingRevealMasterJson = SerializeRevealCards(GetBestEnvidoRevealCards(_player1Cards));
        _pendingRevealGuestJson = SerializeRevealCards(GetBestEnvidoRevealCards(_player2Cards));
        _pendingScoringCardReveal = true;
    }

    void QueueFlorCardReveal()
    {
        if (!PhotonNetwork.IsMasterClient || _player1Cards == null || _player2Cards == null) return;
        _pendingRevealMasterJson = SerializeRevealCards(_player1Cards);
        _pendingRevealGuestJson = SerializeRevealCards(_player2Cards);
        _pendingScoringCardReveal = true;
    }

    void FlushPendingScoringCardReveal()
    {
        if (!_pendingScoringCardReveal) return;
        _pendingScoringCardReveal = false;
        photonView.RPC(nameof(RevealScoringCardsRpc), RpcTarget.All, _pendingRevealMasterJson, _pendingRevealGuestJson);
    }

    [PunRPC]
    void RevealScoringCardsRpc(string masterJson, string guestJson)
    {
        if (UIMANAGER.Instance == null) return;
        var m = JsonUtility.FromJson<CardRevealPayloadList>(masterJson);
        var g = JsonUtility.FromJson<CardRevealPayloadList>(guestJson);
        var mine = PhotonNetwork.IsMasterClient ? m : g;
        var theirs = PhotonNetwork.IsMasterClient ? g : m;
        if (mine?.items != null)
            foreach (var c in mine.items)
                UIMANAGER.Instance.ShowRevealedScoringCard(c.suit, c.rank, true);
        if (theirs?.items != null)
            foreach (var c in theirs.items)
                UIMANAGER.Instance.ShowRevealedScoringCard(c.suit, c.rank, false);
    }

    [PunRPC]
    private void AnnounceEnvidoPoints(int p1, int p2)
    {
        if (UIMANAGER.Instance == null) return;
        if (_isSpectator)
        {
            UIMANAGER.Instance.ShowDeclarationCallout($"Envido: {p1} - {p2}", false);
            return;
        }
        int mine = PhotonNetwork.IsMasterClient ? p1 : p2;
        int theirs = PhotonNetwork.IsMasterClient ? p2 : p1;
        TrucoGameplayAudio.PlayDeclarationPhrase($"Tengo {mine}", true);
        TrucoGameplayAudio.PlayDeclarationPhrase($"Tengo {theirs}", false);
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
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameWon();
        }
        else if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameLost();
        }
        else
        {
            SyncScoresAfterPointChange();
            DataHandler.Instance.points = myPlayerScoreHandler.GetCurrentScore();
            PhotonNetwork.AutomaticallySyncScene = true;
            ResetGame();
        }
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
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameWon();
        }
        else if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameLost();
        }
    }

    [PunRPC]
    private void ContraFlorWinner(int ID)
    {
        if (_isSpectator) return;
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            int score = myPlayerScoreHandler.GetCurrentScore();
            int scoreToAdd = 15 - score;
            myPlayerScoreHandler.UpdateScore(scoreToAdd, true);
            Debug.Log("You Win!");
            UIMANAGER.Instance.UpdateTurnText("You Win!", 3.5f);
            UIMANAGER.Instance.DisableButtons();
            GameWon();
        }
        else
        {
            int score = otherPlayerScoreHandler.GetCurrentScore();
            int scoreToAdd = 15 - score;
            otherPlayerScoreHandler.UpdateScore(scoreToAdd, true);
            Debug.Log("You lose!");
            UIMANAGER.Instance.UpdateTurnText("You lose!", 3.5f);
            UIMANAGER.Instance.DisableButtons();
            GameLost();
        }
        SyncScoresAfterPointChange();
    }

    [PunRPC]
    private void FaltaEnvidoWinner(int ID, int _points)
    {
        if (_isSpectator) return;
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
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
            GameWon();
        else if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
            GameLost();
        else
        {
            if (!UIMANAGER.Instance.trucoPlayed)
            {
                if (UIMANAGER.Instance.unAnsweredChallenges.Count > 0)
                    StartCoroutine(UIMANAGER.Instance.CheckForUnansweredChallenges());
                else if (IsMyTurn())
                    SetCanPlayCard(true);
                return;
            }
            SyncScoresAfterPointChange();
            PhotonNetwork.AutomaticallySyncScene = true;
            ResetGame();
        }
    }

    public bool PlayerHasFlor()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Checking for player 1's flor");
            Debug.LogWarning("Player 1 Cards Count: " + _player1Cards.Count);
            return HasFlor(_player1Cards);
        }
        else
        {
            Debug.LogWarning("Checking for player 2's flor");
            Debug.LogWarning("Player 2 Cards Count: " + _player2Cards.Count);
            return HasFlor(_player2Cards);
        }
    }

    public bool HasFlor(List<DeckCards> hand)
    {
        if (hand == null || hand.Count != 3 || deniedFlor || cardPlayed)
        {
            Debug.LogError("Flor requires exactly 3 cards.");
            return false;
        }

        bool result = hand.All(card => card.suit == hand[0].suit);
        Debug.LogWarning("has Flor: " + result);
        return result;
    }

    public int CalculateFlor(List<DeckCards> hand)
    {
        if (!HasFlor(hand))
            return 0;

        var values = hand.Select(card => GetEnvidoValue(card.rank)).OrderByDescending(v => v).ToList();
        return 20 + values[0] + values[1];
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
        _canPlayCard = _value;
        if (_value && IsMyTurn())
            UIMANAGER.Instance.EnableButtons();
    }

    public bool CanPlayCard()
    {
        return _canPlayCard && !_gameEnded;
    }

    public bool IsMyTurn() => _myTurn;

    public void SetMyTurn(bool _value)
    {
        _myTurn = _value;
    }

    public void PlayFirstHandCardOnTimeout()
    {
        if (!IsMyTurn() || _gameEnded || HandResolved || _handOutcomeCommitted || _restartScheduled) return;
        TrucoRulesScenarioLog.Ok("PlayTimeout → TriggerMazoOnTimeout (no card auto-play)");
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
            int manoPlayer = TrucoHandWinner.GetManoPlayerNumber(DataHandler.Instance.roundNumber);
            int manoActor = manoPlayer == 1
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : (PhotonNetwork.PlayerListOthers.Length > 0
                    ? PhotonNetwork.PlayerListOthers[0].ActorNumber
                    : PhotonNetwork.LocalPlayer.ActorNumber);
            TurnManager.Instance.GiveTurnAgainToActor(manoActor);
            Debug.LogWarning("Draw (parda) — mano keeps lead");
        }
    }

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
        FlushPendingScoringCardReveal();
        UIMANAGER.Instance?.ResetHandChallengeState();
        UIMANAGER.Instance.DisableButtons();
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

        // Winner already ran on All clients with the same actor id — only persist locally.
        // Re-broadcasting via master/guest sync was flipping scores when MasterClient rotated.
        PersistMatchScoresToDataHandler();
        TrucoRulesScenarioLog.Ok("HandWinner RPC",
            "winnerActor=" + ID + " handPts=+" + points + " toMe=" + mine
            + " meNow=" + myPlayerScoreHandler.GetCurrentScore()
            + " oppNow=" + otherPlayerScoreHandler.GetCurrentScore()
            + " localActor=" + PhotonNetwork.LocalPlayer.ActorNumber);

        if (!EvaluateMatchOutcomeAfterPoints())
            ResetGame();
    }

    bool EvaluateMatchOutcomeAfterPoints()
    {
        int me = myPlayerScoreHandler.GetCurrentScore();
        int opp = otherPlayerScoreHandler.GetCurrentScore();
        if (me >= 15)
        {
            TrucoRulesScenarioLog.Ok("MatchEnd → GameWon (≥15)", "me=" + me + " opp=" + opp);
            GameWon();
            return true;
        }
        if (opp >= 15)
        {
            TrucoRulesScenarioLog.Ok("MatchEnd → GameLost (rival ≥15)", "me=" + me + " opp=" + opp);
            GameLost();
            return true;
        }
        return false;
    }

    public void EndRound()
    {
        Debug.LogWarning("Setting My Player Score: " + myPlayerScoreHandler.GetCurrentScore());
        SyncScoresAfterPointChange();
        PhotonNetwork.AutomaticallySyncScene = true;
        UIMANAGER.Instance.DisableButtons();
        if (!EvaluateMatchOutcomeAfterPoints())
            ResetGame();
    }

    void TryReport1v1MatchToBackend(string winnerUserId, System.Action onSettled = null)
    {
        if (_isInTournament || _isSpectator) return;
        if (string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId)) return;
        if (_1v1ResultPosted) return;
        if (string.IsNullOrEmpty(winnerUserId)) return;
        _1v1ResultPosted = true;
        TrucoRulesScenarioLog.Backend("POST /result start",
            "match=" + OneVsOneMatchSession.CurrentMatchId
            + " winnerUserId=" + winnerUserId
            + " isMaster=" + PhotonNetwork.IsMasterClient);
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        if (PhotonNetwork.IsMasterClient)
            _ = ApiController.Finalize1v1MatchAsMaster(matchId, winnerUserId, onSettled);
        else
            _ = ApiController.Finalize1v1MatchAsGuest(matchId, onSettled);
    }

    /// <summary>The opponent failed to reconnect within the window: the player who stayed wins (walkover).</summary>
    public async void WinByOpponentWalkover()
    {
        if (_gameEnded) return;
        TrucoRulesScenarioLog.Ok("Walkover WIN (rival reconnect failed)",
            "match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?"));
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.GanaPorAbandono);
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        string claimerId = PhotonPlayerHelper.GetLocalTrucoPlayerUserId();
        if (!string.IsNullOrEmpty(matchId) && !string.IsNullOrEmpty(claimerId) && !_1v1ResultPosted)
        {
            _1v1ResultPosted = true;
            bool ok = await ApiController.ClaimWalkover1v1(matchId, claimerId, err =>
            {
                TrucoRulesScenarioLog.BackendFail("ClaimWalkover", err);
                Debug.LogWarning("[GameManager] walkover API: " + err);
            });
            if (!ok)
            {
                // Fallback if walkover route rejects — still settle via /result when possible.
                _1v1ResultPosted = false;
                TryReport1v1MatchToBackend(claimerId);
            }
        }
        GameWon();
    }

    private void GameLost()
    {
        if (_gameEnded) return;
        CancelPendingHandRestart();
        TrucoRulesScenarioLog.Ok("GameLost UI + backend settle",
            "me=" + myPlayerScoreHandler.GetCurrentScore()
            + " opp=" + otherPlayerScoreHandler.GetCurrentScore());
        int entryFee = OneVsOneMatchSession.EntryFee;
        TryReport1v1MatchToBackend(PhotonPlayerHelper.GetOtherTrucoPlayerUserId(),
            () => TrucoMatchEndUiPolish.RefreshBalance(losePanel));
        TrucoMatchProgress.ClearAllMatchMemory();
        photonView.Controller.SetScore(myPlayerScoreHandler.GetCurrentScore());
        UIMANAGER.Instance.DisableButtons();

        if (_isInTournament && !_tournamentMatchFinalized)
        {
            OnTournamentMatchLose();
        }
        else
        {
            losePanel.SetActive(true);
            TrucoMatchEndUiPolish.Apply(losePanel, false, entryFee);
            ToggleMenuBtns(true);
        }

        _gameEnded = true;
        FinalizePhotonRoomAfterMatchEnd();
    }

    private void GameWon()
    {
        if (_gameEnded) return;
        CancelPendingHandRestart();
        TrucoRulesScenarioLog.Ok("GameWon UI + backend settle",
            "me=" + myPlayerScoreHandler.GetCurrentScore()
            + " opp=" + otherPlayerScoreHandler.GetCurrentScore()
            + " fee=" + OneVsOneMatchSession.EntryFee);
        int entryFee = OneVsOneMatchSession.EntryFee;
        TryReport1v1MatchToBackend(ApiController.GetSessionUser?.Data?._id,
            () => TrucoMatchEndUiPolish.RefreshBalance(winPanel));
        TrucoMatchProgress.ClearAllMatchMemory();
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.GanastePartida);
        int prize = Player1v1MatchExtensions.ComputeOneVsOnePrize(entryFee);
        if (prize > 0)
            AppManager.Instance?.DisplayNotification(string.Format(TrucoTextosClient.GanastePremio, prize));
        UIMANAGER.Instance.DisableButtons();

        if (_isInTournament && !_tournamentMatchFinalized)
        {
            OnTournamentMatchWon();
        }
        else
        {
            winPanel.SetActive(true);
            TrucoMatchEndUiPolish.Apply(winPanel, true, entryFee);
            ToggleMenuBtns(true);
        }

        _gameEnded = true;
        FinalizePhotonRoomAfterMatchEnd();
    }

    void CancelPendingHandRestart()
    {
        if (_restartRoutine != null)
        {
            StopCoroutine(_restartRoutine);
            _restartRoutine = null;
            TrucoRulesScenarioLog.Ok("CancelPendingHandRestart",
                "reason=match ended — do not reload Gameplay");
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
        // Do NOT ResetHandState() here — clearing HandResolved let the 2.5s timeout
        // ensure fire a second Mazo/Winner (+2 twice) before the scene reload.
        TrucoRulesScenarioLog.Ok("ResetGame → NuevaMano reload scheduled",
            "keep HandResolved=true until next scene");
        _restartRoutine = StartCoroutine(RestartGameAfterDelay());
    }

    private IEnumerator RestartGameAfterDelay()
    {
        UIMANAGER.Instance?.UpdateTurnText(TrucoTextosClient.NuevaMano, 2.5f);
        yield return new WaitForSeconds(2.5f);
        _restartRoutine = null;
        if (_gameEnded)
        {
            TrucoRulesScenarioLog.Ok("NuevaMano reload aborted (match ended)");
            yield break;
        }
        PhotonNetwork.AutomaticallySyncScene = true;
        TrucoSceneTransition.Go("Gameplay");
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
        if (!_gameEnded && IsMyTurn())
            SetCanPlayCard(true);
        else if (!_gameEnded)
            UIMANAGER.Instance.EnableButtons();
    }

    [PunRPC]
    private void AwardPoints(int points)
    {
        if (_isSpectator) return;
        myPlayerScoreHandler.UpdateScore(points, true);
        SyncScoresAfterPointChange();
        if (EvaluateMatchOutcomeAfterPoints()) return;
        if (!_gameEnded && IsMyTurn())
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

        if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
        {
            UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
            GetScore(ChallengeType.ConFlorQuiero);
        }
        CheckChallengePoints();
        points += challengePoints;
        TrucoRulesScenarioLog.Ok("Hand decided → Winner RPC",
            "winnerActor=" + winnerActor + " handPts=" + points
            + " tricks=" + trickResults.Count);
        photonView.RPC(nameof(Winner), RpcTarget.All, winnerActor, points);
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
        // TrucoPunReconnectionManager waits 60s before walkover — do not award win instantly.
        if (TrucoPunReconnectionManager.Instance != null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount > 1) return;

        UIMANAGER.Instance.DisableButtons();
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.GanaPorAbandono);
        GameWon();
    }
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