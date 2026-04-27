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
    public bool cardPlayed = false;
    public bool otherPlayerDeniedFlor = false;
    public bool deniedFlor = false;

    public List<ChallengeType> ActiveChallenges = new List<ChallengeType>();

    // Track per-trick results: 1 = player1 win, 2 = player2 win, 0 = draw
    private List<int> trickResults = new List<int>();

    // Tournament-specific variables
    private bool _isInTournament = false;
    private bool _tournamentMatchFinalized = false;
    private bool _1v1ResultPosted;
    [SerializeField] private bool _isSpectator;

    private void Awake()
    {
        Instance = this;
        _isSpectator = SpectatorContext.IsSpectator;
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
        if (myPlayerScoreHandler != null)
            myPlayerScoreHandler.UpdateScore(DataHandler.Instance.points);
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
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(CacheTrucoOpponentUserId));
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
            SceneManager.LoadScene("MainMenu");
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
            SceneManager.LoadScene("MainMenu");
            return;
        }
        if (!_1v1ResultPosted && !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId))
        {
            var winner = OneVsOneMatchSession.CachedOpponentUserId;
            if (!string.IsNullOrEmpty(winner))
            {
                _1v1ResultPosted = true;
                _ = ApiController.SubmitMatchResult1v1(OneVsOneMatchSession.CurrentMatchId, winner, null);
            }
        }
        if (ApiController.GetSessionUser?.Data?.stats != null)
            ApiController.GetSessionUser.Data.stats.losses++;
        OneVsOneMatchSession.Clear();
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexionPerdida1v1);
        SceneManager.LoadScene("MainMenu");
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
            UIMANAGER.Instance.UpdateTurnText("Tournament Champion!");
            AppManager.Instance.DisplayNotification("Congratulations, you are the champion");
            MultiplayerController.FinalizeCurrentMatch(true);

            LeanTween.delayedCall(5, () =>
            {
                ToggleMenuBtns(true);
            });
        }
        else
        {
            UIMANAGER.Instance.UpdateTurnText("Waiting for next round...");

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
        if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            _gameEnded = true;
            GameLost();
        }
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
        Debug.LogWarning("EventReceived: " + photonEvent.Code);
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
            UIMANAGER.Instance.unAnsweredChallenges.Clear();
            Debug.Log("Truco challenge received");
            challengePoints = 1;
            otherPlayerDeniedFlor = true;
            UIMANAGER.Instance.TrucoChallenged();
            lastChallengeType = ChallengeType.Truco;
            SetCanPlayCard(false);
        }
        else if (photonEvent.Code == UIMANAGER.RETRUCO_CHALLENGE)
        {
            Debug.Log("Truco challenge received");
            challengePoints = 2;
            UIMANAGER.Instance.RetrucoChallenged();
            lastChallengeType = ChallengeType.Retruco;
            SetCanPlayCard(false);
        }
        else if (photonEvent.Code == UIMANAGER.VALE4_CHALLENGE)
        {
            Debug.Log("Truco challenge received");
            challengePoints = 3;
            UIMANAGER.Instance.Vale4Challenged();
            lastChallengeType = ChallengeType.Vale4;
            SetCanPlayCard(false);
        }
        else if (photonEvent.Code == UIMANAGER.ENVIDO_CHALLENGE)
        {
            otherPlayerDeniedFlor = true;
            Debug.Log("Truco challenge received");
            if (lastChallengeType != ChallengeType.Envido)
            {
                challengePoints = 0;
            }
            challengePoints += 2;
            UIMANAGER.Instance.EnvidoChallenged();
            lastChallengeType = ChallengeType.Envido;
            SetCanPlayCard(false);
        }
        else if (photonEvent.Code == UIMANAGER.REALENVIDO_CHALLENGE)
        {
            otherPlayerDeniedFlor = true;
            if (lastChallengeType == ChallengeType.Truco)
            {
                challengePoints = 0;
            }
            challengePoints += 3;
            Debug.Log("Truco challenge received");
            UIMANAGER.Instance.RealEnvidoChallenged();
            lastChallengeType = ChallengeType.RealEnvido;
            SetCanPlayCard(false);
        }
        else if (photonEvent.Code == UIMANAGER.FALTAENVIDO_CHALLENGE)
        {
            otherPlayerDeniedFlor = true;
            Debug.Log("Truco challenge received");
            lastChallengeType = ChallengeType.FaltaEnvido;
            UIMANAGER.Instance.FaltaEnvidoChallenged();
            SetCanPlayCard(false);
        }
        else if (photonEvent.Code == UIMANAGER.QUEIRO_CHALLENGE)
        {
            Debug.Log("Truco challenge received");
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
                Debug.LogWarning("Setting My turn Again");
                SetCanPlayCard(true);
            }
        }
        else if (photonEvent.Code == UIMANAGER.NOQUEIRO_CHALLENGE)
        {
            Debug.Log("No Queiro challenge received");
            UIMANAGER.Instance.NoQueiroChallenged();
            ActiveChallenges.Remove(lastChallengeType);
            if (lastChallengeType == ChallengeType.Truco ||
                lastChallengeType == ChallengeType.Retruco ||
                lastChallengeType == ChallengeType.Vale4)
            {
                UIMANAGER.Instance.trucoPlayed = true;
                photonView.RPC(nameof(CheckForTrick), RpcTarget.MasterClient);
                EndRound();
            }
            else
            {
                CheckToAwardsPoints();
            }
        }
        else if (photonEvent.Code == UIMANAGER.FLOR_CHALLENGE)
        {
            if (deniedFlor)
            {
                UIMANAGER.Instance.invokedChallenges.Add(ChallengeType.Flor);
                if (IsMyTurn())
                {
                    SetCanPlayCard(true);
                }
                return;
            }
            Debug.Log("No Queiro challenge received");
            lastChallengeType = ChallengeType.Flor;
            UIMANAGER.Instance.FlorChallenged();
        }
        else if (photonEvent.Code == UIMANAGER.FLOR_CHICA_CHALLENGE)
        {
            Debug.Log("No Queiro challenge received");
            lastChallengeType = ChallengeType.FlorChica;
            UIMANAGER.Instance.FlorChicaChallenged();
            if (IsMyTurn())
            {
                SetCanPlayCard(true);
            }
        }
        else if (photonEvent.Code == UIMANAGER.CON_FLOR_QUIERO_CHALLENGE)
        {
            Debug.Log("No Queiro challenge received");
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
            Debug.Log("No Queiro challenge received");
            lastChallengeType = ChallengeType.ContraFlor;
            UIMANAGER.Instance.ContraFlorChallenged();
        }
        else if (photonEvent.Code == UIMANAGER.MAZO_CHALLENGE)
        {
            int _points = 0;
            if (!lastChallengeType.Equals(ChallengeType.None))
            {
                ActiveChallenges.Remove(lastChallengeType);
                _points = CheckForMazo();
            }
            else if (!cardPlayed)
            {
                if (PlayerHasFlor())
                {
                    _points = 4;
                }
                else
                {
                    if (!UIMANAGER.Instance.trucoPlayed)
                    {
                        if (deniedFlor && UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.Flor))
                        {
                            _points = 1;
                        }
                        else
                        {
                            _points = 2;
                        }
                    }
                    else
                    {
                        _points = 1;
                    }
                }
            }
            else
            {
                _points = 1;
            }
            photonView.RPC(nameof(CheckForTrick), RpcTarget.MasterClient);
            photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, _points);
        }
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
        int pointsToAdd = 0;
        for (int i = 0; i < ActiveChallenges.Count; i++)
        {
            switch (ActiveChallenges[i])
            {
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
            pointsToAdd += 1;
        }

        Debug.LogWarning("Awarding Points");
        myPlayerScoreHandler.UpdateScore(pointsToAdd);
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            _gameEnded = true;
            GameWon();
        }
        ActiveChallenges.Clear();
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

                    int myscore = myPlayerScoreHandler.GetCurrentScore();
                    int otherscore = otherPlayerScoreHandler.GetCurrentScore();
                    int biggerScore = Mathf.Max(myscore, otherscore);

                    int pointsToAward = (biggerScore + challengePoints) > 15 ? 15 - biggerScore : challengePoints;

                    if (player2Score > player1Score)
                    {
                        photonView.RPC(nameof(EnvidoWinner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, pointsToAward);
                    }
                    else
                    {
                        photonView.RPC(nameof(EnvidoWinner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, pointsToAward);
                    }

                    Debug.LogWarning("Envido Points Awarded");
                    challengePoints = 0;
                    break;
                }
            case ChallengeType.FaltaEnvido:
                {
                    Debug.LogWarning("Checking for falta Envido");
                    int player1Score = CalculateEnvido(_player1Cards);
                    int player2Score = CalculateEnvido(_player2Cards);
                    int myscore = myPlayerScoreHandler.GetCurrentScore();
                    int otherscore = otherPlayerScoreHandler.GetCurrentScore();
                    int biggerScore = Mathf.Max(myscore, otherscore);
                    if (player2Score > player1Score)
                    {
                        photonView.RPC(nameof(FaltaEnvidoWinner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, 15 - biggerScore);
                    }
                    else
                    {
                        photonView.RPC(nameof(FaltaEnvidoWinner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, 15 - biggerScore);
                    }
                    break;
                }
            case ChallengeType.ConFlorQuiero:
                {
                    int player1FlorScore = CalculateFlor(_player1Cards);
                    int player2FlorScore = CalculateFlor(_player2Cards);
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

    [PunRPC]
    private void ConFlorQuieroWinner(int ID, int _points)
    {
        if (_isSpectator) return;
        _gameEnded = true;
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            myPlayerScoreHandler.UpdateScore(_points);
            DataHandler.Instance.points = myPlayerScoreHandler.GetCurrentScore();
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
            myPlayerScoreHandler.UpdateScore(_points);
            challengePoints = 0;
        }
        else
        {
            Debug.Log("You lose!");
            otherPlayerScoreHandler.UpdateScore(_points, true);
            challengePoints = 0;
        }
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
        _gameEnded = true;
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            int score = myPlayerScoreHandler.GetCurrentScore();
            int scoreToAdd = 15 - score;
            myPlayerScoreHandler.UpdateScore(scoreToAdd);
            Debug.Log("You Win!");
            UIMANAGER.Instance.UpdateTurnText("You Win!");
            UIMANAGER.Instance.DisableButtons();
            GameWon();
        }
        else
        {
            int score = otherPlayerScoreHandler.GetCurrentScore();
            int scoreToAdd = 15 - score;
            otherPlayerScoreHandler.UpdateScore(scoreToAdd, true);
            Debug.Log("You lose!");
            UIMANAGER.Instance.UpdateTurnText("You lose!");
            UIMANAGER.Instance.DisableButtons();
            GameLost();
        }
    }

    [PunRPC]
    private void FaltaEnvidoWinner(int ID, int _points)
    {
        if (_isSpectator) return;
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            myPlayerScoreHandler.UpdateScore(_points);
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
        {
            _gameEnded = true;
            GameWon();
        }
        else if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            _gameEnded = true;
            GameLost();
        }
        else
        {
            if (!UIMANAGER.Instance.trucoPlayed)
            {
                if (UIMANAGER.Instance.unAnsweredChallenges.Count > 0)
                {
                    StartCoroutine(UIMANAGER.Instance.CheckForUnansweredChallenges());
                }
                else if (IsMyTurn())
                {
                    Debug.LogWarning("Setting My turn Again");
                    SetCanPlayCard(true);
                }
                return;
            }
            Debug.LogWarning("Setting My Player Score: " + myPlayerScoreHandler.GetCurrentScore());
            DataHandler.Instance.points = myPlayerScoreHandler.GetCurrentScore();
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

    private int GetEnvidoValue(int rank)
    {
        return (rank >= 10) ? 0 : rank;
    }

    public void SetCanPlayCard(bool _value)
    {
        _canPlayCard = _value;
        if (_value)
        {
            UIMANAGER.Instance.EnableButtons();
        }
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
        if (!IsMyTurn() || !CanPlayCard() || cards == null) return;
        foreach (var c in cards)
        {
            if (c == null) continue;
            var card = c.GetComponent<Card>();
            if (card != null && card.CanUserSelect())
            {
                card.CommitPlayForTimeout();
                return;
            }
        }
        if (AppManager.Instance != null)
            AppManager.Instance.DisplayNotification(TrucoTextosClient.TiempoEsgotadoJugada);
    }

    public void CardSelected(Transform cardTransform, CardSuit suit, int value)
    {
        SetCanPlayCard(false);
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
        if (calculatedRank1 > calculatedRank2)
        {
            TurnManager.Instance.GiveTurnAgainTo(0);
            Debug.LogWarning("Player 1 W");
        }
        else if (calculatedRank2 > calculatedRank1)
        {
            TurnManager.Instance.GiveTurnAgainTo(1);
            Debug.LogWarning("Player 2 W");
        }
        else
        {
            Debug.LogWarning("Draw");
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
        if (_isSpectator) return;
        UIMANAGER.Instance.DisableButtons();
        if (ID.Equals(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            Debug.Log("You win!");
            myPlayerScoreHandler.UpdateScore(points);
            UIMANAGER.Instance.DisableButtons();
            _gameEnded = true;
        }
        else
        {
            Debug.Log("You lose!");
            UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.PerdisteMano);
            otherPlayerScoreHandler.UpdateScore(points, true);
            UIMANAGER.Instance.DisableButtons();
            _gameEnded = true;
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
            Debug.LogWarning("Setting My Player Score: " + myPlayerScoreHandler.GetCurrentScore());
            DataHandler.Instance.points = myPlayerScoreHandler.GetCurrentScore();
            PhotonNetwork.AutomaticallySyncScene = true;
            ResetGame();
        }
    }

    public void EndRound()
    {
        _gameEnded = true;
        Debug.LogWarning("Setting My Player Score: " + myPlayerScoreHandler.GetCurrentScore());
        DataHandler.Instance.points = myPlayerScoreHandler.GetCurrentScore();
        PhotonNetwork.AutomaticallySyncScene = true;
        UIMANAGER.Instance.DisableButtons();
        ResetGame();
    }

    void TryReport1v1MatchToBackend(string winnerUserId)
    {
        if (_isInTournament || _isSpectator) return;
        if (string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId)) return;
        if (_1v1ResultPosted) return;
        if (string.IsNullOrEmpty(winnerUserId)) return;
        _1v1ResultPosted = true;
        _ = ApiController.SubmitMatchResult1v1(OneVsOneMatchSession.CurrentMatchId, winnerUserId, null);
    }

    private void GameLost()
    {
        Debug.Log("You lost the game!");
        TryReport1v1MatchToBackend(PhotonPlayerHelper.GetOtherTrucoPlayerUserId());
        photonView.Controller.SetScore(myPlayerScoreHandler.GetCurrentScore());
        UIMANAGER.Instance.DisableButtons();

        if (_isInTournament && !_tournamentMatchFinalized)
        {
            OnTournamentMatchLose();
        }
        else
        {
            losePanel.SetActive(true);
            ToggleMenuBtns(true);
        }

        if (ApiController.GetSessionUser?.Data?.stats != null)
            ApiController.GetSessionUser.Data.stats.losses++;
        _gameEnded = true;
    }

    private void GameWon()
    {
        Debug.Log("You won the game!");
        TryReport1v1MatchToBackend(ApiController.GetSessionUser?.Data?._id);
        UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.GanastePartida);
        UIMANAGER.Instance.DisableButtons();

        if (_isInTournament && !_tournamentMatchFinalized)
        {
            OnTournamentMatchWon();
        }
        else
        {
            winPanel.SetActive(true);
            ToggleMenuBtns(true);
        }

        if (ApiController.GetSessionUser?.Data?.stats != null)
            ApiController.GetSessionUser.Data.stats.wins++;
        _gameEnded = true;
    }

    private void ResetGame()
    {
        UIMANAGER.Instance.DisableButtons();
        StartCoroutine(RestartGameAfterDelay());
    }

    private IEnumerator RestartGameAfterDelay()
    {
        yield return new WaitForSeconds(5);
        SceneManager.LoadScene("Gameplay");
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
        otherPlayerScoreHandler.UpdateScore(points, true);
        photonView.RPC(nameof(AwardPoints), RpcTarget.Others, points);
    }

    public void AwardPointsToThisPlayer(int points)
    {
        myPlayerScoreHandler.UpdateScore(points);
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameWon();
        }
        photonView.RPC(nameof(AwardOtherPlayerPoints), RpcTarget.Others, points);
    }

    [PunRPC]
    private void AwardOtherPlayerPoints(int points)
    {
        if (_isSpectator) return;
        otherPlayerScoreHandler.UpdateScore(points, true);
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameWon();
        }
        else if (otherPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            GameLost();
        }

        if (!_gameEnded)
        {
            if (IsMyTurn())
            {
                SetCanPlayCard(true);
            }
            else
            {
                UIMANAGER.Instance.EnableButtons();
            }
        }
    }

    [PunRPC]
    private void AwardPoints(int points)
    {
        if (_isSpectator) return;
        myPlayerScoreHandler.UpdateScore(points);
        if (myPlayerScoreHandler.GetCurrentScore() >= 15)
        {
            _gameEnded = true;
            GameWon();
        }

        if (!_gameEnded && IsMyTurn())
        {
            SetCanPlayCard(true);
        }
    }

    public void CheckAfterTurn()
    {
        CheckForWinner();
        CheckForTurn();
        if (player1Score.Count != player2Score.Count)
            return;

        int player1WinCount = trickResults.Count(r => r == 1);
        int player2WinCount = trickResults.Count(r => r == 2);
        int drawCount = trickResults.Count(r => r == 0);

        Debug.LogWarning("Player 1 Win Count: " + player1WinCount);
        Debug.LogWarning("Player 2 Win Count: " + player2WinCount);

        if (trickResults.Count >= 2)
        {
            if (trickResults.Count >= 3)
            {
                if (player1WinCount > player2WinCount)
                {
                    if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
                    {
                        UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
                        Debug.LogWarning("Checking for Con Flor Quiero");
                        GetScore(ChallengeType.ConFlorQuiero);
                    }
                    CheckChallengePoints();
                    points += challengePoints;
                    photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, points);
                }
                else if (player2WinCount > player1WinCount)
                {
                    if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
                    {
                        UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
                        Debug.LogWarning("Checking for Con Flor Quiero");
                        GetScore(ChallengeType.ConFlorQuiero);
                    }
                    CheckChallengePoints();
                    points += challengePoints;
                    photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, points);
                }
                else
                {
                    int handWinner = 0;
                    for (int i = 0; i < trickResults.Count; i++)
                    {
                        if (trickResults[i] == 1)
                        {
                            handWinner = 1; break;
                        }
                        if (trickResults[i] == 2)
                        {
                            handWinner = 2; break;
                        }
                    }

                    if (handWinner == 1)
                    {
                        if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
                        {
                            UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
                            Debug.LogWarning("Checking for Con Flor Quiero");
                            GetScore(ChallengeType.ConFlorQuiero);
                        }
                        CheckChallengePoints();
                        points += challengePoints;
                        photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, points);
                    }
                    else if (handWinner == 2)
                    {
                        if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
                        {
                            UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
                            Debug.LogWarning("Checking for Con Flor Quiero");
                            GetScore(ChallengeType.ConFlorQuiero);
                        }
                        CheckChallengePoints();
                        points += challengePoints;
                        photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, points);
                    }
                    else
                    {
                        CheckChallengePoints();
                        points += challengePoints;
                        photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, points);
                    }
                }
            }
            else
            {
                if (player1WinCount >= 2 && player2WinCount < 2)
                {
                    if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
                    {
                        UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
                        Debug.LogWarning("Checking for Con Flor Quiero");
                        GetScore(ChallengeType.ConFlorQuiero);
                    }
                    CheckChallengePoints();
                    points += challengePoints;
                    photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, points);
                }
                else if (player2WinCount >= 2 && player1WinCount < 2)
                {
                    if (UIMANAGER.Instance.invokedChallenges.Contains(ChallengeType.ConFlorQuiero))
                    {
                        UIMANAGER.Instance.invokedChallenges.Remove(ChallengeType.ConFlorQuiero);
                        Debug.LogWarning("Checking for Con Flor Quiero");
                        GetScore(ChallengeType.ConFlorQuiero);
                    }
                    CheckChallengePoints();
                    points += challengePoints;
                    photonView.RPC(nameof(Winner), RpcTarget.All, PhotonNetwork.PlayerListOthers[0].ActorNumber, points);
                }
            }
        }
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
        if (_isInTournament) return; // Bracket / API flow handles tournament exits
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