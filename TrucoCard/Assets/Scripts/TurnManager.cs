using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{ 
    public static TurnManager Instance { get; private set; }
    private List<string> _turnOrder = new List<string>();
    
    private int currentTurnIndex = 0;
    private bool _giveTurnAgain = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // This script is use to manage the turns of the players in the game.
    private void Awake()
    {
        Instance = this;
        DataHandler.Instance.roundNumber++;
        if (PhotonNetwork.IsMasterClient)
        {
            // Check who is player 1 based on the round number
            Player masterClient = DataHandler.Instance.roundNumber % 2 == 0
                ? PhotonNetwork.PlayerList[1]
                : PhotonNetwork.PlayerList[0];

            if (masterClient.Equals(PhotonNetwork.LocalPlayer))
            {
                GameManager.Instance.SetCards();
                StartTheTurn();
            }
            
            Debug.LogWarning("Setting Master Client to: " + masterClient.ActorNumber);
            PhotonNetwork.SetMasterClient(masterClient);
        }
    }

    // Set New player 1 Based on the round number
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.LogWarning("OnMaster Client Switched");
        if (newMasterClient.Equals(PhotonNetwork.LocalPlayer))
        {
            GameManager.Instance.SetCards();
            StartTheTurn();
        }
    }

    // This function is called to start the turn of the players
    void StartTheTurn()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            _turnOrder.Add(PhotonNetwork.LocalPlayer.ActorNumber.ToString());
            for (int i = 0; i < PhotonNetwork.CurrentRoom.PlayerCount; i++)
            {
                if (PhotonNetwork.PlayerList[i].ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    _turnOrder.Add(PhotonNetwork.PlayerList[i].ActorNumber.ToString());
                }
            }
            
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
        if (GameManager.Instance._gameEnded)
            return;
        if (PhotonNetwork.LocalPlayer.ActorNumber.ToString() == turnNumber)
        {
            Debug.Log("It's your turn: " + turnNumber);
            UIMANAGER.Instance.UpdateTurnText("Your Turn");
            GameManager.Instance.SetCanPlayCard(true);
            GameManager.Instance.SetMyTurn(true);
            UIMANAGER.Instance.EnableButtons();
        }
        else
        {
            Debug.Log("Waiting for player: " + turnNumber);
            UIMANAGER.Instance.UpdateTurnText("Other Player's Turn");
            GameManager.Instance.SetCanPlayCard(false);
            GameManager.Instance.SetMyTurn(false);
            UIMANAGER.Instance.DisableButtons();
        }
    }

    public void GiveTurnAgainTo(int playerNumber)
    {
        _giveTurnAgain = true;
        currentTurnIndex = playerNumber;
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
        string otherPlayerName = "";
        for (int i = 0; i < PhotonNetwork.CurrentRoom.PlayerCount; i++)
        {
            if (PhotonNetwork.PlayerList[i].ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                otherPlayerName = PhotonNetwork.PlayerList[i].ActorNumber.ToString();
                break;
            }
        }

        currentTurnIndex = _turnOrder.IndexOf(otherPlayerName);
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
