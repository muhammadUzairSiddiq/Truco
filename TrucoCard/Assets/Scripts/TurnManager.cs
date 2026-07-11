using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{ 
    public static TurnManager Instance { get; private set; }
    private List<string> _turnOrder = new List<string>();
    
    private int currentTurnIndex = 0;
    private bool _giveTurnAgain = false;
    private Coroutine _turnTimeoutRoutine;
    private Coroutine _opponentTurnDisplayRoutine;
    const float TurnTimeoutSeconds = 30f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // This script is use to manage the turns of the players in the game.
    private void Awake()
    {
        Instance = this;
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator)
        {
            return;
        }
        DataHandler.Instance.roundNumber++;
        var truco = PhotonPlayerHelper.GetTrucoPlayers();
        if (truco.Count < 2) return;
        if (PhotonNetwork.IsMasterClient)
        {
            Player firstForRound = GetManoPlayerForRound(truco, DataHandler.Instance.roundNumber);
            TrucoRulesScenarioLog.Ok("Mano for new hand",
                "round=" + DataHandler.Instance.roundNumber
                + " firstActor=" + (firstForRound != null ? firstForRound.ActorNumber : -1)
                + " (odd→actorOrder[0], even→actorOrder[1])");

            if (firstForRound != null && firstForRound.Equals(PhotonNetwork.LocalPlayer))
            {
                GameManager.Instance.SetCards();
                StartTheTurn();
            }

            PhotonNetwork.SetMasterClient(firstForRound);
        }
    }

    /// <summary>
    /// Mano / first to play alternates each hand. Players are ordered by ActorNumber;
    /// odd round → lower actor (usually host), even round → higher actor (usually guest).
    /// </summary>
    public static Player GetManoPlayerForRound(List<Player> trucoOrdered, int roundNumber)
    {
        if (trucoOrdered == null || trucoOrdered.Count < 2) return null;
        return roundNumber % 2 == 0 ? trucoOrdered[1] : trucoOrdered[0];
    }

    // Set New player 1 Based on the round number
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator) return;
        // Only block while NuevaMano reload is pending or match ended — not on a fresh scene deal.
        if (GameManager.Instance != null &&
            (GameManager.Instance._gameEnded || GameManager.Instance.IsRestartScheduled()))
        {
            TrucoRulesScenarioLog.Ok("OnMasterClientSwitched skipped deal (restart/match end)");
            return;
        }
        if (newMasterClient != null && newMasterClient.Equals(PhotonNetwork.LocalPlayer))
        {
            TrucoRulesScenarioLog.Ok("OnMasterClientSwitched → deal + StartTheTurn",
                "newMaster=" + newMasterClient.ActorNumber
                + " round=" + DataHandler.Instance.roundNumber);
            GameManager.Instance.SetCards();
            StartTheTurn();
        }
    }

    // This function is called to start the turn of the players
    void StartTheTurn()
    {
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator) return;
        if (!PhotonNetwork.IsMasterClient) return;

        _turnOrder.Clear();
        var t = PhotonPlayerHelper.GetTrucoPlayers();
        if (t.Count < 2) return;
        for (int i = 0; i < t.Count; i++)
            _turnOrder.Add(t[i].ActorNumber.ToString());

        // CRITICAL: do not always start at index 0 (host). Mano must follow roundNumber
        // or after host Mazo the guest never gets MY_TURN on the next hand.
        Player mano = GetManoPlayerForRound(t, DataHandler.Instance.roundNumber);
        currentTurnIndex = 0;
        if (mano != null)
        {
            int idx = _turnOrder.IndexOf(mano.ActorNumber.ToString());
            if (idx >= 0) currentTurnIndex = idx;
        }

        string turnNumber = GetCurrentPlayerTurn();
        TrucoRulesScenarioLog.Ok("StartTheTurn",
            "round=" + DataHandler.Instance.roundNumber
            + " firstActor=" + turnNumber
            + " turnIndex=" + currentTurnIndex
            + " order=" + string.Join(",", _turnOrder));
        StartTurn(turnNumber);
    }
    
    // This function is called to start the turn of the players over network
    private void StartTurn(string turnNumber)
    {
        photonView.RPC(nameof(Turn), RpcTarget.All, turnNumber);
    }

    [PunRPC]
    private void Turn(string turnNumber)
    {
        if (GameManager.Instance == null || GameManager.Instance._gameEnded || GameManager.Instance.HandResolved) return;
        if (_turnOrder.Count == 0)
        {
            var truco = PhotonPlayerHelper.GetTrucoPlayers();
            for (int i = 0; i < truco.Count; i++)
                _turnOrder.Add(truco[i].ActorNumber.ToString());
        }
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator)
        {
            if (UIMANAGER.Instance != null)
                UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.EspectandoAdmin, 4.5f);
            return;
        }
        if (_turnTimeoutRoutine != null)
        {
            StopCoroutine(_turnTimeoutRoutine);
            _turnTimeoutRoutine = null;
        }
        if (_opponentTurnDisplayRoutine != null)
        {
            StopCoroutine(_opponentTurnDisplayRoutine);
            _opponentTurnDisplayRoutine = null;
        }
        if (PhotonNetwork.LocalPlayer.ActorNumber.ToString() == turnNumber)
        {
            TrucoRulesScenarioLog.Ok("TurnStart MY_TURN", "timer=" + TurnTimeoutSeconds + "s actor=" + turnNumber);
            GameManager.Instance.SetMyTurn(true);
            GameManager.Instance.SetCanPlayCard(true);
            UIMANAGER.Instance.EnableButtons();
            _turnTimeoutRoutine = StartCoroutine(TurnTimeoutRoutine(turnNumber));
        }
        else
        {
            TrucoRulesScenarioLog.Opp("TurnStart WAIT_RIVAL", "timer=" + TurnTimeoutSeconds + "s actor=" + turnNumber);
            GameManager.Instance.SetCanPlayCard(false);
            GameManager.Instance.SetMyTurn(false);
            UIMANAGER.Instance.DisableButtons();
            // Opponent also sees a live 30 s countdown (both players watch the same clock).
            _opponentTurnDisplayRoutine = StartCoroutine(OpponentTurnDisplayRoutine());
        }
    }

    // Display-only countdown shown to the player whose turn it is NOT. No auto-play here:
    // the active player's client owns the auto-play and will advance the turn for everyone.
    IEnumerator OpponentTurnDisplayRoutine()
    {
        float d = TurnTimeoutSeconds;
        while (d > 0f)
        {
            if (GameManager.Instance != null && (GameManager.Instance._gameEnded || GameManager.Instance.HandResolved)) yield break;
            if (TrucoPunReconnectionManager.IsWaitingForOpponentReconnect)
            {
                int rem = TrucoPunReconnectionManager.OpponentReconnectSecondsRemaining;
                TrucoPunReconnectionManager.UpdateOpponentAbsentTurnBanner(rem);
                yield return null;
                continue;
            }
            if (UIMANAGER.Instance != null &&
                (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            {
                if (UIMANAGER.Instance != null && !UIMANAGER.Instance._iOweChallengeResponse)
                {
                    int sec = UIMANAGER.Instance.GetChallengeResponseSecondsRemaining();
                    UIMANAGER.Instance.UpdateTurnText(
                        TrucoTextosClient.FormatoBannerEsperandoRivalConSegundos(TrucoTextosClient.EsperandoRespuestaRival, sec),
                        -1f, sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos);
                }
                yield return null;
                continue;
            }
            d -= Time.deltaTime;
            if (UIMANAGER.Instance != null)
            {
                int sec = Mathf.CeilToInt(d);
                if (sec < 0) sec = 0;
                bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
                UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.FormatoBannerTurnoRivalConSegundos(sec), -1f, urgent);
            }
            yield return null;
        }
        // After the 30 s turn window: keep showing reconnect countdown if the rival dropped.
        while (true)
        {
            if (GameManager.Instance != null && (GameManager.Instance._gameEnded || GameManager.Instance.HandResolved)) yield break;
            if (TrucoPunReconnectionManager.IsWaitingForOpponentReconnect)
            {
                int rem = TrucoPunReconnectionManager.OpponentReconnectSecondsRemaining;
                TrucoPunReconnectionManager.UpdateOpponentAbsentTurnBanner(rem);
                yield return null;
                continue;
            }
            if (UIMANAGER.Instance != null &&
                (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            {
                if (!UIMANAGER.Instance._iOweChallengeResponse)
                {
                    int sec = UIMANAGER.Instance.GetChallengeResponseSecondsRemaining();
                    UIMANAGER.Instance.UpdateTurnText(
                        TrucoTextosClient.FormatoBannerEsperandoRivalConSegundos(TrucoTextosClient.EsperandoRespuestaRival, sec),
                        -1f, sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos);
                }
                yield return null;
                continue;
            }
            if (UIMANAGER.Instance != null)
                UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.FormatoBannerEsperandoRival(TrucoTextosClient.EsperandoJugadaRival), -1f);
            yield return null;
        }
    }

    IEnumerator TurnTimeoutRoutine(string turnForActor)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber.ToString() != turnForActor)
            yield break;
        float d = TurnTimeoutSeconds;
        while (d > 0f)
        {
            if (GameManager.Instance != null && (GameManager.Instance._gameEnded || GameManager.Instance.HandResolved)) yield break;
            if (UIMANAGER.Instance != null &&
                (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            {
                if (UIMANAGER.Instance._iOweChallengeResponse)
                {
                    yield return null;
                    continue;
                }
                int sec = UIMANAGER.Instance.GetChallengeResponseSecondsRemaining();
                UIMANAGER.Instance.UpdateTurnText(
                    TrucoTextosClient.FormatoBannerEsperandoRivalConSegundos(TrucoTextosClient.EsperandoRespuestaRival, sec),
                    -1f, sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos);
                yield return null;
                continue;
            }
            d -= Time.deltaTime;
            if (UIMANAGER.Instance != null)
            {
                int sec = Mathf.CeilToInt(d);
                if (sec < 0) sec = 0;
                bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
                UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.FormatoBannerTuTurnoConSegundos(sec), -1f, urgent);
            }
            yield return null;
        }
        if (UIMANAGER.Instance != null && UIMANAGER.Instance._iOweChallengeResponse)
        {
            TrucoRulesScenarioLog.Ok("TurnTimeout → auto NoQuiero (owed challenge response)",
                "actor=" + turnForActor);
            UIMANAGER.Instance.ChallengeNoQueiro();
            yield break;
        }
        if (UIMANAGER.Instance != null &&
            (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
        {
            TrucoRulesScenarioLog.Ok("TurnTimeout skipped (challenge still pending)",
                "actor=" + turnForActor);
            yield break;
        }
        TrucoRulesScenarioLog.Ok("TurnTimeout → Mazo (no play in " + TurnTimeoutSeconds + "s)",
            "actor=" + turnForActor);
        GameManager.Instance?.PlayFirstHandCardOnTimeout();
        StartCoroutine(EnsureMazoResolvedAfterTimeout(turnForActor));
    }

    IEnumerator EnsureMazoResolvedAfterTimeout(string turnForActor)
    {
        yield return new WaitForSeconds(2.5f);
        if (GameManager.Instance == null || GameManager.Instance._gameEnded) yield break;
        if (PhotonNetwork.LocalPlayer.ActorNumber.ToString() != turnForActor) yield break;
        if (GameManager.Instance.HandResolved || GameManager.Instance.IsHandOutcomeLocked())
        {
            TrucoRulesScenarioLog.Ok("Timeout ensure: hand already resolved → no second Mazo",
                "actor=" + turnForActor);
            GameManager.Instance.RequestStateSyncAfterReconnect();
            yield break;
        }
        TrucoRulesScenarioLog.Ok("Timeout ensure → ForceMazoTimeoutResolution",
            "actor=" + turnForActor);
        GameManager.Instance.ForceMazoTimeoutResolution();
    }

    public void GiveTurnAgainTo(int playerNumber)
    {
        _giveTurnAgain = true;
        currentTurnIndex = playerNumber;
    }

    /// <summary>Give the next trick lead to a specific player (by Photon actor number). Winner of a trick leads next.</summary>
    public void GiveTurnAgainToActor(int actorNumber)
    {
        _giveTurnAgain = true;
        int idx = _turnOrder.IndexOf(actorNumber.ToString());
        currentTurnIndex = idx >= 0 ? idx : 0;
    }
    
    // This function is called to end the turn of the players
    public void EndTurn()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            SwitchTurn();
        }
        else
        {
            photonView.RPC(nameof(SwitchTurn),RpcTarget.MasterClient);
        }
    }

    public void SwitchTurnSilently()
    {
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator) return;
        var tr = PhotonPlayerHelper.GetTrucoPlayers();
        string otherPlayerName = string.Empty;
        foreach (var p in tr)
        {
            if (p.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                otherPlayerName = p.ActorNumber.ToString();
                break;
            }
        }
        if (string.IsNullOrEmpty(otherPlayerName)) return;
        currentTurnIndex = _turnOrder.IndexOf(otherPlayerName);
        if (currentTurnIndex < 0) currentTurnIndex = 0;
        photonView.RPC(nameof(SwitchTurnSilently),RpcTarget.OthersBuffered, currentTurnIndex);
    }    
    
    [PunRPC]
    private void SwitchTurnSilently(int turnIndex)
    {
        currentTurnIndex = turnIndex;
    }
    
    [PunRPC]
    private void SwitchTurn()
    {
        if (GameManager.Instance != null && GameManager.Instance.HandResolved) return;
        GameManager.Instance.CheckAfterTurn();
        
        if (_giveTurnAgain)
        {
            _giveTurnAgain = false;
            string nextPlayerTurn = GetCurrentPlayerTurn();
            StartTurn(nextPlayerTurn);
        }
        else
        {
            currentTurnIndex++;
            if (currentTurnIndex >= _turnOrder.Count)
            {
                currentTurnIndex = 0; // Reset to the first player
            }

            string nextPlayerTurn = GetCurrentPlayerTurn();
            StartTurn(nextPlayerTurn);
        }
    }
    
    /// <summary>Restart the active 30 s turn clock after a canto closes (full fresh window).</summary>
    public void RestartTurnTimersIfActive()
    {
        if (GameManager.Instance == null || GameManager.Instance._gameEnded || GameManager.Instance.HandResolved) return;
        if (UIMANAGER.Instance != null &&
            (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            return;
        if (_turnTimeoutRoutine != null)
        {
            StopCoroutine(_turnTimeoutRoutine);
            _turnTimeoutRoutine = null;
        }
        if (_opponentTurnDisplayRoutine != null)
        {
            StopCoroutine(_opponentTurnDisplayRoutine);
            _opponentTurnDisplayRoutine = null;
        }
        string actor = PhotonNetwork.LocalPlayer.ActorNumber.ToString();
        if (GameManager.Instance.IsMyTurn())
        {
            TrucoRulesScenarioLog.Ok("TimerReset MY_TURN after canto close", "fresh=" + TurnTimeoutSeconds + "s");
            _turnTimeoutRoutine = StartCoroutine(TurnTimeoutRoutine(actor));
            UIMANAGER.Instance?.EnableButtons();
        }
        else
        {
            TrucoRulesScenarioLog.Ok("TimerReset WAIT_RIVAL after canto close", "fresh=" + TurnTimeoutSeconds + "s");
            _opponentTurnDisplayRoutine = StartCoroutine(OpponentTurnDisplayRoutine());
        }
    }

    private string GetCurrentPlayerTurn()
    {
        string currentPlayerId = _turnOrder[currentTurnIndex];
        return currentPlayerId;
    }

    public int GetCurrentTurnActorNumber()
    {
        string id = GetCurrentPlayerTurn();
        return int.TryParse(id, out int n) ? n : PhotonNetwork.LocalPlayer.ActorNumber;
    }

    public void ResumeTurnAfterSync(int turnActor)
    {
        if (GameManager.Instance == null || GameManager.Instance._gameEnded) return;
        if (GameManager.Instance.IsHandOutcomeLocked()) return;
        int idx = _turnOrder.IndexOf(turnActor.ToString());
        if (idx >= 0) currentTurnIndex = idx;
        bool mine = PhotonNetwork.LocalPlayer.ActorNumber == turnActor;
        GameManager.Instance.SetMyTurn(mine);
        GameManager.Instance.SetCanPlayCard(mine);
        if (mine) UIMANAGER.Instance?.EnableButtons();
        else UIMANAGER.Instance?.DisableButtons();
    }

    /// <summary>Stop turn countdown coroutines when a hand ends (Mazo, trick win, etc.).</summary>
    public void StopAllTurnTimers()
    {
        TrucoRulesScenarioLog.Ok("TimersStopped (hand resolved / mazo / winner)");
        if (_turnTimeoutRoutine != null)
        {
            StopCoroutine(_turnTimeoutRoutine);
            _turnTimeoutRoutine = null;
        }
        if (_opponentTurnDisplayRoutine != null)
        {
            StopCoroutine(_opponentTurnDisplayRoutine);
            _opponentTurnDisplayRoutine = null;
        }
    }
}
