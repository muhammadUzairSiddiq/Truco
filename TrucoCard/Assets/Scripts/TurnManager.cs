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
            Player firstForRound = DataHandler.Instance.roundNumber % 2 == 0 ? truco[1] : truco[0];

            if (firstForRound != null && firstForRound.Equals(PhotonNetwork.LocalPlayer))
            {
                GameManager.Instance.SetCards();
                StartTheTurn();
            }
            
            Debug.LogWarning("Truco first actor: " + firstForRound.ActorNumber);
            PhotonNetwork.SetMasterClient(firstForRound);
        }
    }

    // Set New player 1 Based on the round number
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.LogWarning("OnMaster Client Switched");
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator) return;
        if (newMasterClient.Equals(PhotonNetwork.LocalPlayer))
        {
            GameManager.Instance.SetCards();
            StartTheTurn();
        }
    }

    // This function is called to start the turn of the players
    void StartTheTurn()
    {
        if (PhotonPlayerHelper.IsSpectatorPlayer(PhotonNetwork.LocalPlayer) || SpectatorContext.IsSpectator) return;
        if (PhotonNetwork.IsMasterClient)
        {
            _turnOrder.Clear();
            var t = PhotonPlayerHelper.GetTrucoPlayers();
            if (t.Count < 2) return;
            for (int i = 0; i < t.Count; i++) _turnOrder.Add(t[i].ActorNumber.ToString());
            string turnNumber = GetCurrentPlayerTurn();
            StartTurn(turnNumber);
        } 
    }
    
    // This function is called to start the turn of the players over network
    private void StartTurn(string turnNumber)
    {
        photonView.RPC(nameof(Turn), RpcTarget.All, turnNumber);
    }

    [PunRPC]
    private void Turn(string turnNumber)
    {
        if (GameManager.Instance == null || GameManager.Instance._gameEnded) return;
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
            Debug.Log("It's your turn: " + turnNumber);
            GameManager.Instance.SetCanPlayCard(true);
            GameManager.Instance.SetMyTurn(true);
            UIMANAGER.Instance.EnableButtons();
            _turnTimeoutRoutine = StartCoroutine(TurnTimeoutRoutine(turnNumber));
        }
        else
        {
            Debug.Log("Waiting for player: " + turnNumber);
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
            if (GameManager.Instance != null && GameManager.Instance._gameEnded) yield break;
            if (UIMANAGER.Instance != null &&
                (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            {
                // A canto is open. If I'm not the one who must respond, show that I'm waiting.
                if (UIMANAGER.Instance != null && !UIMANAGER.Instance._iOweChallengeResponse)
                    UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.EsperandoRespuestaRival, -1f);
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
        // Countdown finished — show waiting until the next Turn RPC resets the banner.
        if (UIMANAGER.Instance != null)
            UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.EsperandoJugadaRival, -1f);
    }

    IEnumerator TurnTimeoutRoutine(string turnForActor)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber.ToString() != turnForActor)
            yield break;
        float d = TurnTimeoutSeconds;
        while (d > 0f)
        {
            if (GameManager.Instance != null && GameManager.Instance._gameEnded) yield break;
            if (UIMANAGER.Instance != null &&
                (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            {
                // Canto open: if I'm waiting on the opponent's answer, show it (the responder runs their own 30 s timer).
                if (!UIMANAGER.Instance._iOweChallengeResponse)
                    UIMANAGER.Instance.UpdateTurnText(TrucoTextosClient.EsperandoRespuestaRival, -1f);
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
        if (UIMANAGER.Instance != null &&
            (UIMANAGER.Instance._isChallengepPending || UIMANAGER.Instance.unAnsweredChallenges.Count > 0))
            yield break;
        GameManager.Instance?.PlayFirstHandCardOnTimeout();
        // Safety net: if the RPC chain doesn't advance the turn within 2 s, master forces EndTurn.
        StartCoroutine(ForceAdvanceTurnIfStillStuck(turnForActor));
    }

    IEnumerator ForceAdvanceTurnIfStillStuck(string turnForActor)
    {
        yield return new WaitForSeconds(2f);
        if (GameManager.Instance == null || GameManager.Instance._gameEnded) yield break;
        if (PhotonNetwork.LocalPlayer.ActorNumber.ToString() != turnForActor) yield break;
        if (!GameManager.Instance.IsMyTurn()) yield break;
        if (PhotonNetwork.IsMasterClient)
            EndTurn();
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
    
    private string GetCurrentPlayerTurn()
    {
        string currentPlayerId = _turnOrder[currentTurnIndex];
        return currentPlayerId;
    }
    
}
